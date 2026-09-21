using System.Text;
using System.Text.Json;
using SoftwareServicePlatform.Updater;

internal static class Program
{
    private static readonly JsonSerializerOptions
        JsonOptions =
            new()
            {
                PropertyNameCaseInsensitive =
                    true
            };


    /// <summary>
    /// WinForms Updater 程序入口。
    ///
    /// 现在 Updater 不再使用黑色控制台窗口，
    /// 而是启动正式的更新进度窗口 UpdaterForm。
    ///
    /// 流程：
    ///
    /// 1. 解析命令行参数；
    /// 2. 读取 updater.json；
    /// 3. 校验配置；
    /// 4. 确定 AppRoot；
    /// 5. 读取需要等待退出的主程序 PID；
    /// 6. 启动 UpdaterForm；
    /// 7. 更新完成后返回窗口的 ExitCode。
    /// </summary>
    [STAThread]
    public static int Main(
        string[] args)
    {
        try
        {
            /*
             * .NET 6+ WinForms 推荐初始化方式。
             *
             * 它会初始化：
             *
             * 高 DPI
             * 默认字体
             * VisualStyles
             *
             * 必须在创建任何 Form 之前调用。
             */
            ApplicationConfiguration.Initialize();


            /*
             * ==========================================
             * 1. 解析命令行参数
             * ==========================================
             *
             * 支持：
             *
             * --config xxx.json
             * --app-root "C:\xxx"
             * --wait-pid 1234
             */
            var arguments =
                ParseArguments(
                    args
                );


            /*
             * ==========================================
             * 2. 确定 updater.json 路径
             * ==========================================
             *
             * 如果没有显式传 --config，
             * 默认读取 Updater.exe 同目录下：
             *
             * updater.json
             */
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


            if (
                !File.Exists(
                    configPath)
            )
            {
                throw new FileNotFoundException(
                    "找不到 updater.json",
                    configPath
                );
            }


            /*
             * ==========================================
             * 3. 读取 updater.json
             * ==========================================
             */
            var configText =
                File.ReadAllText(
                    configPath,
                    Encoding.UTF8
                );


            var config =
                JsonSerializer
                    .Deserialize<
                        UpdaterConfig>(
                            configText,
                            JsonOptions
                        )
                ?? throw new InvalidOperationException(
                    "updater.json 格式无效"
                );


            ValidateConfig(
                config
            );


            /*
             * ==========================================
             * 4. 确定主程序根目录 AppRoot
             * ==========================================
             *
             * 推荐目录结构：
             *
             * AppRoot/
             * ├─ MainApp.exe
             * ├─ version.txt
             * └─ updater/
             *    ├─ SoftwareServicePlatform.Updater.exe
             *    └─ updater.json
             *
             * 因此：
             *
             * 如果没有传 --app-root，
             * 默认使用 Updater 所在目录的上一级。
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


            if (
                !Directory.Exists(
                    appRoot)
            )
            {
                throw new DirectoryNotFoundException(
                    $"主程序目录不存在：{appRoot}"
                );
            }


            /*
             * ==========================================
             * 5. 获取需要等待退出的主程序 PID
             * ==========================================
             *
             * 主程序启动 Updater 时通常会传：
             *
             * --wait-pid 当前主程序PID
             *
             * Updater 可以先下载，
             * 真正替换文件之前再等待该进程退出。
             */
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


            /*
             * ==========================================
             * 6. 启动正式更新窗口
             * ==========================================
             *
             * UpdaterForm 内部负责：
             *
             * 检查版本
             * 显示检查进度
             * 显示下载进度
             * 显示安装进度
             * 调用 UpdaterEngine
             * 更新完成后自动关闭
             */
            using var form =
                new UpdaterForm(
                    config,
                    appRoot,
                    waitPid
                );


            Application.Run(
                form
            );


            /*
             * UpdaterForm：
             *
             * 0 = 成功
             * 1 = 失败
             */
            return form.ExitCode;
        }
        catch (Exception ex)
        {
            /*
             * 这里主要处理：
             *
             * updater.json 不存在
             * JSON 格式错误
             * 配置字段错误
             * AppRoot 不存在
             * Form 尚未创建之前发生的错误
             *
             * UpdaterForm 启动后的业务异常，
             * 由 UpdaterForm 自己处理。
             */
            WriteFailureLog(
                ex
            );


            ShowStartupError(
                ex
            );


            return 1;
        }
    }


    /// <summary>
    /// 解析命令行参数。
    ///
    /// 例如：
    ///
    /// --config updater.json
    /// --app-root "C:\Program Files\MyApp"
    /// --wait-pid 1234
    ///
    /// 最终解析成：
    ///
    /// config   -> updater.json
    /// app-root -> C:\Program Files\MyApp
    /// wait-pid -> 1234
    /// </summary>
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


