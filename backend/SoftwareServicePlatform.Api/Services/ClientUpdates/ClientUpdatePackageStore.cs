using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using SoftwareServicePlatform.Api.Models;
using System.Text;
namespace SoftwareServicePlatform.Api.Services.ClientUpdates
{
    /// <summary>
    /// 客户端文件级增量更新包存储。
    ///
    /// 管理员 / 开发人员上传的是“目标版本完整目录 ZIP”。
    /// 服务端会：
    ///
    /// 1. 安全解压；
    /// 2. 读取 .update-ignore.txt；
    /// 3. 读取 .update-delete.txt；
    /// 4. 计算每个文件 SHA256；
    /// 5. 生成 manifest.json；
    /// 6. 保存目标版本完整文件状态。
    ///
    /// 客户端升级时比较本地文件 SHA256，
    /// 只下载真正变化的文件。
    /// </summary>
    public sealed class ClientUpdatePackageStore
    {

        /*
 * ==========================================
 * 更新 ZIP 安全限制
 * ==========================================
 *
 * 防止异常 ZIP / ZIP Bomb：
 *
 * 1. 文件数量过多；
 * 2. 单个文件解压后异常巨大；
 * 3. 整个 ZIP 解压后占满服务器磁盘。
 */

        private const int
            MaxZipFileCount =
                20000;


        /*
         * 单个解压文件最大 4 GB。
         */
        private const long
            MaxSingleFileSize =
                4L
                * 1024
                * 1024
                * 1024;


        /*
         * 一个更新包解压后总大小最大 20 GB。
         */
        private const long
            MaxTotalExtractedSize =
                20L
                * 1024
                * 1024
                * 1024;

        private static readonly JsonSerializerOptions
            JsonOptions =
                new()
                {
                    PropertyNamingPolicy =
                        JsonNamingPolicy.CamelCase,

                    WriteIndented =
                        true
                };


        private readonly string
            _storageRoot;


        public ClientUpdatePackageStore(
            string contentRootPath)
        {
            _storageRoot =
                Path.Combine(
                    contentRootPath,
                    "storage",
                    "client-updates"
                );
        }


        public string GetVersionRoot(
            int versionId)
        {
            return Path.Combine(
                _storageRoot,
                versionId.ToString()
            );
        }


        public string GetManifestPath(
            int versionId)
        {
            return Path.Combine(
                GetVersionRoot(versionId),
                "manifest.json"
            );
        }


        public string GetFilesRoot(
            int versionId)
        {
            return Path.Combine(
                GetVersionRoot(versionId),
                "files"
            );
        }


        public bool HasPackage(
            int versionId)
        {
            return File.Exists(
                GetManifestPath(
                    versionId
                )
            );
        }


        public ClientUpdateManifest?
            LoadManifest(
                int versionId)
        {
            var path =
                GetManifestPath(
                    versionId
                );

            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var json =
                    File.ReadAllText(
                        path
                    );

                return JsonSerializer
                    .Deserialize<
                        ClientUpdateManifest>(
                            json,
                            JsonOptions
                        );
            }
            catch
            {
                /*
                 * 清单损坏时不要伪装成有效更新包。
                 * 后续上传一个新的更新 ZIP 即可修复。
                 */
                return null;
            }
        }


