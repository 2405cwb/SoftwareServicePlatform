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
    /// 需要项目引用：
    /// System.Net.Http
    /// System.Web.Extensions
    ///
    /// 重要约定：
    /// serverUrl / softwareCode / updateToken 不再写进业务源码，
    /// 全部从 updater/updater.json 读取。
    ///
    /// 主程序只负责：
    /// 1. 读取 updater.json 并检查版本；
    /// 2. 提示用户；
    /// 3. 启动独立 Updater.exe；
    /// 4. 正常退出。
    ///
    /// 下载 / SHA256 / 备份 / 回滚 / UAC / 重启由 Updater 负责。
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
                var config = LoadUpdaterConfig(
                    owner,
                    updaterExePath);

                if (config == null)
                {
                    return;
                }

                using (var client = new HttpClient())
                {
                    client.Timeout =
                        TimeSpan.FromSeconds(15);

                    client.DefaultRequestHeaders.Add(
                        "X-Update-Token",
                        config.updateToken);

                    var body =
                        Json.Serialize(
                            new
                            {
                                softwareCode =
                                    config.softwareCode,

                                currentVersion =
                                    currentVersion
                            });

                    var response =
                        await client.PostAsync(
                            config.serverUrl.TrimEnd('/')
                            + "/api/client-updates/check",
                            new StringContent(
                                body,
                                Encoding.UTF8,
                                "application/json"));

                    /*
                     * 启动时检查失败不阻止业务软件正常使用。
                     * 401/403 等详细原因由 updater.log / 服务端日志排查。
                     */
                    if (!response.IsSuccessStatusCode)
                    {
                        return;
                    }

                    var json =
                        await response.Content
                            .ReadAsStringAsync();

                    var result =
                        Json.Deserialize<UpdateCheckResult>(
                            json);

                    if (result == null
                        || !result.hasUpdate)
                    {
                        return;
                    }

                    var message =
                        "发现新版本："
                        + result.latestVersion;

                    if (!string.IsNullOrWhiteSpace(
                            result.releaseNotes))
                    {
                        message +=
                            "\r\n\r\n更新说明：\r\n"
                            + result.releaseNotes;
                    }

                    var buttons =
                        result.forceUpdate
                            ? MessageBoxButtons.OKCancel
                            : MessageBoxButtons.YesNo;

                    var answer =
                        MessageBox.Show(
                            owner,
                            message,
                            result.forceUpdate
                                ? "必须升级"
                                : "发现软件更新",
                            buttons,
                            MessageBoxIcon.Information);

                    var accepted =
                        result.forceUpdate
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

                    if (!File.Exists(
                            updaterExePath))
                    {
                        MessageBox.Show(
                            owner,
                            "找不到自动更新程序：\r\n"
                            + updaterExePath,
                            "更新失败",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        if (result.forceUpdate)
                        {
                            Application.Exit();
                        }

                        return;
                    }

                    var pid =
                        Process.GetCurrentProcess().Id;

                    var arguments =
                        "--app-root "
                        + Quote(appRootPath)
                        + " --wait-pid "
                        + pid;

                    try
                    {
                        /*
                         * UseShellExecute=true 很重要：
                         * Updater 如果通过自身 manifest 声明需要管理员权限，
                         * Windows Shell 才能正常触发 UAC。
                         *
                         * 业务程序不保存管理员逻辑，也不保存 Token。
                         */
                        Process.Start(
                            new ProcessStartInfo
                            {
                                FileName =
                                    updaterExePath,

                                Arguments =
                                    arguments,

                                WorkingDirectory =
                                    Path.GetDirectoryName(
                                        updaterExePath),

                                UseShellExecute =
                                    true
                            });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            owner,
                            "无法启动自动更新程序。\r\n\r\n"
                            + ex.Message,
                            "更新失败",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        if (result.forceUpdate)
                        {
                            Application.Exit();
                        }

                        return;
                    }

                    /*
                     * Updater 已成功启动后退出业务程序，
                     * 避免 EXE / DLL 被占用。
                     */
                    Application.Exit();
                }
            }
            catch
            {
                /*
                 * 自动更新属于辅助能力。
                 * 检查服务器临时不可用时，不阻止业务程序启动。
                 */
            }
        }

        private static UpdaterConfig LoadUpdaterConfig(
            IWin32Window owner,
            string updaterExePath)
        {
            var updaterDirectory =
                Path.GetDirectoryName(
                    updaterExePath);

            if (string.IsNullOrWhiteSpace(
                    updaterDirectory))
            {
                return null;
            }

            var configPath =
                Path.Combine(
                    updaterDirectory,
                    "updater.json");

            if (!File.Exists(configPath))
            {
                MessageBox.Show(
                    owner,
                    "找不到自动更新配置文件：\r\n"
                    + configPath,
                    "自动更新",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return null;
            }

            UpdaterConfig config;

            try
            {
                var configJson =
                    File.ReadAllText(
                        configPath,
                        Encoding.UTF8);

                config =
                    Json.Deserialize<UpdaterConfig>(
                        configJson);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    owner,
                    "updater.json 读取失败：\r\n"
                    + ex.Message,
                    "自动更新",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return null;
            }

            if (config == null
                || string.IsNullOrWhiteSpace(config.serverUrl)
                || string.IsNullOrWhiteSpace(config.softwareCode)
                || string.IsNullOrWhiteSpace(config.updateToken))
            {
                MessageBox.Show(
                    owner,
                    "updater.json 配置不完整，必须包含：\r\n"
                    + "serverUrl / softwareCode / updateToken",
                    "自动更新",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return null;
            }

            Uri serverUri;

            if (!Uri.TryCreate(
                    config.serverUrl,
                    UriKind.Absolute,
                    out serverUri)
                || (serverUri.Scheme != Uri.UriSchemeHttp
                    && serverUri.Scheme != Uri.UriSchemeHttps))
            {
                MessageBox.Show(
                    owner,
                    "updater.json 的 serverUrl 无效：\r\n"
                    + config.serverUrl,
                    "自动更新",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return null;
            }

            return config;
        }

        private static string Quote(
            string value)
        {
            return "\""
                + value.Replace(
                    "\"",
                    "\\\"")
                + "\"";
        }

        private sealed class UpdaterConfig
        {
            public string serverUrl { get; set; }

            public string softwareCode { get; set; }

            public string updateToken { get; set; }
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
