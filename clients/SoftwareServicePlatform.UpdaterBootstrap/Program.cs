using System.Diagnostics;

namespace SoftwareServicePlatform.UpdaterBootstrap
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                var options =
                    ParseArguments(args);

                var waitPid =
                    RequireInt(
                        options,
                        "--wait-pid");

                var source =
                    RequirePath(
                        options,
                        "--source");

                var target =
                    RequirePath(
                        options,
                        "--target");

                var appRoot =
                    RequirePath(
                        options,
                        "--app-root");

                options.TryGetValue(
                    "--restart-main",
                    out var restartMain);

                WaitForExit(
                    waitPid,
                    TimeSpan.FromMinutes(2));

                ReplaceWithRetries(
                    source,
                    target);

                TryDeleteSelfUpdateDirectory(
                    source);

                if (
                    !string.IsNullOrWhiteSpace(
                        restartMain)
                    &&
                    File.Exists(
                        restartMain))
                {
                    Process.Start(
                        new ProcessStartInfo
                        {
                            FileName =
                                restartMain,
                            WorkingDirectory =
                                appRoot,
                            UseShellExecute =
                                true
                        });
                }

                return 0;
            }
            catch (Exception ex)
            {
                TryLog(ex);
                return 1;
            }
        }

        private static Dictionary<string, string>
            ParseArguments(
                string[] args)
        {
            var result =
                new Dictionary<
                    string,
                    string>(
                        StringComparer
                            .OrdinalIgnoreCase);

            for (
                var i = 0;
                i < args.Length;
                i++)
            {
                if (
                    !args[i].StartsWith(
                        "--",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (
                    i + 1
                    >= args.Length)
                {
                    break;
                }

                result[args[i]] =
                    args[i + 1];

                i++;
            }

            return result;
        }

        private static int RequireInt(
            IReadOnlyDictionary<string, string>
                values,
            string key)
        {
            if (
                !values.TryGetValue(
                    key,
                    out var text)
                ||
                !int.TryParse(
                    text,
                    out var value)
                ||
                value <= 0)
            {
                throw new InvalidOperationException(
                    $"缺少或无效参数：{key}");
            }

            return value;
        }

        private static string RequirePath(
            IReadOnlyDictionary<string, string>
                values,
            string key)
        {
            if (
                !values.TryGetValue(
                    key,
                    out var value)
                ||
                string.IsNullOrWhiteSpace(
                    value))
            {
                throw new InvalidOperationException(
                    $"缺少参数：{key}");
            }

            return Path.GetFullPath(
                value);
        }

        private static void WaitForExit(
            int pid,
            TimeSpan timeout)
        {
            try
            {
                using var process =
                    Process.GetProcessById(
                        pid);

                if (process.HasExited)
                {
                    return;
                }

                if (
                    !process.WaitForExit(
                        (int)timeout
                            .TotalMilliseconds))
                {
                    throw new TimeoutException(
                        "等待旧 Updater 退出超时");
                }
            }
            catch (ArgumentException)
            {
                /*
                 * 进程已经不存在。
                 */
            }
        }

        private static void
            ReplaceWithRetries(
                string source,
                string target)
        {
            if (!File.Exists(source))
            {
                throw new FileNotFoundException(
                    "待安装的新 Updater 不存在",
                    source);
            }

            var targetDirectory =
                Path.GetDirectoryName(
                    target)
                ?? throw new InvalidOperationException(
                    "Updater 目标目录无效");

            Directory.CreateDirectory(
                targetDirectory);

            Exception? lastException =
                null;

            for (
                var i = 0;
                i < 20;
                i++)
            {
                try
                {
                    var tempTarget =
                        target
                        + ".new";

                    File.Copy(
                        source,
                        tempTarget,
                        overwrite: true);

                    File.Move(
                        tempTarget,
                        target,
                        overwrite: true);

                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    Thread.Sleep(
                        500);
                }
            }

            throw new IOException(
                "替换 Updater 失败",
                lastException);
        }

        private static void
            TryDeleteSelfUpdateDirectory(
                string source)
        {
            try
            {
                var directory =
                    Path.GetDirectoryName(
                        source);

                if (
                    !string.IsNullOrWhiteSpace(
                        directory)
                    &&
                    Directory.Exists(
                        directory))
                {
                    Directory.Delete(
                        directory,
                        recursive: true);
                }
            }
            catch
            {
                /*
                 * 清理失败不影响更新结果。
                 */
            }
        }

        private static void TryLog(
            Exception ex)
        {
            try
            {
                var logPath =
                    Path.Combine(
                        AppContext
                            .BaseDirectory,
                        "updater-bootstrap.log");

                File.AppendAllText(
                    logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n");
            }
            catch
            {
            }
        }
    }
}
