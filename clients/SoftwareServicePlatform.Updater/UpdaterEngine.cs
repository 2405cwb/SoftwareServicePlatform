using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace SoftwareServicePlatform.Updater
{
    /// <summary>
    /// 通用 Windows 桌面软件更新引擎。
    ///
    /// 目标：
    ///
    /// 1. 比较本地 SHA256；
    /// 2. 只下载发生变化的文件；
    /// 3. 更新前备份旧文件；
    /// 4. 任意一步失败时自动回滚；
    /// 5. 成功后写 version.txt；
    /// 6. 自动重启主程序；
    /// 7. 增量失败可回退到完整安装包。
    /// </summary>
    public sealed class UpdaterEngine
    {
        private readonly UpdaterConfig
            _config;

        private readonly string
            _appRoot;

        private readonly HttpClient
            _httpClient;


        public UpdaterEngine(
            UpdaterConfig config,
            string appRoot)
        {
            _config =
                config;

            _appRoot =
                Path.GetFullPath(
                    appRoot
                );

            _httpClient =
                new HttpClient
                {
                    BaseAddress =
                        new Uri(
                            config.ServerUrl
                                .TrimEnd('/')
                            + "/"
                        ),

                    Timeout =
                        TimeSpan
                            .FromMinutes(30)
                };

            _httpClient
                .DefaultRequestHeaders
                .Add(
                    "X-Update-Token",
                    config.UpdateToken
                );
        }


        public string ReadCurrentVersion()
        {
            var path =
                GetSafeAppPath(
                    _config.VersionFile
                );

            if (!File.Exists(path))
            {
                /*
                 * 第一次接入自动更新的旧客户端
                 * 可能还没有 version.txt。
                 *
                 * 这种情况下返回 0.0.0，
                 * 平台会把当前已发布版本视为更新。
                 */
                return "0.0.0";
            }

            return File
                .ReadAllText(path)
                .Trim();
        }


        public async Task<
            ClientUpdateCheckResponse>
            CheckAsync(
                string currentVersion,
                CancellationToken cancellationToken =
                    default)
        {
            var response =
                await _httpClient
                    .PostAsJsonAsync(
                        "api/client-updates/check",
                        new ClientUpdateCheckRequest
                        {
                            SoftwareCode =
                                _config.SoftwareCode,

                            CurrentVersion =
                                currentVersion
                        },
                        cancellationToken
                    );

            if (!response.IsSuccessStatusCode)
            {
                var text =
                    await response.Content
                        .ReadAsStringAsync(
                            cancellationToken
                        );

                throw new InvalidOperationException(
                    $"检查更新失败：HTTP {(int)response.StatusCode} {text}"
                );
            }

            var result =
                await response.Content
                    .ReadFromJsonAsync<
                        ClientUpdateCheckResponse>(
                            cancellationToken:
                                cancellationToken
                        );

            return result
                ?? throw new InvalidOperationException(
                    "检查更新接口返回空数据"
                );
        }


        /// <summary>
        /// 应用已经检查到的目标版本。
        /// </summary>
        public async Task ApplyAsync(
            ClientUpdateCheckResponse update,
            int? waitProcessId,
            CancellationToken cancellationToken =
                default)
        {
            if (
                !update.HasUpdate
                ||
                !update.VersionId.HasValue
            )
            {
                Log(
                    "当前已经是最新版本，无需更新。"
                );

                return;
            }

            /*
             * 先把需要的文件完整下载到临时目录。
             *
             * 下载阶段主程序可以仍然运行，
             * 真正替换前才等待主进程退出。
             */
            if (
                update.IncrementalAvailable
                &&
                update.Manifest != null
            )
            {
                try
                {
                    await ApplyIncrementalAsync(
                        update,
                        waitProcessId,
                        cancellationToken
                    );

                    return;
                }
                catch (Exception ex)
                {
                    Log(
                        "增量更新失败："
                        + ex.Message
                    );

                    if (
                        !_config
                            .FallbackToFullInstaller
                        ||
                        !update
                            .FullPackageAvailable
                    )
                    {
                        throw;
                    }

                    Log(
                        "准备回退到完整安装包。"
                    );
                }
            }


            if (
                _config
                    .FallbackToFullInstaller
                &&
                update.FullPackageAvailable
            )
            {
                await DownloadAndRunFullInstallerAsync(
                    update,
                    waitProcessId,
                    cancellationToken
                );

                return;
            }

            throw new InvalidOperationException(
                "当前版本没有可用的增量更新包，也没有完整安装包可兜底"
            );
        }


        private async Task ApplyIncrementalAsync(
            ClientUpdateCheckResponse update,
            int? waitProcessId,
            CancellationToken cancellationToken)
        {
            var manifest =
                update.Manifest
                ?? throw new InvalidOperationException(
                    "增量更新清单为空"
                );

            var versionId =
                update.VersionId!.Value;

            var tempRoot =
                GetSafeAppPath(
                    Path.Combine(
                        ".update-temp",
                        versionId.ToString()
                    )
                );

            var backupRoot =
                GetSafeAppPath(
                    Path.Combine(
                        ".update-backup",
                        DateTime.Now
                            .ToString(
                                "yyyyMMdd-HHmmss"
                            )
                        +
                        "-"
                        +
                        versionId
                    )
                );

            TryDeleteDirectory(
                tempRoot
            );

            Directory.CreateDirectory(
                tempRoot
            );

            var changedFiles =
                new List<
                    ClientUpdateManifestFile>();


            /*
             * ==========================================
             * 1. 本地 SHA256 比较
             * ==========================================
             */
            foreach (
                var file
                in manifest.Files)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var localPath =
                    GetSafeAppPath(
                        file.Path
                    );

                if (
                    File.Exists(localPath)
                )
                {
                    var localSha =
                        await ComputeSha256Async(
                            localPath,
                            cancellationToken
                        );

                    if (
                        string.Equals(
                            localSha,
                            file.Sha256,
                            StringComparison
                                .OrdinalIgnoreCase
                        )
                    )
                    {
                        continue;
                    }
                }

                changedFiles.Add(
                    file
                );
            }

            Log(
                $"目标版本 {update.LatestVersion}：共 {manifest.Files.Count} 个文件，需要下载 {changedFiles.Count} 个。"
            );


            /*
             * ==========================================
             * 2. 下载变化文件到临时目录
             * ==========================================
             */
            for (
                var i = 0;
                i < changedFiles.Count;
                i++
            )
            {
                var file =
                    changedFiles[i];

                Log(
                    $"下载 {i + 1}/{changedFiles.Count}：{file.Path}"
                );

                var tempPath =
                    GetSafeChildPath(
                        tempRoot,
                        file.Path
                    );

                Directory.CreateDirectory(
                    Path.GetDirectoryName(
                        tempPath
                    )!
                );

                var url =
                    "api/client-updates/versions/"
                    +
                    versionId
                    +
                    "/files?path="
                    +
                    Uri.EscapeDataString(
                        file.Path
                    );

                using var response =
                    await _httpClient
                        .GetAsync(
                            url,
                            HttpCompletionOption
                                .ResponseHeadersRead,
                            cancellationToken
                        );

                response
                    .EnsureSuccessStatusCode();

                await using (
                    var input =
                        await response.Content
                            .ReadAsStreamAsync(
                                cancellationToken
                            )
                )
                await using (
                    var output =
                        new FileStream(
                            tempPath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None,
                            1024 * 1024,
                            useAsync: true
                        )
                )
                {
                    await input.CopyToAsync(
                        output,
                        cancellationToken
                    );
                }

                var downloadedSha =
                    await ComputeSha256Async(
                        tempPath,
                        cancellationToken
                    );

                if (
                    !string.Equals(
                        downloadedSha,
                        file.Sha256,
                        StringComparison
                            .OrdinalIgnoreCase
                    )
                )
                {
                    throw new InvalidDataException(
                        $"下载文件 SHA256 校验失败：{file.Path}"
                    );
                }
            }


            /*
             * ==========================================
             * 3. 真正替换前等待主程序退出
             * ==========================================
             */
            await WaitForProcessExitAsync(
                waitProcessId,
                cancellationToken
            );


            /*
             * ==========================================
             * 4. 备份 + 替换 + 删除
             * ==========================================
             */
            Directory.CreateDirectory(
                backupRoot
            );

            var newlyCreated =
                new List<string>();

            var backedUp =
                new List<string>();

            try
            {
                foreach (
                    var file
                    in changedFiles)
                {
                    var destination =
                        GetSafeAppPath(
                            file.Path
                        );

                    var source =
                        GetSafeChildPath(
                            tempRoot,
                            file.Path
                        );

                    if (
                        File.Exists(
                            destination)
                    )
                    {
                        var backup =
                            GetSafeChildPath(
                                backupRoot,
                                file.Path
                            );

                        Directory.CreateDirectory(
                            Path.GetDirectoryName(
                                backup
                            )!
                        );

                        File.Copy(
                            destination,
                            backup,
                            overwrite: true
                        );

                        backedUp.Add(
                            file.Path
                        );
                    }
                    else
                    {
                        newlyCreated.Add(
                            file.Path
                        );
                    }

                    Directory.CreateDirectory(
                        Path.GetDirectoryName(
                            destination
                        )!
                    );

                    File.Copy(
                        source,
                        destination,
                        overwrite: true
                    );
                }


                /*
                 * .update-delete.txt 声明的旧文件，
                 * 删除前同样备份。
                 */
                foreach (
                    var deletePath
                    in manifest.DeletePaths)
                {
                    var destination =
                        GetSafeAppPath(
                            deletePath
                        );

                    if (
                        !File.Exists(
                            destination)
                    )
                    {
                        continue;
                    }

                    var backup =
                        GetSafeChildPath(
                            backupRoot,
                            deletePath
                        );

                    Directory.CreateDirectory(
                        Path.GetDirectoryName(
                            backup
                        )!
                    );

                    File.Copy(
                        destination,
                        backup,
                        overwrite: true
                    );

                    if (
                        !backedUp.Any(
                            x =>
                                string.Equals(
                                    x,
                                    deletePath,
                                    StringComparison
                                        .OrdinalIgnoreCase
                                )
                        )
                    )
                    {
                        backedUp.Add(
                            deletePath
                        );
                    }

                    File.Delete(
                        destination
                    );
                }


                /*
                 * ==========================================
                 * 5. 替换后再次验证目标 SHA256
                 * ==========================================
                 */
                foreach (
                    var file
                    in changedFiles)
                {
                    var destination =
                        GetSafeAppPath(
                            file.Path
                        );

                    var actualSha =
                        await ComputeSha256Async(
                            destination,
                            cancellationToken
                        );

                    if (
                        !string.Equals(
                            actualSha,
                            file.Sha256,
                            StringComparison
                                .OrdinalIgnoreCase
                        )
                    )
                    {
                        throw new InvalidDataException(
                            $"更新后 SHA256 校验失败：{file.Path}"
                        );
                    }
                }


                /*
                 * ==========================================
                 * 6. 所有文件成功后才写新版本号
                 * ==========================================
                 */
                var versionPath =
                    GetSafeAppPath(
                        _config.VersionFile
                    );

                Directory.CreateDirectory(
                    Path.GetDirectoryName(
                        versionPath
                    )!
                );

                await File.WriteAllTextAsync(
                    versionPath,
                    update.LatestVersion,
                    cancellationToken
                );

                Log(
                    $"增量更新成功：{update.CurrentVersion} -> {update.LatestVersion}"
                );
            }
            catch
            {
                Log(
                    "文件替换失败，开始回滚。"
                );

                /*
                 * 新增文件原本不存在，
                 * 回滚时删除。
                 */
                foreach (
                    var relativePath
                    in newlyCreated)
                {
                    try
                    {
                        var destination =
                            GetSafeAppPath(
                                relativePath
                            );

                        if (
                            File.Exists(
                                destination)
                        )
                        {
                            File.Delete(
                                destination
                            );
                        }
                    }
                    catch
                    {
                        // 尽最大努力回滚。
                    }
                }


                /*
                 * 原来存在的文件从备份恢复。
                 */
                foreach (
                    var relativePath
                    in backedUp)
                {
                    try
                    {
                        var backup =
                            GetSafeChildPath(
                                backupRoot,
                                relativePath
                            );

                        if (
                            !File.Exists(
                                backup)
                        )
                        {
                            continue;
                        }

                        var destination =
                            GetSafeAppPath(
                                relativePath
                            );

                        Directory.CreateDirectory(
                            Path.GetDirectoryName(
                                destination
                            )!
                        );

                        File.Copy(
                            backup,
                            destination,
                            overwrite: true
                        );
                    }
                    catch
                    {
                        // 尽最大努力回滚。
                    }
                }

                throw;
            }
            finally
            {
                TryDeleteDirectory(
                    tempRoot
                );
            }


            /*
             * 成功以后备份目录保留。
             *
             * 它只保留“本次发生变化/删除”的旧文件，
             * 方便现场出现问题时人工恢复。
             *
             * 后续你可以增加定时清理策略。
             */
            if (
                _config.RestartAfterUpdate
            )
            {
                RestartMainApplication();
            }
        }


        private async Task
            DownloadAndRunFullInstallerAsync(
                ClientUpdateCheckResponse update,
                int? waitProcessId,
                CancellationToken cancellationToken)
        {
            if (
                !update.VersionId.HasValue
            )
            {
                throw new InvalidOperationException(
                    "完整安装包下载缺少 VersionId"
                );
            }

            var downloadRoot =
                GetSafeAppPath(
                    ".update-temp"
                );

            Directory.CreateDirectory(
                downloadRoot
            );

            var fileName =
                string.IsNullOrWhiteSpace(
                    update.FullPackageFileName)
                    ? $"setup-{update.LatestVersion}.exe"
                    : Path.GetFileName(
                        update.FullPackageFileName
                    );

            var installerPath =
                Path.Combine(
                    downloadRoot,
                    fileName
                );

            var url =
                "api/client-updates/versions/"
                +
                update.VersionId.Value
                +
                "/full-package";

            Log(
                "正在下载完整安装包兜底..."
            );

            using var response =
                await _httpClient
                    .GetAsync(
                        url,
                        HttpCompletionOption
                            .ResponseHeadersRead,
                        cancellationToken
                    );

            response
                .EnsureSuccessStatusCode();

            await using (
                var input =
                    await response.Content
                        .ReadAsStreamAsync(
                            cancellationToken
                        )
            )
            await using (
                var output =
                    new FileStream(
                        installerPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        1024 * 1024,
                        useAsync: true
                    )
            )
            {
                await input.CopyToAsync(
                    output,
                    cancellationToken
                );
            }

            if (
                !string.IsNullOrWhiteSpace(
                    update.FullPackageSha256)
            )
            {
                var sha =
                    await ComputeSha256Async(
                        installerPath,
                        cancellationToken
                    );

                if (
                    !string.Equals(
                        sha,
                        update.FullPackageSha256,
                        StringComparison
                            .OrdinalIgnoreCase
                    )
                )
                {
                    throw new InvalidDataException(
                        "完整安装包 SHA256 校验失败"
                    );
                }
            }

            await WaitForProcessExitAsync(
                waitProcessId,
                cancellationToken
            );

            Log(
                "启动完整安装程序。"
            );

            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        installerPath,

                    UseShellExecute =
                        true
                }
            );
        }


        private async Task
            WaitForProcessExitAsync(
                int? processId,
                CancellationToken cancellationToken)
        {
            if (
                !processId.HasValue
                ||
                processId.Value <= 0
            )
            {
                return;
            }

            Process process;

            try
            {
                process =
                    Process.GetProcessById(
                        processId.Value
                    );
            }
            catch (
                ArgumentException)
            {
                /*
                 * 进程已经退出。
                 */
                return;
            }

            using (process)
            {
                if (process.HasExited)
                {
                    return;
                }

                Log(
                    $"等待主程序退出，PID={processId.Value}..."
                );

                var waitTask =
                    process.WaitForExitAsync(
                        cancellationToken
                    );

                var timeoutTask =
                    Task.Delay(
                        TimeSpan.FromSeconds(
                            Math.Max(
                                5,
                                _config
                                    .WaitForProcessSeconds
                            )
                        ),
                        cancellationToken
                    );

                var completed =
                    await Task.WhenAny(
                        waitTask,
                        timeoutTask
                    );

                if (
                    completed != waitTask
                )
                {
                    throw new TimeoutException(
                        "等待主程序退出超时，请关闭主程序后重试更新"
                    );
                }

                await waitTask;
            }
        }


        private void RestartMainApplication()
        {
            if (
                string.IsNullOrWhiteSpace(
                    _config.MainExecutable)
            )
            {
                return;
            }

            var path =
                GetSafeAppPath(
                    _config.MainExecutable
                );

            if (!File.Exists(path))
            {
                Log(
                    $"主程序不存在，无法自动重启：{path}"
                );

                return;
            }

            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        path,

                    WorkingDirectory =
                        _appRoot,

                    UseShellExecute =
                        true
                }
            );
        }


        private string GetSafeAppPath(
            string relativePath)
        {
            return GetSafeChildPath(
                _appRoot,
                relativePath
            );
        }


        private static string GetSafeChildPath(
            string root,
            string relativePath)
        {
            var normalized =
                (relativePath
                    ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .TrimStart('/');

            if (
                string.IsNullOrWhiteSpace(
                    normalized)
                ||
                Path.IsPathRooted(
                    normalized)
                ||
                normalized
                    .Split('/')
                    .Any(
                        x =>
                            x == ".."
                            ||
                            x == "."
                            ||
                            string.IsNullOrWhiteSpace(
                                x)
                    )
            )
            {
                throw new InvalidDataException(
                    "更新路径无效"
                );
            }

            var rootFull =
                Path.GetFullPath(
                    root
                );

            var target =
                Path.GetFullPath(
                    Path.Combine(
                        rootFull,
                        normalized.Replace(
                            '/',
                            Path.DirectorySeparatorChar
                        )
                    )
                );

            var prefix =
                rootFull
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar
                    )
                +
                Path.DirectorySeparatorChar;

            if (
                !target.StartsWith(
                    prefix,
                    StringComparison
                        .OrdinalIgnoreCase
                )
            )
            {
                throw new InvalidDataException(
                    "更新路径越界"
                );
            }

            return target;
        }


        private static async Task<string>
            ComputeSha256Async(
                string path,
                CancellationToken cancellationToken)
        {
            await using var stream =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    1024 * 1024,
                    useAsync: true
                );

            using var sha256 =
                SHA256.Create();

            var hash =
                await sha256
                    .ComputeHashAsync(
                        stream,
                        cancellationToken
                    );

            return Convert
                .ToHexString(hash);
        }


        private static void
            TryDeleteDirectory(
                string path)
        {
            try
            {
                if (
                    Directory.Exists(path)
                )
                {
                    Directory.Delete(
                        path,
                        recursive: true
                    );
                }
            }
            catch
            {
                // 临时目录清理失败不覆盖主异常。
            }
        }


        private static void Log(
            string message)
        {
            var line =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

            Console.WriteLine(
                line
            );

            try
            {
                var logPath =
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "updater.log"
                    );

                File.AppendAllText(
                    logPath,
                    line
                    +
                    Environment.NewLine
                );
            }
            catch
            {
                // 日志不能影响更新流程。
            }
        }
    }
}
