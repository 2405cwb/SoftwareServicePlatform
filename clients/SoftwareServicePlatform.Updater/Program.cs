using System.Diagnostics;
using System.Text.Json;
using SoftwareServicePlatform.Updater;
using System.Runtime.InteropServices;
using System.Text;
internal static class Program
{
    private static readonly JsonSerializerOptions
        JsonOptions =
            new()
            {
                PropertyNameCaseInsensitive =
                    true
            };


    public static async Task<int> Main(
        string[] args)
    {
        try
        {
            var arguments =
                ParseArguments(
                    args
                );

            var configPath =
                arguments.TryGetValue(
                    "config",
                    out var configArgument)
                    ? Path.GetFullPath(
                        configArgument
                    )
                    : Path.Combine(
                        AppContext.BaseDirectory,
                        "updater.json"
                    );

            if (!File.Exists(
                    configPath))
            {
                throw new FileNotFoundException(
                    "找不到 updater.json",
                    configPath
                );
            }

            var config =
                JsonSerializer
                    .Deserialize<
                        UpdaterConfig>(
                            await File
                                .ReadAllTextAsync(
                                    configPath
                                ),
                            JsonOptions
                        )
                ?? throw new InvalidOperationException(
                    "updater.json 格式无效"
                );

            ValidateConfig(
                config
            );

            /*
             * 推荐目录：
             *
             * AppRoot/
             * ├─ MainApp.exe
             * ├─ version.txt
             * └─ updater/
             *    ├─ SoftwareServicePlatform.Updater.exe
             *    └─ updater.json
             *
             * 因此默认 AppRoot = Updater 所在目录的上一级。
             */
            var appRoot =
                arguments.TryGetValue(
                    "app-root",
                    out var appRootArgument)
                    ? Path.GetFullPath(
                        appRootArgument
                    )
                    : Path.GetFullPath(
                        Path.Combine(
                            AppContext.BaseDirectory,
                            ".."
                        )
                    );

            int? waitPid =
                null;

            if (
                arguments.TryGetValue(
                    "wait-pid",
                    out var pidText)
                &&
                int.TryParse(
                    pidText,
                    out var pid)
                &&
                pid > 0
            )
            {
                waitPid =
                    pid;
            }

            var engine =
                new UpdaterEngine(
                    config,
                    appRoot
                );

            var currentVersion =
                engine.ReadCurrentVersion();

            Console.WriteLine(
                $"当前版本：{currentVersion}"
            );

            var update =
                await engine.CheckAsync(
                    currentVersion
                );

            if (!update.HasUpdate)
            {
                Console.WriteLine(
                    "当前已经是最新版本。"
                );

                return 0;
            }

            Console.WriteLine(
                $"发现新版本：{update.LatestVersion}"
            );

            Console.WriteLine(
                $"强制升级：{(update.ForceUpdate ? "是" : "否")}"
            );

            if (
                !string.IsNullOrWhiteSpace(
                    update.ReleaseNotes)
            )
            {
                Console.WriteLine(
                    "更新说明："
                );

                Console.WriteLine(
                    update.ReleaseNotes
                );
            }

            /*
             * 本 Updater 的职责是“执行已经确认的更新”。
             *
             * 是否弹窗、是否允许稍后更新，
             * 由主程序决定。
             *
             * Qt / WinForms 示例中：
             *
             * 主程序先调用 check 接口
             * ↓
             * 用户点击“立即更新”
             * ↓
             * 启动本 Updater
             * ↓
             * 主程序退出
             */
            await engine.ApplyAsync(
                update,
                waitPid
            );

            return 0;
        }
        catch (Exception ex)
        {
            WriteFailureLog(ex);

            Console.Error.WriteLine(
                "自动更新失败："
                + ex);

            ShowErrorMessage(ex);

            return 1;
        }
    }


    private static Dictionary<
        string,
        string>
        ParseArguments(
            string[] args)
    {
        var result =
            new Dictionary<
                string,
                string>(
                    StringComparer
                        .OrdinalIgnoreCase
                );

        for (
            var i = 0;
            i < args.Length;
            i++
        )
        {
            var key =
                args[i];

            if (
                !key.StartsWith(
                    "--")
            )
            {
                continue;
            }

            key =
                key[2..];

            if (
                i + 1
                < args.Length
                &&
                !args[i + 1]
                    .StartsWith(
                        "--")
            )
            {
                result[key] =
                    args[++i];
            }
            else
            {
                result[key] =
                    "true";
            }
        }

        return result;
    }


    private static void ValidateConfig(
        UpdaterConfig config)
    {
        if (
            !Uri.TryCreate(
                config.ServerUrl,
                UriKind.Absolute,
                out var serverUri)
            ||
            (
                serverUri.Scheme
                != Uri.UriSchemeHttps
                &&
                serverUri.Scheme
                != Uri.UriSchemeHttp
            )
        )
        {
            throw new InvalidOperationException(
                "updater.json 的 serverUrl 无效"
            );
        }

        if (
            string.IsNullOrWhiteSpace(
                config.SoftwareCode)
        )
        {
            throw new InvalidOperationException(
                "updater.json 缺少 softwareCode"
            );
        }

        if (
            string.IsNullOrWhiteSpace(
                config.UpdateToken)
        )
        {
            throw new InvalidOperationException(
                "updater.json 缺少 updateToken"
            );
        }

        if (
            string.IsNullOrWhiteSpace(
                config.VersionFile)
        )
        {
            config.VersionFile =
                "version.txt";
        }
    }


    private static void WriteFailureLog(
    Exception ex)
    {
        try
        {
            var logPath =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "updater.log");

            var message =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] "
                + "自动更新失败："
                + ex
                + Environment.NewLine;

            File.AppendAllText(
                logPath,
                message,
                Encoding.UTF8);
        }
        catch
        {
            // 日志写入失败不能覆盖原始错误。
        }
    }


    private static void ShowErrorMessage(
        Exception ex)
    {
        try
        {
            var message =
                "自动更新失败。\r\n\r\n"
                + ex.Message
                + "\r\n\r\n"
                + "详细信息已写入：\r\n"
                + Path.Combine(
                    AppContext.BaseDirectory,
                    "updater.log");

            MessageBoxW(
                IntPtr.Zero,
                message,
                "软件自动更新失败",
                0x00000010);
        }
        catch
        {
            // 弹窗失败时仍然保留控制台和日志。
        }
    }


    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern int MessageBoxW(
        IntPtr hWnd,
        string text,
        string caption,
        uint type);
}