            /*
             * 只处理：
             *
             * --xxx
             *
             * 普通参数直接忽略。
             */
            if (
                !key.StartsWith(
                    "--",
                    StringComparison.Ordinal)
            )
            {
                continue;
            }


            key =
                key[2..];


            if (
                string.IsNullOrWhiteSpace(
                    key)
            )
            {
                continue;
            }


            /*
             * 如果后面还有一个不是 -- 开头的值，
             * 就把它作为当前参数值。
             *
             * 例如：
             *
             * --wait-pid 1234
             */
            if (
                i + 1
                <
                args.Length
                &&
                !args[i + 1]
                    .StartsWith(
                        "--",
                        StringComparison.Ordinal)
            )
            {
                result[key] =
                    args[++i];
            }
            else
            {
                /*
                 * 支持纯开关参数：
                 *
                 * --xxx
                 *
                 * 当前虽然还没有使用，
                 * 但保留通用解析能力。
                 */
                result[key] =
                    "true";
            }
        }


        return result;
    }


    /// <summary>
    /// 校验 updater.json 中的核心配置。
    ///
    /// 配置错误必须尽早发现，
    /// 不要等到更新到一半才失败。
    /// </summary>
    private static void ValidateConfig(
        UpdaterConfig config)
    {
        /*
         * ==========================================
         * serverUrl
         * ==========================================
         */
        if (
            !Uri.TryCreate(
                config.ServerUrl,
                UriKind.Absolute,
                out var serverUri)
            ||
            (
                serverUri.Scheme
                !=
                Uri.UriSchemeHttps
                &&
                serverUri.Scheme
                !=
                Uri.UriSchemeHttp
            )
        )
        {
            throw new InvalidOperationException(
                "updater.json 的 serverUrl 无效"
            );
        }


        /*
         * ==========================================
         * softwareCode
         * ==========================================
         */
        if (
            string.IsNullOrWhiteSpace(
                config.SoftwareCode)
        )
        {
            throw new InvalidOperationException(
                "updater.json 缺少 softwareCode"
            );
        }


        /*
         * ==========================================
         * updateToken
         * ==========================================
         *
         * UpdateToken 是当前：
         *
         * 客户 + 软件
         *
         * 的自动更新凭证。
         */
        if (
            string.IsNullOrWhiteSpace(
                config.UpdateToken)
        )
        {
            throw new InvalidOperationException(
                "updater.json 缺少 updateToken"
            );
        }


        /*
         * ==========================================
         * versionFile
         * ==========================================
         *
         * 如果没有填写，
         * 默认使用 version.txt。
         */
        if (
            string.IsNullOrWhiteSpace(
                config.VersionFile)
        )
        {
            config.VersionFile =
                "version.txt";
        }


        /*
         * ==========================================
         * waitForProcessSeconds
         * ==========================================
         *
         * 防止配置成负数或 0。
         */
        if (
            config.WaitForProcessSeconds
            <= 0
        )
        {
            config.WaitForProcessSeconds =
                60;
        }
    }


    /// <summary>
    /// Program 启动阶段发生异常时写 updater.log。
    ///
    /// UpdaterEngine 自己也会写 updater.log，
    /// 两边共用同一个日志文件。
    /// </summary>
    private static void WriteFailureLog(
        Exception ex)
    {
        try
        {
            var logPath =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "updater.log"
                );


            var message =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] "
                +
                "Updater 启动失败："
                +
                ex
                +
                Environment.NewLine;


            File.AppendAllText(
                logPath,
                message,
                Encoding.UTF8
            );
        }
        catch
        {
            /*
             * 日志失败不能覆盖真正的启动异常。
             */
        }
    }


    /// <summary>
    /// UpdaterForm 尚未创建之前发生错误时，
    /// 使用 WinForms MessageBox 告知用户。
    ///
    /// 因为现在项目已经是 WinExe，
    /// 不再依赖 Console.Error。
    /// </summary>
    private static void ShowStartupError(
        Exception ex)
    {
        try
        {
            var logPath =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "updater.log"
                );


            MessageBox.Show(
                "自动更新程序启动失败。\r\n\r\n"
                +
                ex.Message
                +
                "\r\n\r\n"
                +
                "详细信息已写入：\r\n"
                +
                logPath,
                "软件自动更新失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
        catch
        {
            /*
             * 极端情况下 MessageBox 也可能失败。
             *
             * 此时至少 WriteFailureLog 已经尝试写入日志。
             */
        }
    }
}
