using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace YourApplication
{
    /// <summary>
    /// .NET Framework 4.8 WinForms 自动更新接入示例。
    ///
    /// 新流程：
    /// 1. 安装包只携带非敏感的 updater.bootstrap.json；
    /// 2. 第一次启动如果没有 updater.json，就提示客户输入一次性更新激活码；
    /// 3. 激活成功后服务器给“这一台安装实例”签发独立 UpdateToken；
    /// 4. 客户端自动生成 updater.json；
    /// 5. 以后启动直接使用设备自己的 UpdateToken 检查更新。
    ///
    /// 客户不需要手工填写 UpdateToken。
    /// </summary>
    public static class AutoUpdateHelper
    {
        private static readonly JavaScriptSerializer Json =
            new JavaScriptSerializer();

        public static async Task CheckOnStartupAsync(
            IWin32Window owner,
            string currentVersion,
            string updaterExePath,
            string appRootPath)
        {
            try
            {
                var config = await LoadOrActivateUpdaterConfigAsync(
                    owner,
                    updaterExePath);

                /*
                 * 用户取消首次更新授权、bootstrap 配置缺失，
                 * 或激活失败时，不阻止业务软件正常启动。
                 */
                if (config == null)
                {
                    return;
                }

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);

                    client.DefaultRequestHeaders.Add(
                        "X-Update-Token",
                        config.updateToken);

                    var body = Json.Serialize(
                        new
                        {
                            softwareCode = config.softwareCode,
                            currentVersion = currentVersion
                        });

                    var response = await client.PostAsync(
                        config.serverUrl.TrimEnd('/')
                        + "/api/client-updates/check",
                        new StringContent(
                            body,
                            Encoding.UTF8,
                            "application/json"));

                    /*
                     * 自动更新检查失败不阻止业务软件正常使用。
                     * 例如服务器临时不可用、设备凭证被管理员停用等。
                     */
                    if (!response.IsSuccessStatusCode)
                    {
                        return;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var result = Json.Deserialize<UpdateCheckResult>(json);

                    if (result == null || !result.hasUpdate)
                    {
                        return;
                    }

                    var message = "发现新版本：" + result.latestVersion;

                    if (!string.IsNullOrWhiteSpace(result.releaseNotes))
                    {
                        message +=
                            "\r\n\r\n更新说明：\r\n"
                            + result.releaseNotes;
                    }

                    var buttons = result.forceUpdate
                        ? MessageBoxButtons.OKCancel
                        : MessageBoxButtons.YesNo;

                    var answer = MessageBox.Show(
                        owner,
                        message,
                        result.forceUpdate ? "必须升级" : "发现软件更新",
                        buttons,
                        MessageBoxIcon.Information);

                    var accepted = result.forceUpdate
                        ? answer == DialogResult.OK
                        : answer == DialogResult.Yes;

                    if (!accepted)
                    {
                        if (result.forceUpdate)
                        {
                            Application.Exit();
                        }

                        return;
                    }

                    if (!File.Exists(updaterExePath))
                    {
                        MessageBox.Show(
                            owner,
                            "找不到自动更新程序：\r\n" + updaterExePath,
                            "更新失败",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        if (result.forceUpdate)
                        {
                            Application.Exit();
                        }

                        return;
                    }

                    var pid = Process.GetCurrentProcess().Id;

                    var arguments =
                        "--config "
                        + Quote(config.configFilePath)
                        + " --app-root "
                        + Quote(appRootPath)
                        + " --wait-pid "
                        + pid;

                    try
                    {
                        Process.Start(
                            new ProcessStartInfo
                            {
                                FileName = updaterExePath,
                                Arguments = arguments,
                                WorkingDirectory = Path.GetDirectoryName(
                                    updaterExePath),
                                UseShellExecute = true
                            });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            owner,
                            "无法启动自动更新程序。\r\n\r\n" + ex.Message,
                            "更新失败",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        if (result.forceUpdate)
                        {
                            Application.Exit();
                        }

                        return;
                    }

                    Application.Exit();
                }
            }
            catch
            {
                /*
                 * 自动更新属于辅助能力。
                 * 不应该因为平台临时不可用而阻止业务软件启动。
                 */
            }
        }

        /// <summary>
        /// 优先读取已经激活后生成的 updater.json。
        /// 如果不存在，则尝试执行第一次更新授权。
        /// </summary>
        private static async Task<UpdaterConfig> LoadOrActivateUpdaterConfigAsync(
            IWin32Window owner,
            string updaterExePath)
        {
            var updaterDirectory = Path.GetDirectoryName(updaterExePath);

            if (string.IsNullOrWhiteSpace(updaterDirectory))
            {
                return null;
            }

            var bootstrapPath = Path.Combine(
                updaterDirectory,
                "updater.bootstrap.json");

            BootstrapConfig bootstrap = null;

            if (File.Exists(bootstrapPath))
            {
                try
                {
                    bootstrap = Json.Deserialize<BootstrapConfig>(
                        File.ReadAllText(bootstrapPath, Encoding.UTF8));
                }
                catch
                {
                    bootstrap = null;
                }
            }

            /*
             * 新版把含有 UpdateToken 的 updater.json
             * 放到当前 Windows 用户可写的 LocalAppData。
             *
             * 这样即使软件安装在 C:\Program Files，
             * 普通用户第一次更新授权时也不会因为没有写权限而失败。
             */
            string configPath = null;

            if (bootstrap != null
                && !string.IsNullOrWhiteSpace(bootstrap.softwareCode))
            {
                configPath = GetWritableConfigPath(bootstrap.softwareCode);

                var existing = TryLoadUpdaterConfig(configPath);

                if (existing != null)
                {
                    existing.configFilePath = configPath;
                    return existing;
                }
            }

            /*
             * 兼容旧版：
             * 如果安装目录 updater/updater.json 已经存在，
             * 仍然可以继续使用。
             */
            var legacyConfigPath = Path.Combine(
                updaterDirectory,
                "updater.json");

            var legacy = TryLoadUpdaterConfig(legacyConfigPath);

            if (legacy != null)
            {
                legacy.configFilePath = legacyConfigPath;
                return legacy;
            }

            if (!File.Exists(bootstrapPath))
            {
                return null;
            }

            BootstrapConfig bootstrapFromFile;

            try
            {
                bootstrapFromFile = Json.Deserialize<BootstrapConfig>(
                    File.ReadAllText(bootstrapPath, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    owner,
                    "更新授权配置读取失败：\r\n" + ex.Message,
                    "设备更新授权",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return null;
            }

            bootstrap = bootstrapFromFile;

            if (bootstrap == null
                || string.IsNullOrWhiteSpace(bootstrap.serverUrl)
                || string.IsNullOrWhiteSpace(bootstrap.softwareCode))
            {
                MessageBox.Show(
                    owner,
                    "updater.bootstrap.json 配置不完整。",
                    "设备更新授权",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return null;
            }

            configPath = GetWritableConfigPath(bootstrap.softwareCode);

            var activationCode = ActivationCodeDialog.ShowActivationCode(owner);

            if (string.IsNullOrWhiteSpace(activationCode))
            {
                return null;
            }

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);

                    var requestBody = Json.Serialize(
                        new
                        {
                            activationCode = activationCode,
                            softwareCode = bootstrap.softwareCode,
                            deviceName = Environment.MachineName
                        });

                    var response = await client.PostAsync(
                        bootstrap.serverUrl.TrimEnd('/')
                        + "/api/client-activation/activate",
                        new StringContent(
                            requestBody,
                            Encoding.UTF8,
                            "application/json"));

                    var responseText = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        MessageBox.Show(
                            owner,
                            "设备更新授权失败：\r\n" + responseText,
                            "设备更新授权",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        return null;
                    }

                    var result = Json.Deserialize<ActivationResult>(responseText);

                    if (result == null
                        || string.IsNullOrWhiteSpace(result.updateToken))
                    {
                        MessageBox.Show(
                            owner,
                            "设备更新授权失败：服务器没有返回更新凭证。",
                            "设备更新授权",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        return null;
                    }

                    var config = new UpdaterConfig
                    {
                        serverUrl = bootstrap.serverUrl.TrimEnd('/'),
                        softwareCode = bootstrap.softwareCode,
                        updateToken = result.updateToken,
                        versionFile = string.IsNullOrWhiteSpace(bootstrap.versionFile)
                            ? "version.txt"
                            : bootstrap.versionFile,
                        mainExecutable = bootstrap.mainExecutable,
                        fallbackToFullInstaller = bootstrap.fallbackToFullInstaller,
                        restartAfterUpdate = bootstrap.restartAfterUpdate,
                        waitForProcessSeconds = bootstrap.waitForProcessSeconds <= 0
                            ? 60
                            : bootstrap.waitForProcessSeconds
                    };

                    /*
                     * updater.json 中会包含设备 UpdateToken，
                     * 所以不要把这个文件提交到 Git。
                     */
                    var configDirectory = Path.GetDirectoryName(configPath);

                    if (!string.IsNullOrWhiteSpace(configDirectory))
                    {
                        Directory.CreateDirectory(configDirectory);
                    }

                    config.configFilePath = configPath;

                    File.WriteAllText(
                        configPath,
                        Json.Serialize(config),
                        new UTF8Encoding(false));

                    MessageBox.Show(
                        owner,
                        "设备更新授权成功。\r\n\r\n设备："
                        + (string.IsNullOrWhiteSpace(result.deviceName)
                            ? Environment.MachineName
                            : result.deviceName),
                        "设备更新授权",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    return config;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    owner,
                    "设备更新授权失败：\r\n" + ex.Message,
                    "设备更新授权",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return null;
            }
        }

        private static UpdaterConfig TryLoadUpdaterConfig(string configPath)
        {
            if (!File.Exists(configPath))
            {
                return null;
            }

            try
            {
                var config = Json.Deserialize<UpdaterConfig>(
                    File.ReadAllText(configPath, Encoding.UTF8));

                if (config == null
                    || string.IsNullOrWhiteSpace(config.serverUrl)
                    || string.IsNullOrWhiteSpace(config.softwareCode)
                    || string.IsNullOrWhiteSpace(config.updateToken))
                {
                    return null;
                }

                return config;
            }
            catch
            {
                return null;
            }
        }

        private static string GetWritableConfigPath(string softwareCode)
        {
            var safeCode = string.IsNullOrWhiteSpace(softwareCode)
                ? "default"
                : softwareCode.Trim();

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                safeCode = safeCode.Replace(invalid, '_');
            }

            var root = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

            return Path.Combine(
                root,
                "SoftwareServicePlatform",
                "UpdaterConfigs",
                safeCode,
                "updater.json");
        }

        private static string Quote(string value)
        {
            return "\""
                + value.Replace("\"", "\\\"")
                + "\"";
        }

        private sealed class BootstrapConfig
        {
            public string serverUrl { get; set; }
            public string softwareCode { get; set; }
            public string versionFile { get; set; }
            public string mainExecutable { get; set; }
            public bool fallbackToFullInstaller { get; set; }
            public bool restartAfterUpdate { get; set; }
            public int waitForProcessSeconds { get; set; }
        }

        private sealed class UpdaterConfig
        {
            [ScriptIgnore]
            public string configFilePath { get; set; }

            public string serverUrl { get; set; }
            public string softwareCode { get; set; }
            public string updateToken { get; set; }
            public string versionFile { get; set; }
            public string mainExecutable { get; set; }
            public bool fallbackToFullInstaller { get; set; }
            public bool restartAfterUpdate { get; set; }
            public int waitForProcessSeconds { get; set; }
        }

        private sealed class ActivationResult
        {
            public string updateToken { get; set; }
            public string installationId { get; set; }
            public string deviceName { get; set; }
            public string softwareCode { get; set; }
            public string softwareName { get; set; }
            public string customerName { get; set; }
        }

        private sealed class UpdateCheckResult
        {
            public bool hasUpdate { get; set; }
            public string latestVersion { get; set; }
            public string releaseNotes { get; set; }
            public bool forceUpdate { get; set; }
        }
    }
}
