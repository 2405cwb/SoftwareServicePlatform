using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;
using System.Security.Claims;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 软件版本附加资料接口。
    ///
    /// 负责：
    ///
    /// 用户手册
    /// 版本说明
    /// 常见问题
    /// 问题日志
    /// 配置文件
    /// 其他资料
    ///
    /// 注意：
    /// 这里不负责安装包。
    /// 安装包继续使用原来的 SoftwareVersion 上传逻辑。
    /// </summary>
    [ApiController]
    [Route("api/software-versions")]
    [Authorize(
        Roles = "Admin,Support,Developer"
    )]
    public class SoftwareVersionAttachmentsController
        : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        private readonly IWebHostEnvironment _environment;


        public SoftwareVersionAttachmentsController(
            AppDbContext dbContext,
            IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _environment = environment;
        }


        /// <summary>
        /// 给指定软件版本上传一个附加资料。
        ///
        /// POST
        /// /api/software-versions/{versionId}/attachments
        ///
        /// 请求类型：
        /// multipart/form-data
        /// </summary>
        [HttpPost("{versionId:int}/attachments")]
        [RequestSizeLimit(110 * 1024 * 1024)]
        public async Task<IActionResult> UploadAttachment(
            int versionId,

            [FromForm] IFormFile file,

            [FromForm] string attachmentType = "Other",

            [FromForm] bool isCustomerVisible = true,

            [FromForm] string remark = "")
        {
            /*
             * ==========================================
             * 1. 检查软件版本
             * ==========================================
             */
            var softwareVersion =
                await _dbContext.SoftwareVersions
                    .FirstOrDefaultAsync(
                        x => x.Id == versionId
                    );


            if (softwareVersion == null)
            {
                return NotFound(
                    "软件版本不存在"
                );
            }


            /*
             * ==========================================
             * 2. 检查当前用户
             * ==========================================
             *
             * JWT 中的 NameIdentifier
             * 保存的是 User.Id。
             */
            var userIdText =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );


            if (!int.TryParse(
                    userIdText,
                    out var currentUserId))
            {
                return Unauthorized(
                    "无法识别当前用户"
                );
            }


            var currentUserExists =
                await _dbContext.Users.AnyAsync(
                    x =>
                        x.Id == currentUserId
                        &&
                        x.IsEnabled
                );


            if (!currentUserExists)
            {
                return Unauthorized(
                    "当前用户不存在或已停用"
                );
            }


            /*
             * ==========================================
             * 3. 检查文件
             * ==========================================
             */
            if (file == null ||
                file.Length <= 0)
            {
                return BadRequest(
                    "请选择需要上传的文件"
                );
            }


            /*
             * 单个附加资料最大100MB。
             */
            const long maxFileSize =
                100L * 1024 * 1024;


            if (file.Length > maxFileSize)
            {
                return BadRequest(
                    "单个附件不能超过100MB"
                );
            }


            /*
             * ==========================================
             * 4. 检查附件类型
             * ==========================================
             */
            var allowedAttachmentTypes =
                new[]
                {
                    "Manual",
                    "ReleaseDocument",
                    "Troubleshooting",
                    "Log",
                    "Config",
                    "Other"
                };


            attachmentType =
                attachmentType.Trim();


            var validAttachmentType =
                allowedAttachmentTypes.Any(
                    x =>
                        string.Equals(
                            x,
                            attachmentType,
                            StringComparison.OrdinalIgnoreCase
                        )
                );


            if (!validAttachmentType)
            {
                return BadRequest(
                    "附件类型不正确"
                );
            }


            /*
             * 把大小写统一成系统约定值。
             *
             * 比如前端传：
             *
             * manual
             *
             * 最终保存：
             *
             * Manual
             */
            attachmentType =
                allowedAttachmentTypes
                    .First(
                        x =>
                            string.Equals(
                                x,
                                attachmentType,
                                StringComparison.OrdinalIgnoreCase
                            )
                    );


            /*
             * ==========================================
             * 5. 问题日志强制内部可见
             * ==========================================
             *
             * 即使有人绕过前端，
             * 手动发送：
             *
             * AttachmentType = Log
             * IsCustomerVisible = true
             *
             * 后端仍然强制改成 false。
             *
             * 客户权限不能只靠前端控制。
             */
            if (attachmentType == "Log")
            {
                isCustomerVisible = false;
            }


            /*
             * ==========================================
             * 6. 禁止危险文件类型
             * ==========================================
             *
             * 版本附件主要是文档、日志、
             * 配置、压缩包等资料。
             *
             * 不允许把可执行程序作为
             * “附加资料”上传。
             *
             * 正式安装包有自己独立的上传接口。
             */
            var extension =
                Path.GetExtension(
                    file.FileName
                ).ToLowerInvariant();


            var blockedExtensions =
                new[]
                {
                    ".exe",
                    ".dll",
                    ".msi",
                    ".bat",
                    ".cmd",
                    ".com",
                    ".scr",
                    ".ps1",
                    ".vbs",
                    ".js",
                    ".reg"
                };


            if (blockedExtensions.Contains(extension))
            {
                return BadRequest(
                    "该文件类型不能作为版本附加资料上传"
                );
            }


            /*
             * ==========================================
             * 7. 创建服务器保存目录
             * ==========================================
             *
             * 文件不放进 wwwroot。
             *
             * 这样别人不能直接通过 URL
             * 绕过权限访问。
             */
            var now =
                DateTime.UtcNow;


            var relativeDirectory =
                Path.Combine(
                    "Storage",
                    "VersionAttachments",
                    now.Year.ToString(),
                    now.Month.ToString("D2"),
                    versionId.ToString()
                );


            var physicalDirectory =
                Path.Combine(
                    _environment.ContentRootPath,
                    relativeDirectory
                );


            Directory.CreateDirectory(
                physicalDirectory
            );


            /*
             * ==========================================
             * 8. 生成服务器文件名
             * ==========================================
             *
             * 用户看到：
             *
             * 用户手册.pdf
             *
             * 服务器实际可能保存：
             *
             * 1d3e...a9.pdf
             */
            var storedFileName =
                $"{Guid.NewGuid():N}{extension}";


            var physicalFilePath =
                Path.Combine(
                    physicalDirectory,
                    storedFileName
                );


            var relativeFilePath =
                Path.Combine(
                    relativeDirectory,
                    storedFileName
                );


            try
            {
                /*
                 * ======================================
                 * 9. 保存物理文件
                 * ======================================
                 */
                await using (
                    var fileStream =
                        new FileStream(
                            physicalFilePath,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None
                        )
                )
                {
                    await file.CopyToAsync(
                        fileStream
                    );
                }


                /*
                 * ======================================
                 * 10. 创建数据库记录
                 * ======================================
                 */
                var attachment =
                    new SoftwareVersionAttachment
                    {
                        SoftwareVersionId =
                            softwareVersion.Id,

                        FileName =
                            Path.GetFileName(
                                file.FileName
                            ),

                        StoredFileName =
                            storedFileName,

                        StoragePath =
                            relativeFilePath,

                        FileSize =
                            file.Length,

                        ContentType =
                            file.ContentType ?? string.Empty,

                        AttachmentType =
                            attachmentType,

                        IsCustomerVisible =
                            isCustomerVisible,

                        UploadedByUserId =
                            currentUserId,

                        Remark =
                            remark?.Trim()
                            ?? string.Empty,

                        CreatedAt =
                            now
                    };


                _dbContext
                    .SoftwareVersionAttachments
                    .Add(
                        attachment
                    );


                /*
                 * 上传附件也属于版本发生变化，
                 * 所以顺便刷新版本的修改时间。
                 */
                softwareVersion.UpdatedAt =
                    now;


                await _dbContext
                    .SaveChangesAsync();


                /*
                 * ======================================
                 * 11. 返回前端
                 * ======================================
                 *
                 * 不返回 StoragePath，
                 * 防止服务器内部路径信息暴露。
                 */
                return Ok(
                    new
                    {
                        attachment.Id,

                        attachment.SoftwareVersionId,

                        attachment.FileName,

                        attachment.FileSize,

                        attachment.ContentType,

                        attachment.AttachmentType,

                        attachment.IsCustomerVisible,

                        attachment.Remark,

                        attachment.UploadedByUserId,

                        attachment.CreatedAt
                    }
                );
            }
            catch
            {
                /*
                 * 如果：
                 *
                 * 文件已经写入硬盘
                 *
                 * 但数据库保存失败
                 *
                 * 就把刚才写进去的物理文件删除，
                 * 防止产生垃圾文件。
                 */
                if (System.IO.File.Exists(
                        physicalFilePath))
                {
                    System.IO.File.Delete(
                        physicalFilePath
                    );
                }


                throw;
            }
        }


        /// <summary>
        /// 查询某个软件版本下面的所有附加资料。
        ///
        /// GET
        /// /api/software-versions/{versionId}/attachments
        /// </summary>
        [HttpGet("{versionId:int}/attachments")]
        public async Task<IActionResult> GetAttachments(
            int versionId)
        {
            /*
             * 先确认版本存在。
             */
            var versionExists =
                await _dbContext.SoftwareVersions
                    .AnyAsync(
                        x => x.Id == versionId
                    );


            if (!versionExists)
            {
                return NotFound(
                    "软件版本不存在"
                );
            }


            /*
             * 查询附件。
             *
             * 后台版本管理目前只有：
             *
             * Admin
             * Support
             * Developer
             *
             * 可以访问这个 Controller，
             * 所以这里可以返回内部附件。
             */
            var attachments =
                await _dbContext
                    .SoftwareVersionAttachments

                    .AsNoTracking()

                    .Where(
                        x =>
                            x.SoftwareVersionId
                            ==
                            versionId
                    )

                    .OrderByDescending(
                        x => x.CreatedAt
                    )

                    .Select(
                        x => new
                        {
                            x.Id,

                            x.SoftwareVersionId,

                            x.FileName,

                            x.FileSize,

                            x.ContentType,

                            x.AttachmentType,

                            x.IsCustomerVisible,

                            x.Remark,

                            x.UploadedByUserId,

                            UploadedByName =
                                x.UploadedByUser.DisplayName,

                            x.CreatedAt
                        }
                    )

                    .ToListAsync();


            return Ok(
                attachments
            );
        }/// <summary>
         /// 删除版本附加资料。
         ///
         /// DELETE
         /// /api/software-versions/attachments/{attachmentId}
         /// </summary>
        [HttpDelete("attachments/{attachmentId:int}")]
        public async Task<IActionResult> DeleteAttachment(
            int attachmentId)
        {
            /*
             * 查询数据库记录。
             */
            var attachment =
                await _dbContext
                    .SoftwareVersionAttachments

                    .FirstOrDefaultAsync(
                        x =>
                            x.Id == attachmentId
                    );


            if (attachment == null)
            {
                return NotFound(
                    "附件不存在"
                );
            }


            /*
             * 先得到物理文件路径。
             */
            var physicalFilePath =
                Path.GetFullPath(
                    Path.Combine(
                        _environment.ContentRootPath,
                        attachment.StoragePath
                    )
                );


            var storageRoot =
                Path.GetFullPath(
                    Path.Combine(
                        _environment.ContentRootPath,
                        "Storage",
                        "VersionAttachments"
                    )
                );


            /*
             * 路径安全检查。
             */
            if (!physicalFilePath.StartsWith(
                    storageRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(
                    "附件路径不合法"
                );
            }


            /*
             * 先删除数据库记录。
             */
            _dbContext
                .SoftwareVersionAttachments
                .Remove(
                    attachment
                );


            await _dbContext
                .SaveChangesAsync();


            /*
             * 数据库删除成功以后，
             * 再删除物理文件。
             *
             * 即使物理文件已经不存在，
             * 也不影响数据库删除结果。
             */
            if (System.IO.File.Exists(
                    physicalFilePath))
            {
                System.IO.File.Delete(
                    physicalFilePath
                );
            }


            return Ok(
                new
                {
                    message =
                        "附件删除成功"
                }
            );
        }
    }
}