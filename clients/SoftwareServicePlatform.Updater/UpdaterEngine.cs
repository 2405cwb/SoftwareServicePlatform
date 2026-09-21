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
    /// 7. 增量失败可回退到完整安装包；
    /// 8. 自动更新过程尽力上报到平台下载记录；
    /// 9. 通过 IProgress 向 GUI 上报检查、下载、安装进度。
    /// </summary>
    public sealed class UpdaterEngine
    {
        private readonly UpdaterConfig
            _config;

        private readonly string
            _appRoot;

        private readonly HttpClient
            _httpClient;

        /*
         * GUI 进度回调。
         *
         * UpdaterEngine 本身不依赖具体窗口，
         * 只负责把“当前进行到哪一步”通过 IProgress 上报出去。
         *
         * 这样以后 WinForms / WPF / 其它界面都可以复用同一套更新核心。
         */
        private readonly IProgress<UpdaterProgress>?
            _progress;


        public UpdaterEngine(
            UpdaterConfig config,
            string appRoot,
            IProgress<UpdaterProgress>? progress = null)
        {
            _config =
                config;

            _appRoot =
                Path.GetFullPath(
                    appRoot
                );

            _progress =
                progress;

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
        /// 尽力向平台创建一条自动更新记录。
        /// 记录接口失败不能影响真正的软件更新。
        /// </summary>
        private async Task<int?>
            TryStartUpdateReportAsync(
                int versionId,
                string fromVersion,
                string downloadType,
                int fileCount,
                long fileSize)
        {
            try
            {
                using var timeout =
                    new CancellationTokenSource(
                        TimeSpan.FromSeconds(5)
                    );

                var response =
                    await _httpClient
                        .PostAsJsonAsync(
                            "api/client-updates/report/start",
                            new ClientUpdateReportStartRequest
                            {
                                VersionId =
                                    versionId,

                                FromVersion =
                                    fromVersion,

                                DownloadType =
                                    downloadType,

                                FileCount =
                                    fileCount,

                                FileSize =
                                    fileSize
                            },
                            timeout.Token
                        );

                if (!response.IsSuccessStatusCode)
                {
                    Log(
                        $"自动更新记录开始上报失败：HTTP {(int)response.StatusCode}"
                    );

                    return null;
                }

                var result =
                    await response.Content
                        .ReadFromJsonAsync<
                            ClientUpdateReportStartResponse>(
                                cancellationToken:
                                    timeout.Token
                            );

                if (
                    result == null
                    ||
                    result.RecordId <= 0
                )
                {
                    Log(
                        "自动更新记录开始上报返回无效 RecordId。"
                    );

                    return null;
                }

                return result.RecordId;
            }
            catch (Exception ex)
            {
                Log(
                    "自动更新记录开始上报失败："
                    + ex.Message
                );

                return null;
            }
        }


        /// <summary>
        /// 尽力向平台完成一条自动更新记录。
        /// 上报失败不能把已经成功的软件更新判为失败。
        /// </summary>
        private async Task
            TryCompleteUpdateReportAsync(
                int? recordId,
                bool success,
                string errorMessage)
        {
            if (!recordId.HasValue)
            {
                return;
            }

            try
            {
                using var timeout =
                    new CancellationTokenSource(
                        TimeSpan.FromSeconds(5)
                    );

                var response =
                    await _httpClient
                        .PostAsJsonAsync(
                            "api/client-updates/report/complete",
                            new ClientUpdateReportCompleteRequest
                            {
                                RecordId =
                                    recordId.Value,

                                Success =
                                    success,

                                ErrorMessage =
                                    errorMessage
                            },
                            timeout.Token
                        );

                if (!response.IsSuccessStatusCode)
                {
                    Log(
                        $"自动更新记录完成上报失败：HTTP {(int)response.StatusCode}"
                    );
                }
            }
            catch (Exception ex)
            {
                Log(
                    "自动更新记录完成上报失败："
                    + ex.Message
                );
            }
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
             *
             * 这里会逐个比较目标清单和本地文件。
             *
             * 例如目标版本一共有 1309 个文件，
             * 实际只有 14 个文件发生变化，
             * 最终只会把这 14 个文件加入 changedFiles。
             */
            ReportProgress(
                "正在检查本地文件...",
                0,
                manifest.Files.Count
            );

            for (
                var fileIndex = 0;
                fileIndex < manifest.Files.Count;
                fileIndex++
            )
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var file =
                    manifest.Files[
                        fileIndex
                    ];

                /*
                 * 把当前正在校验的文件展示到 GUI。
                 */
                ReportProgress(
                    "正在检查本地文件...",
                    fileIndex + 1,
                    manifest.Files.Count,
                    file.Path
                );

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
             * 自动更新记录按“一次更新”记录，
             * 不按单个 DLL 记录。
             */
            var actualDownloadSize =
                changedFiles.Sum(
                    x => x.Size
                );

            var reportId =
                await TryStartUpdateReportAsync(
                    versionId,
                    update.CurrentVersion,
                    "AutoIncremental",
                    changedFiles.Count,
                    actualDownloadSize
                );


            /*
             * 最外层 try：
             *
             * 1. 覆盖下载阶段异常；
             * 2. 覆盖替换/校验阶段异常；
             * 3. 统一上报 Success / Failed；
             * 4. 无论成功失败都清理 .update-temp。
             */
            try
            {
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

                    /*
                     * current 使用 i，
                     * 表示“已有 i 个文件完整下载成功，
                     * 当前正在下载第 i + 1 个”。
                     */
                    ReportProgress(
                        "正在下载更新文件...",
                        i,
                        changedFiles.Count,
                        file.Path
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

                    /*
                     * 文件完整下载并通过 SHA256 后，
                     * 才把进度推进到 i + 1。
                     */
                    ReportProgress(
                        "正在下载更新文件...",
                        i + 1,
                        changedFiles.Count,
                        file.Path
                    );
                }


                /*
                 * ==========================================
                 * 3. 真正替换前等待主程序退出
                 * ==========================================
                 */
                ReportProgress(
                    "正在等待主程序退出...",
                    indeterminate: true
                );

                await WaitForProcessExitAsync(
                    waitProcessId,
                    cancellationToken
                );


                /*
                 * ==========================================
                 * 4. 备份 + 替换 + 删除
                 * ==========================================
                 */
                ReportProgress(
                    "正在安装更新...",
                    0,
                    changedFiles.Count
                );

                Directory.CreateDirectory(
                    backupRoot
                );

                var newlyCreated =
                    new List<string>();

                var backedUp =
                    new List<string>();

                /*
                 * 内层 try：
                 * 专门负责文件替换事务和失败回滚。
                 */
                try
                {
                    for (
                        var installIndex = 0;
                        installIndex < changedFiles.Count;
                        installIndex++
                    )
                    {
                        var file =
                            changedFiles[
                                installIndex
                            ];

                        ReportProgress(
                            "正在安装更新...",
                            installIndex,
                            changedFiles.Count,
                            file.Path
                        );

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

                        /*
                         * 当前文件替换成功后推进安装进度。
                         */
                        ReportProgress(
                            "正在安装更新...",
                            installIndex + 1,
                            changedFiles.Count,
                            file.Path
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
                     *
                     * version.txt 同样属于更新事务的一部分。
                     *
                     * 如果写版本号失败，
                     * catch 中必须能够恢复原来的版本文件。
                     */
                    var versionRelativePath =
                        _config.VersionFile;

                    var versionPath =
                        GetSafeAppPath(
                            versionRelativePath
                        );

                    /*
                     * 如果 version.txt 还没有在前面的文件替换过程中
                     * 被加入备份列表，这里单独处理。
                     *
                     * 默认情况下 version.txt 不会进入 manifest，
                     * 因为服务端已经把它设置为受保护文件。
                     */
                    var versionAlreadyTracked =
                        backedUp.Any(
                            x =>
                                string.Equals(
                                    x,
                                    versionRelativePath,
                                    StringComparison
                                        .OrdinalIgnoreCase
                                )
                        )
                        ||
                        newlyCreated.Any(
                            x =>
                                string.Equals(
                                    x,
                                    versionRelativePath,
                                    StringComparison
                                        .OrdinalIgnoreCase
                                )
                        );

                    if (!versionAlreadyTracked)
                    {
                        if (File.Exists(versionPath))
                        {
                            var versionBackupPath =
                                GetSafeChildPath(
                                    backupRoot,
                                    versionRelativePath
                                );

                            Directory.CreateDirectory(
                                Path.GetDirectoryName(
                                    versionBackupPath
                                )!
                            );

                            File.Copy(
                                versionPath,
                                versionBackupPath,
                                overwrite: true
                            );

                            backedUp.Add(
                                versionRelativePath
                            );
                        }
                        else
                        {
                            /*
                             * 原来不存在 version.txt。
                             *
                             * 如果后面失败，
                             * rollback 会把新创建的 version.txt 删除。
                             */
                            newlyCreated.Add(
                                versionRelativePath
                            );
                        }
                    }

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

                    /*
                     * GUI 显示最终完成状态。
                     */
                    ReportProgress(
                        $"更新完成：{update.CurrentVersion} → {update.LatestVersion}",
                        1,
                        1
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


                await TryCompleteUpdateReportAsync(
                    reportId,
                    true,
                    string.Empty
                );
            }
            catch (Exception ex)
            {
                await TryCompleteUpdateReportAsync(
                    reportId,
                    false,
                    ex.Message
                );

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

            var reportId =
                await TryStartUpdateReportAsync(
                    update.VersionId.Value,
                    update.CurrentVersion,
                    "AutoFullPackage",
                    1,
                    update.FullPackageFileSize
                );

            try
            {
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

                /*
                 * 完整安装包目前使用流式 CopyToAsync，
                 * 暂时不计算字节级百分比，
                 * GUI 使用 Marquee 表示正在进行。
                 */
                ReportProgress(
                    "正在下载完整安装包...",
                    currentFile: fileName,
                    indeterminate: true
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

                ReportProgress(
                    "下载完成，正在启动安装程序...",
                    currentFile: fileName,
                    indeterminate: true
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

                await TryCompleteUpdateReportAsync(
                    reportId,
                    true,
                    string.Empty
                );
            }
            catch (Exception ex)
            {
                await TryCompleteUpdateReportAsync(
                    reportId,
                    false,
                    ex.Message
                );

                throw;
            }
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


        /// <summary>
        /// 向外部 GUI 上报当前更新进度。
        ///
        /// UpdaterEngine 不直接操作任何控件，
        /// 因此核心更新逻辑仍然可以独立测试和复用。
        ///
        /// message:
        ///     当前阶段说明，例如“正在下载更新文件...”。
        ///
        /// current / total:
        ///     当前完成数量和总数量。
        ///
        /// currentFile:
        ///     当前处理文件，用于窗口中展示。
        ///
        /// indeterminate:
        ///     无法准确计算百分比时设为 true，
        ///     GUI 可使用 Marquee 进度条。
        /// </summary>
        private void ReportProgress(
            string message,
            int current = 0,
            int total = 0,
            string currentFile = "",
            bool indeterminate = false)
        {
            _progress?.Report(
                new UpdaterProgress
                {
                    Message =
                        message,

                    Current =
                        current,

                    Total =
                        total,

                    CurrentFile =
                        currentFile,

                    IsIndeterminate =
                        indeterminate
                }
            );
        }


        /// <summary>
        /// 更新器统一日志。
        ///
        /// 即使改成 WinExe 没有控制台，
        /// updater.log 仍会保留完整更新过程。
        /// </summary>
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
