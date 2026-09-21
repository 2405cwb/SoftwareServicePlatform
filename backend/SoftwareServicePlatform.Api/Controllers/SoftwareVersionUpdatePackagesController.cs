using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Services.ClientUpdates;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 软件版本“文件级增量更新包”管理。
    ///
    /// 管理员 / 开发人员上传目标版本完整目录 ZIP，
    /// 服务器自动生成 manifest.json。
    ///
    /// 发布后的版本不允许再修改更新包，
    /// 保证一个已经发布的版本内容不可变。
    /// </summary>
    [ApiController]
    [Route("api/software-version-update-packages")]
    [Authorize(Roles = "Admin,Developer")]
    public class SoftwareVersionUpdatePackagesController
        : ControllerBase
    {
        private readonly AppDbContext
            _dbContext;

        private readonly IWebHostEnvironment
            _environment;


        public SoftwareVersionUpdatePackagesController(
            AppDbContext dbContext,
            IWebHostEnvironment environment)
        {
            _dbContext =
                dbContext;

            _environment =
                environment;
        }


        /// <summary>
        /// 获取所有版本的更新包状态。
        ///
        /// GET /api/software-version-update-packages
        /// </summary>
        [HttpGet]
        public async Task<IActionResult>
            GetAll(
                CancellationToken cancellationToken)
        {
            var versions =
                await _dbContext
                    .SoftwareVersions
                    .AsNoTracking()
                    .Include(x => x.Software)
                    .OrderByDescending(
                        x => x.CreatedAt
                    )
                    .ToListAsync(
                        cancellationToken
                    );

            var store =
                new ClientUpdatePackageStore(
                    _environment.ContentRootPath
                );

            var result =
                versions.Select(
                    version =>
                    {
                        var manifest =
                            store.LoadManifest(
                                version.Id
                            );

                        /*
                         * 如果草稿版本在上传更新 ZIP 后
                         * 又修改了 SoftwareId / Version，
                         * 旧 manifest 不再属于当前版本。
                         *
                         * 这种情况下不把旧包当成有效更新包，
                         * 管理员重新上传一次即可。
                         */
                        var manifestValid =
                            manifest != null
                            &&
                            manifest.SoftwareId
                                == version.SoftwareId
                            &&
                            string.Equals(
                                manifest.Version,
                                version.Version,
                                StringComparison.OrdinalIgnoreCase
                            );

                        return new ClientUpdatePackageSummary
                        {
                            VersionId =
                                version.Id,

                            SoftwareId =
                                version.SoftwareId,

                            SoftwareName =
                                version.Software?.Name
                                ?? string.Empty,

                            SoftwareCode =
                                version.Software?.Code
                                ?? string.Empty,

                            Version =
    version.Version,

                            VersionType =
    version.VersionType,

                            PublishStatus =
    version.PublishStatus,

                            HasUpdatePackage =
                                manifestValid,

                            FileCount =
                                manifestValid
                                    ? manifest!.Files.Count
                                    : 0,

                            TotalFileSize =
                                manifestValid
                                    ? manifest!.TotalFileSize
                                    : 0,

                            DeleteCount =
                                manifestValid
                                    ? manifest!.DeletePaths.Count
                                    : 0,

                            GeneratedAt =
                                manifestValid
                                    ? manifest!.GeneratedAt
                                    : null,

                            FullPackageAvailable =
                                !string.IsNullOrWhiteSpace(
                                    version.PackageRelativePath),

                            FullPackageFileName =
                                version.PackageFileName,

                            FullPackageFileSize =
                                version.PackageFileSize
                        };
                    }
                )
                .ToList();

            return Ok(result);
        }


        /// <summary>
        /// 获取指定版本更新包的完整 manifest。
        ///
        /// GET /api/software-version-update-packages/{versionId}
        /// </summary>
        [HttpGet("{versionId:int}")]
        public async Task<IActionResult>
            GetOne(
                int versionId,
                CancellationToken cancellationToken)
        {
            var version =
                await _dbContext
                    .SoftwareVersions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id == versionId,
                        cancellationToken
                    );

            if (version == null)
            {
                return NotFound(
                    "软件版本不存在"
                );
            }

            var store =
                new ClientUpdatePackageStore(
                    _environment.ContentRootPath
                );

            var manifest =
                store.LoadManifest(
                    versionId
                );

            if (
                manifest == null
                ||
                manifest.SoftwareId
                    != version.SoftwareId
                ||
                !string.Equals(
                    manifest.Version,
                    version.Version,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return NotFound(
                    "当前版本尚未上传有效的增量更新 ZIP，或草稿版本信息在上传后发生了变化"
                );
            }

            return Ok(manifest);
        }


        /// <summary>
        /// 上传目标版本完整目录 ZIP。
        ///
        /// POST /api/software-version-update-packages/{versionId}
        ///
        /// multipart/form-data:
        /// file = xxx.zip
        /// </summary>
        [HttpPost("{versionId:int}")]
        [RequestSizeLimit(
            2L * 1024 * 1024 * 1024)]
        public async Task<IActionResult>
            Upload(
                int versionId,
                [FromForm] IFormFile file,
                CancellationToken cancellationToken)
        {
            var version =
                await _dbContext
                    .SoftwareVersions
                    .Include(x => x.Software)
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id == versionId,
                        cancellationToken
                    );

            if (version == null)
            {
                return NotFound(
                    "软件版本不存在"
                );
            }

            /*
             * 已发布版本的文件内容必须保持不变。
             *
             * 如果发布后还允许随意替换更新包，
             * 同一个 1.2.0 在不同时间可能下载到不同 DLL，
             * SHA256 / 回滚 / 问题追踪都会失去意义。
             */
            if (
                version.PublishStatus
                != "Draft"
            )
            {
                return BadRequest(
                    "只有草稿版本允许上传或更换自动更新包"
                );
            }

            if (
                version.VersionType
                == "Dev"
            )
            {
                /*
                 * Dev 是否上传更新包不是技术上做不到，
                 * 但 Dev 当前不能发布给客户，
                 * 没必要生成客户自动更新包。
                 */
                return BadRequest(
                    "Dev 版本不用于客户自动更新"
                );
            }

            var store =
                new ClientUpdatePackageStore(
                    _environment.ContentRootPath
                );

            try
            {
                var manifest =
                    await store.SavePackageAsync(
                        version,
                        file,
                        cancellationToken
                    );

                return Ok(
                    new
                    {
                        message =
                            "自动更新包生成成功",

                        versionId =
                            version.Id,

                        version =
                            version.Version,

                        fileCount =
                            manifest.Files.Count,

                        totalFileSize =
                            manifest.TotalFileSize,

                        deleteCount =
                            manifest.DeletePaths.Count,

                        generatedAt =
                            manifest.GeneratedAt
                    }
                );
            }
            catch (
                InvalidDataException ex)
            {
                return BadRequest(
                    ex.Message
                );
            }
        }


        /// <summary>
        /// 删除草稿版本的增量更新包。
        /// </summary>
        [HttpDelete("{versionId:int}")]
        public async Task<IActionResult>
            Delete(
                int versionId,
                CancellationToken cancellationToken)
        {
            var version =
                await _dbContext
                    .SoftwareVersions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id == versionId,
                        cancellationToken
                    );

            if (version == null)
            {
                return NotFound(
                    "软件版本不存在"
                );
            }

            if (
                version.PublishStatus
                != "Draft"
            )
            {
                return BadRequest(
                    "已发布/已停用版本的更新包不能删除"
                );
            }

            var store =
                new ClientUpdatePackageStore(
                    _environment.ContentRootPath
                );

            store.DeletePackage(
                versionId
            );

            return NoContent();
        }
    }
}
