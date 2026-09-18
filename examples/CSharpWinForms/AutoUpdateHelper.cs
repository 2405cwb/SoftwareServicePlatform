using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    ///
    /// System.Net.Http
    /// System.Web.Extensions
    ///
    /// 主程序只负责：
    /// 1. 检查版本；
    /// 2. 弹窗；
    /// 3. 启动独立 Updater.exe；
    /// 4. 正常退出。
    ///
    /// 文件下载 / SHA256 / 备份 / 回滚由 Updater 完成。
    /// </summary>
    public static class AutoUpdateHelper
    {
        private static readonly JavaScriptSerializer Json =
            new JavaScriptSerializer();


        public static async Task CheckOnStartupAsync(
            IWin32Window owner,
            string serverUrl,
            string softwareCode,
            string updateToken,
            string currentVersion,
            string updaterExePath,
            string appRootPath)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout =
                        TimeSpan.FromSeconds(15);

                    client.DefaultRequestHeaders.Add(
                        "X-Update-Token",
                        updateToken);

                    var body =
                        Json.Serialize(
                            new
                            {
                                softwareCode =
                                    softwareCode,

                                currentVersion =
                                    currentVersion
                            });

                    var response =
                        await client.PostAsync(
                            serverUrl.TrimEnd('/')
                            + "/api/client-updates/check",
                            new StringContent(
                                body,
                                Encoding.UTF8,
                                "application/json"));

                    /*
                     * 更新检查失败不能导致业务软件打不开。
                     * 正式工程可以写日志。
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
                        /*
                         * 强制升级时，如果用户拒绝，
                         * 业务上通常应该退出软件。
                         *
                         * 你可以根据项目要求决定。
                         */
                        if (result.forceUpdate)
                        {
                            Application.Exit();
                        }

                        return;
                    }

                    if (!System.IO.File.Exists(
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
                        Process.Start(
                            new ProcessStartInfo
                            {
                                FileName =
                                    updaterExePath,

                                Arguments =
                                    arguments,

                                WorkingDirectory =
                                    System.IO.Path
                                        .GetDirectoryName(
                                            updaterExePath),

                                UseShellExecute =
                                    true
                            });
                    }
                    catch
                    {
                        MessageBox.Show(
                            owner,
                            "无法启动自动更新程序。",
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
                     * 正常退出主程序，
                     * 避免 EXE / DLL 被 Windows 锁定。
                     */
                    Application.Exit();
                }
            }
            catch
            {
                /*
                 * 自动更新属于辅助能力。
                 * 非强制情况下更新服务器临时不可用，
                 * 不应该阻止软件正常启动。
                 *
                 * 正式工程建议在这里记录日志。
                 */
            }
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


        private sealed class UpdateCheckResult
        {
            public bool hasUpdate { get; set; }

            public string latestVersion { get; set; }

            public string releaseNotes { get; set; }

            public bool forceUpdate { get; set; }
        }
    }
}