        /// <summary>
        /// 保存目标版本完整目录 ZIP，
        /// 并生成文件级增量更新清单。
        /// </summary>
        public async Task<
            ClientUpdateManifest>
            SavePackageAsync(
                SoftwareVersion version,
                IFormFile zipFile,
                CancellationToken cancellationToken =
                    default)
        {
            if (
                zipFile == null
                ||
                zipFile.Length <= 0
            )
            {
                throw new InvalidDataException(
                    "请选择更新 ZIP 文件"
                );
            }

            var extension =
                Path.GetExtension(
                    zipFile.FileName
                );

            if (
                !string.Equals(
                    extension,
                    ".zip",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new InvalidDataException(
                    "增量更新包必须是 ZIP 文件"
                );
            }

            Directory.CreateDirectory(
                _storageRoot
            );

            var workingRoot =
                Path.Combine(
                    _storageRoot,
                    ".work-" +
                    version.Id +
                    "-" +
                    Guid.NewGuid()
                        .ToString("N")
                );

            var zipPath =
                Path.Combine(
                    workingRoot,
                    "package.zip"
                );

            var extractRoot =
                Path.Combine(
                    workingRoot,
                    "extract"
                );

            var stagingRoot =
                Path.Combine(
                    _storageRoot,
                    ".staging-" +
                    version.Id +
                    "-" +
                    Guid.NewGuid()
                        .ToString("N")
                );

            try
            {
                Directory.CreateDirectory(
                    workingRoot
                );

                Directory.CreateDirectory(
                    extractRoot
                );

                await using (
                    var input =
                        zipFile.OpenReadStream()
                )
                await using (
                    var output =
                        new FileStream(
                            zipPath,
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

                SafeExtractZip(
                    zipPath,
                    extractRoot
                );

                /*
                 * 兼容两种压缩方式：
                 *
                 * 1. 直接压缩目录中的文件；
                 * 2. 把整个发布目录作为 ZIP 的唯一一级文件夹。
                 */
                var contentRoot =
                    ResolveContentRoot(
                        extractRoot
                    );

                var ignorePatterns =
                    LoadIgnorePatterns(
                        contentRoot
                    );

                var deletePaths =
                    LoadDeletePaths(
                        contentRoot
                    );

                var stagingFilesRoot =
                    Path.Combine(
                        stagingRoot,
                        "files"
                    );

                Directory.CreateDirectory(
                    stagingFilesRoot
                );

                var manifest =
                    new ClientUpdateManifest
                    {
                        SoftwareId =
                            version.SoftwareId,

                        VersionId =
                            version.Id,

                        Version =
                            version.Version,

                        GeneratedAt =
                            DateTime.UtcNow,

                        DeletePaths =
                            deletePaths
                    };

                var sourceFiles =
                    Directory
                        .GetFiles(
                            contentRoot,
                            "*",
                            SearchOption
                                .AllDirectories
                        )
                        .OrderBy(
                            x => x,
                            StringComparer
                                .OrdinalIgnoreCase
                        )
                        .ToList();

                foreach (
                    var sourcePath
                    in sourceFiles)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    var relativePath =
                        NormalizeRelativePath(
                            Path.GetRelativePath(
                                contentRoot,
                                sourcePath
                            )
                        );

                    if (
                        string.IsNullOrWhiteSpace(
                            relativePath)
                        ||
                        ShouldIgnore(
                            relativePath,
                            ignorePatterns)
                        ||
                        IsProtectedPath(
                            relativePath)
                    )
                    {
                        continue;
                    }

                    var destinationPath =
                        GetSafeCombinedPath(
                            stagingFilesRoot,
                            relativePath
                        );

                    Directory.CreateDirectory(
                        Path.GetDirectoryName(
                            destinationPath
                        )!
                    );

                    File.Copy(
                        sourcePath,
                        destinationPath,
                        overwrite: true
                    );

                    var sha256 =
                        await ComputeSha256Async(
                            destinationPath,
                            cancellationToken
                        );

                    var info =
                        new FileInfo(
                            destinationPath
                        );

                    manifest.Files.Add(
                        new ClientUpdateManifestFile
                        {
                            Path =
                                relativePath,

                            Size =
                                info.Length,

                            Sha256 =
                                sha256
                        }
                    );

                    manifest.TotalFileSize +=
                        info.Length;
                }

                if (
                    manifest.Files.Count == 0
                )
                {
                    throw new InvalidDataException(
                        "更新 ZIP 中没有可发布文件"
                    );
                }

                /*
                 * 清单里的路径不允许大小写重复。
                 *
                 * Windows 默认大小写不敏感，
                 * 如果同时出现：
                 *
                 * A.dll
                 * a.dll
                 *
                 * 客户端替换会产生歧义。
                 */
                var duplicatePath =
                    manifest.Files
                        .GroupBy(
                            x => x.Path,
                            StringComparer
                                .OrdinalIgnoreCase
                        )
                        .FirstOrDefault(
                            x => x.Count() > 1
                        );

                if (duplicatePath != null)
                {
                    throw new InvalidDataException(
                        $"更新包存在重复文件路径：{duplicatePath.Key}"
                    );
                }

                manifest.Files =
                    manifest.Files
                        .OrderBy(
                            x => x.Path,
                            StringComparer
                                .OrdinalIgnoreCase
                        )
                        .ToList();

                /*
                 * 删除清单不能和目标版本文件清单冲突。
                 *
                 * 否则同一个路径会出现：
                 *
                 * 先更新新文件
                 * 又要求删除
                 *
                 * 语义不明确，直接拒绝上传。
                 */
                var targetPathSet =
                    manifest.Files
                        .Select(x => x.Path)
                        .ToHashSet(
                            StringComparer.OrdinalIgnoreCase
                        );

                var conflictDeletePath =
                    manifest.DeletePaths
                        .FirstOrDefault(
                            x => targetPathSet.Contains(x)
                        );

                if (conflictDeletePath != null)
                {
                    throw new InvalidDataException(
                        $".update-delete.txt 与目标文件冲突：{conflictDeletePath}"
                    );
                }

                var manifestJson =
                    JsonSerializer.Serialize(
                        manifest,
                        JsonOptions
                    );

                await File.WriteAllTextAsync(
                    Path.Combine(
                        stagingRoot,
                        "manifest.json"
                    ),
                    manifestJson,
                    cancellationToken
                );

                /*
                 * 全部准备完成以后才替换正式目录。
                 *
                 * 这样 ZIP 解压 / SHA256 计算中途失败，
                 * 不会破坏已经存在的更新包。
                 */
                var finalRoot =
                    GetVersionRoot(
                        version.Id
                    );

                var oldBackup =
                    Path.Combine(
                        _storageRoot,
                        ".old-" +
                        version.Id +
                        "-" +
                        Guid.NewGuid()
                            .ToString("N")
                    );

                if (
                    Directory.Exists(
                        finalRoot)
                )
                {
                    Directory.Move(
                        finalRoot,
                        oldBackup
                    );
                }

                try
                {
                    Directory.Move(
                        stagingRoot,
                        finalRoot
                    );

                    if (
                        Directory.Exists(
                            oldBackup)
                    )
                    {
                        Directory.Delete(
                            oldBackup,
                            recursive: true
                        );
                    }
                }
                catch
                {
                    if (
                        !Directory.Exists(
                            finalRoot)
                        &&
                        Directory.Exists(
                            oldBackup)
                    )
                    {
                        Directory.Move(
                            oldBackup,
                            finalRoot
                        );
                    }

                    throw;
                }

                return manifest;
            }
            finally
            {
                TryDeleteDirectory(
                    workingRoot
                );

                TryDeleteDirectory(
                    stagingRoot
                );
            }
        }


        public void DeletePackage(
            int versionId)
        {
            var path =
                GetVersionRoot(
                    versionId
                );

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


        /// <summary>
        /// 根据 manifest 中已经确认过的相对路径，
        /// 获取服务端实际文件路径。
        /// </summary>
        public string GetPublishedFilePath(
            int versionId,
            string relativePath)
        {
            return GetSafeCombinedPath(
                GetFilesRoot(versionId),
                NormalizeRelativePath(
                    relativePath
                )
            );
        }


        public static string
            NormalizeRelativePath(
                string path)
        {
            return (path ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .TrimStart('/');
        }


        /// <summary>
        /// 防止 ../../xxx 一类 Zip Slip / Path Traversal。
        /// </summary>
        public static string
            GetSafeCombinedPath(
                string root,
                string relativePath)
        {
            var normalized =
                NormalizeRelativePath(
                    relativePath
                );

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
                    "更新文件路径无效"
                );
            }

            var rootFullPath =
                Path.GetFullPath(
                    root
                );

            var targetFullPath =
                Path.GetFullPath(
                    Path.Combine(
                        rootFullPath,
                        normalized.Replace(
                            '/',
                            Path.DirectorySeparatorChar
                        )
                    )
                );

            var rootPrefix =
                rootFullPath
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                )
                +
                Path.DirectorySeparatorChar;

            if (
                !targetFullPath
                    .StartsWith(
                        rootPrefix,
                        StringComparison
                            .OrdinalIgnoreCase
                    )
            )
            {
                throw new InvalidDataException(
                    "更新文件路径越界"
                );
            }

            return targetFullPath;
        }


        private static void SafeExtractZip(
     string zipPath,
     string extractRoot)
        {
            /*
             * 保留 CP936 支持。
             *
             * 主要兼容 Bandizip / Windows 环境中
             * 一部分中文 ZIP 文件名。
             */
            Encoding.RegisterProvider(
                CodePagesEncodingProvider.Instance
            );

            var zipEntryEncoding =
                Encoding.GetEncoding(936);


            using var zipStream =
                new FileStream(
                    zipPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                );


            using var archive =
                new ZipArchive(
                    zipStream,
                    ZipArchiveMode.Read,
                    leaveOpen: false,
                    entryNameEncoding:
                        zipEntryEncoding
                );


            /*
             * ==========================================
             * 第一阶段：
             * 解压前先检查 ZIP 清单
             * ==========================================
             */

            var fileCount =
                0;

            long declaredTotalSize =
                0;


            foreach (
                var entry
                in archive.Entries)
            {
                var rawPath =
                    entry.FullName
                        .Replace('\\', '/');


                /*
                 * 目录 Entry 不计文件数量和大小。
                 */
                if (
                    rawPath.EndsWith('/')
                )
                {
                    continue;
                }


                fileCount++;


                /*
                 * 文件数量限制。
                 */
                if (
                    fileCount
                    >
                    MaxZipFileCount
                )
                {
                    throw new InvalidDataException(
                        $"更新 ZIP 文件数量过多。"
                        +
                        $"最大允许 {MaxZipFileCount} 个文件。"
                    );
                }


                /*
                 * 单文件声明大小限制。
                 */
                if (
                    entry.Length
                    >
                    MaxSingleFileSize
                )
                {
                    throw new InvalidDataException(
                        "更新 ZIP 中存在超大文件。\r\n"
                        +
                        $"文件：{entry.FullName}\r\n"
                        +
                        $"文件大小：{FormatBytes(entry.Length)}\r\n"
                        +
                        $"单文件最大允许：{FormatBytes(MaxSingleFileSize)}"
                    );
                }


                /*
                 * 防止 long 溢出。
                 */
                try
                {
                    declaredTotalSize =
                        checked(
                            declaredTotalSize
                            +
                            entry.Length
                        );
                }
                catch (
                    OverflowException)
                {
                    throw new InvalidDataException(
                        "更新 ZIP 解压大小异常"
                    );
                }


                /*
                 * 总解压大小限制。
                 */
                if (
                    declaredTotalSize
                    >
                    MaxTotalExtractedSize
                )
                {
                    throw new InvalidDataException(
                        "更新 ZIP 解压后总大小过大。\r\n"
                        +
                        $"当前声明大小：{FormatBytes(declaredTotalSize)}\r\n"
                        +
                        $"最大允许：{FormatBytes(MaxTotalExtractedSize)}"
                    );
                }
            }


            /*
             * ==========================================
             * 第二阶段：
             * 真正开始安全解压
             * ==========================================
             *
             * 不能只相信 ZIP Header 中声明的 Length。
             *
             * 解压过程中还要按照实际写入字节数
             * 再做一次限制。
             */

            long actualTotalSize =
                0;


            /*
             * 记录已经成功解压的目标文件。
             *
             * Windows 路径默认大小写不敏感，
             * 用于发现：
             *
             * A.dll
             * a.dll
             *
             * 等冲突。
             */
            var extractedEntries =
                new Dictionary<
                    string,
                    string>(
                        StringComparer
                            .OrdinalIgnoreCase
                    );


            foreach (
                var entry
                in archive.Entries)
            {
                var rawPath =
                    entry.FullName
                        .Replace('\\', '/');


                /*
                 * ZIP 目录项。
                 */
                if (
                    rawPath.EndsWith('/')
                )
                {
                    var directoryRelative =
                        rawPath
                            .TrimEnd('/');


                    if (
                        string.IsNullOrWhiteSpace(
                            directoryRelative)
                    )
                    {
                        continue;
                    }


                    var directoryPath =
                        GetSafeCombinedPath(
                            extractRoot,
                            directoryRelative
                        );


                    Directory.CreateDirectory(
                        directoryPath
                    );


                    continue;
                }


                var relativePath =
                    NormalizeRelativePath(
                        rawPath
                    );


                var destinationPath =
                    GetSafeCombinedPath(
                        extractRoot,
                        relativePath
                    );


                Directory.CreateDirectory(
                    Path.GetDirectoryName(
                        destinationPath
                    )!
                );


                /*
                 * ZIP 内不能有多个 Entry
                 * 最终落到同一个 Windows 文件路径。
                 */
                if (
                    File.Exists(
                        destinationPath)
                    ||
                    extractedEntries
                        .ContainsKey(
                            destinationPath)
                )
                {
                    extractedEntries
                        .TryGetValue(
                            destinationPath,
                            out var previousEntry
                        );


                    throw new InvalidDataException(
                        "ZIP 解压目标路径发生冲突。\r\n"
                        +
                        $"当前 Entry：{entry.FullName}\r\n"
                        +
                        $"当前归一化路径：{relativePath}\r\n"
                        +
                        $"目标文件：{destinationPath}\r\n"
                        +
                        $"此前 Entry："
                        +
                        (
                            previousEntry
                            ??
                            "未记录（可能是 Windows 路径映射冲突）"
                        )
                    );
                }


                try
                {
                    using var input =
                        entry.Open();


                    using var output =
                        new FileStream(
                            destinationPath,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None
                        );


                    var buffer =
                        new byte[
                            1024 * 1024
                        ];


                    long currentFileSize =
                        0;


                    while (true)
                    {
                        var read =
                            input.Read(
                                buffer,
                                0,
                                buffer.Length
                            );


                        if (
                            read <= 0
                        )
                        {
                            break;
                        }


                        /*
                         * 当前单文件实际解压大小。
                         */
                        currentFileSize =
                            checked(
                                currentFileSize
                                +
                                read
                            );


                        /*
                         * ZIP 整体实际解压大小。
                         */
                        actualTotalSize =
                            checked(
                                actualTotalSize
                                +
                                read
                            );


                        if (
                            currentFileSize
                            >
                            MaxSingleFileSize
                        )
                        {
                            throw new InvalidDataException(
                                "ZIP 中单个文件实际解压大小超过限制。\r\n"
                                +
                                $"文件：{entry.FullName}\r\n"
                                +
                                $"最大允许：{FormatBytes(MaxSingleFileSize)}"
                            );
                        }


                        if (
                            actualTotalSize
                            >
                            MaxTotalExtractedSize
                        )
                        {
                            throw new InvalidDataException(
                                "ZIP 实际解压总大小超过限制。\r\n"
                                +
                                $"最大允许：{FormatBytes(MaxTotalExtractedSize)}"
                            );
                        }


                        output.Write(
                            buffer,
                            0,
                            read
                        );
                    }


                    extractedEntries[
                        destinationPath
                    ] =
                        entry.FullName;
                }
                catch (
                    Exception ex)
                {
                    /*
                     * 当前文件可能只写了一部分。
                     *
                     * 尽量删除，
                     * 最终 workingRoot 也会统一清理。
                     */
                    try
                    {
                        if (
                            File.Exists(
                                destinationPath)
                        )
                        {
                            File.Delete(
                                destinationPath
                            );
                        }
                    }
                    catch
                    {
                        // 不覆盖真正异常。
                    }


                    if (
                        ex
                        is InvalidDataException
                    )
                    {
                        throw;
                    }


                    throw new InvalidDataException(
                        "ZIP 文件解压失败。\r\n"
                        +
                        $"Entry：{entry.FullName}\r\n"
                        +
                        $"归一化路径：{relativePath}\r\n"
                        +
                        $"目标文件：{destinationPath}\r\n"
                        +
                        $"原因：{ex.Message}",
                        ex
                    );
                }
            }
        }

        private static string FormatBytes(
    long bytes)
        {
            const double kb =
                1024;

            const double mb =
                kb * 1024;

            const double gb =
                mb * 1024;


            if (
                bytes >= gb
            )
            {
                return
                    $"{bytes / gb:F2} GB";
            }


            if (
                bytes >= mb
            )
            {
                return
                    $"{bytes / mb:F2} MB";
            }


            if (
                bytes >= kb
            )
            {
                return
                    $"{bytes / kb:F2} KB";
            }


            return
                $"{bytes} B";
        }
        private static string ResolveContentRoot(
            string extractRoot)
        {
            var entries =
                Directory
                    .GetFileSystemEntries(
                        extractRoot
                    );

            if (
                entries.Length == 1
                &&
                Directory.Exists(
                    entries[0])
            )
            {
                return entries[0];
            }

            return extractRoot;
        }


        private static List<string>
            LoadIgnorePatterns(
                string contentRoot)
        {
            var patterns =
                new List<string>
                {
                    /*
                     * 更新器自身独立放在 updater/ 目录，
                     * 第一版不做“更新器自更新”，
                     * 避免正在运行的 EXE 被覆盖。
                     */
                    "updater/**",

                    /*
                     * 客户端运行时产生的临时/回滚目录。
                     */
                    ".update-temp/**",
                    ".update-backup/**",

                    /*
                     * 版本号由 Updater 成功升级后写入，
                     * 不从 ZIP 直接覆盖。
                     */
                    "version.txt",

                    /*
                     * 两个控制文件本身不发布到客户端。
                     */
                    ".update-ignore.txt",
                    ".update-delete.txt"
                };

            var ignoreFile =
                Path.Combine(
                    contentRoot,
                    ".update-ignore.txt"
                );

            if (
                File.Exists(
                    ignoreFile)
            )
            {
                foreach (
                    var rawLine
                    in File.ReadAllLines(
                        ignoreFile)
                )
                {
                    var line =
                        rawLine.Trim();

                    if (
                        string.IsNullOrWhiteSpace(
                            line)
                        ||
                        line.StartsWith('#')
                    )
                    {
                        continue;
                    }

                    patterns.Add(
                        NormalizeRelativePath(
                            line
                        )
                    );
                }
            }

            return patterns;
        }


        private static List<string>
            LoadDeletePaths(
                string contentRoot)
        {
            var deleteFile =
                Path.Combine(
                    contentRoot,
                    ".update-delete.txt"
                );

            if (
                !File.Exists(
                    deleteFile)
            )
            {
                return new List<string>();
            }

            var result =
                new List<string>();

            foreach (
                var rawLine
                in File.ReadAllLines(
                    deleteFile)
            )
            {
                var line =
                    rawLine.Trim();

                if (
                    string.IsNullOrWhiteSpace(
                        line)
                    ||
                    line.StartsWith('#')
                )
                {
                    continue;
                }

                var normalized =
                    NormalizeRelativePath(
                        line
                    );

                /*
                 * 用统一的安全路径校验，
                 * 只不过这里不真正使用返回值。
                 */
                _ =
                    GetSafeCombinedPath(
                        contentRoot,
                        normalized
                    );

                if (
                    IsProtectedPath(
                        normalized)
                )
                {
                    throw new InvalidDataException(
                        $".update-delete.txt 不允许删除受保护路径：{normalized}"
                    );
                }

                result.Add(
                    normalized
                );
            }

            return result
                .Distinct(
                    StringComparer
                        .OrdinalIgnoreCase
                )
                .OrderBy(
                    x => x,
                    StringComparer
                        .OrdinalIgnoreCase
                )
                .ToList();
        }


        private static bool IsProtectedPath(
            string relativePath)
        {
            var path =
                NormalizeRelativePath(
                    relativePath
                );

            return string.Equals(
                       path,
                       "version.txt",
                       StringComparison
                           .OrdinalIgnoreCase
                   )
                   ||
                   path.StartsWith(
                       "updater/",
                       StringComparison
                           .OrdinalIgnoreCase
                   )
                   ||
                   path.StartsWith(
                       ".update-temp/",
                       StringComparison
                           .OrdinalIgnoreCase
                   )
                   ||
                   path.StartsWith(
                       ".update-backup/",
                       StringComparison
                           .OrdinalIgnoreCase
                   );
        }


        private static bool ShouldIgnore(
            string relativePath,
            IEnumerable<string>
                patterns)
        {
            foreach (
                var pattern
                in patterns)
            {
                if (
                    MatchGlob(
                        relativePath,
                        pattern)
                )
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// 简单 Glob：
        ///
        /// *  = 当前目录任意字符
        /// ** = 可跨目录
        /// ?  = 单个字符
        /// </summary>
        private static bool MatchGlob(
            string path,
            string pattern)
        {
            var normalizedPath =
                NormalizeRelativePath(
                    path
                );

            var normalizedPattern =
                NormalizeRelativePath(
                    pattern
                );

            var regexText =
                "^"
                +
                Regex
                    .Escape(
                        normalizedPattern
                    )
                    .Replace(
                        @"\*\*",
                        ".*"
                    )
                    .Replace(
                        @"\*",
                        "[^/]*"
                    )
                    .Replace(
                        @"\?",
                        "[^/]"
                    )
                +
                "$";

            return Regex.IsMatch(
                normalizedPath,
                regexText,
                RegexOptions
                    .IgnoreCase
                |
                RegexOptions
                    .CultureInvariant
            );
        }


        private static async Task<string>
            ComputeSha256Async(
                string filePath,
                CancellationToken cancellationToken)
        {
            await using var stream =
                new FileStream(
                    filePath,
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


        private static void TryDeleteDirectory(
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
                /*
                 * 临时目录清理失败不覆盖原始异常。
                 * 后续可以由运维脚本清理 .work-* / .staging-*。
                 */
            }
        }
    }
}
