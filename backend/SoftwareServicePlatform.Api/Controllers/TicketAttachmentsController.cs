using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;
using System.Security.Claims;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 工单附件管理。
    /// </summary>
    [ApiController]
    [Route("api/tickets")]
    [Authorize(Roles = "Admin,Support,Developer,Customer")]
    public class TicketAttachmentsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;


        /// <summary>
        /// 单个附件最大 100MB。
        /// </summary>
        private const long MaxFileSize =
            100L * 1024 * 1024;


        /// <summary>
        /// 一次最多上传5个附件。
        /// </summary>
        private const int MaxFileCount = 5;


        /*
         * Ticket 附件不允许直接上传这些可执行文件。
         *
         * 日志、图片、PDF、Office、ZIP、7Z、RAR、
         * TXT、JSON、XML、DMP 等仍然可以上传。
         */
        private static readonly HashSet<string> BlockedExtensions =
            new(StringComparer.OrdinalIgnoreCase)
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


        public TicketAttachmentsController(
            AppDbContext dbContext,
            IWebHostEnvironment environment)
        {
            _dbContext = dbContext;

            _environment = environment;
        }


        /// <summary>
        /// 上传工单附件。
        ///
        /// POST /api/tickets/{ticketId}/attachments
        ///
        /// multipart/form-data
        /// </summary>
        [HttpPost("{ticketId:int}/attachments")]
        [RequestSizeLimit(520L * 1024 * 1024)]
        public async Task<IActionResult> UploadAttachments(
            int ticketId,
            [FromForm] List<IFormFile> files,
            [FromForm] bool isInternal = false,
            [FromForm] int? ticketRecordId = null)
        {
            if (ticketId <= 0)
            {
                return BadRequest("工单ID无效");
            }


            /*
             * ==========================================
             * 1. 当前用户
             * ==========================================
             */
            var currentUser =
                await GetCurrentUserAsync();


            if (currentUser == null)
            {
                return Unauthorized(
                    "当前用户不存在或已停用"
                );
            }


            /*
             * ==========================================
             * 2. 查询 Ticket
             * ==========================================
             */
            var ticket =
                await _dbContext.Tickets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Id == ticketId
                    );


            if (ticket == null)
            {
                return NotFound(
                    "工单不存在"
                );
            }


            /*
             * ==========================================
             * 3. Ticket 数据权限
             * ==========================================
             */
            if (!CanAccessTicket(
                    currentUser,
                    ticket))
            {
                return Forbid();
            }


            /*
             * Closed 后不能继续补附件。
             *
             * 如果问题再次出现，
             * 应该先 Reopen。
             */
            if (ticket.Status == "Closed")
            {
                return BadRequest(
                    "已关闭的工单不能上传附件，请先重新打开工单"
                );
            }


            /*
             * Customer 永远不能创建内部附件。
             */
            if (currentUser.Role == "Customer"
                &&
                isInternal)
            {
                return BadRequest(
                    "客户用户不能上传内部附件"
                );
            }


            /*
             * ==========================================
             * 4. 如果附件关联 TicketRecord
             * ==========================================
             */
            TicketRecord? ticketRecord = null;


            if (ticketRecordId.HasValue)
            {
                ticketRecord =
                    await _dbContext.TicketRecords
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x =>
                                x.Id ==
                                ticketRecordId.Value
                                &&
                                x.TicketId ==
                                ticketId
                        );


                if (ticketRecord == null)
                {
                    return BadRequest(
                        "指定的工单处理记录不存在"
                    );
                }

                /*
 * 附件只能关联到当前用户自己创建的处理记录。
 *
 * 防止用户手工构造 ticketRecordId，
 * 把自己的文件挂到其他人的回复下面。
 */
                if (ticketRecord.CreatedByUserId
                    !=
                    currentUser.Id)
                {
                    return BadRequest(
                        "只能给自己创建的处理记录上传附件"
                    );
                }
                /*
                 * Customer 不能给内部记录上传附件。
                 */
                if (currentUser.Role == "Customer"
                    &&
                    ticketRecord.IsInternal)
                {
                    return Forbid();
                }


                /*
                 * 如果这条 TicketRecord 本身就是内部记录，
                 * 它的附件也必须是内部附件。
                 */
                if (ticketRecord.IsInternal)
                {
                    isInternal = true;
                }
            }


            /*
             * ==========================================
             * 5. 文件数量检查
             * ==========================================
             */
            if (files == null
                ||
                files.Count == 0)
            {
                return BadRequest(
                    "请选择需要上传的附件"
                );
            }


            if (files.Count > MaxFileCount)
            {
                return BadRequest(
                    $"一次最多上传{MaxFileCount}个附件"
                );
            }


            /*
             * ==========================================
             * 6. 先统一检查所有文件
             * ==========================================
             *
             * 先检查，再真正保存。
             *
             * 防止：
             *
             * 前4个已经保存，
             * 第5个才发现非法。
             */
            foreach (var file in files)
            {
                if (file.Length <= 0)
                {
                    return BadRequest(
                        $"文件 {file.FileName} 内容为空"
                    );
                }


                if (file.Length > MaxFileSize)
                {
                    return BadRequest(
                        $"文件 {file.FileName} 超过100MB限制"
                    );
                }


                /*
                 * Path.GetFileName：
                 *
                 * 防止客户端传：
                 *
                 * ../../xxx.txt
                 *
                 * 最终只保留文件名。
                 */
                var originalFileName =
                    Path.GetFileName(
                        file.FileName
                    );


                if (string.IsNullOrWhiteSpace(
                        originalFileName))
                {
                    return BadRequest(
                        "附件文件名无效"
                    );
                }


                var extension =
                    Path.GetExtension(
                        originalFileName
                    );


                if (BlockedExtensions.Contains(
                        extension))
                {
                    return BadRequest(
                        $"不允许上传 {extension} 类型的文件"
                    );
                }
            }


            /*
             * ==========================================
             * 7. 准备存储目录
             * ==========================================
             *
             * 文件不放 wwwroot。
             *
             * 因为 wwwroot 可以作为公开静态文件目录。
             *
             * Ticket 附件必须经过权限接口下载。
             */
            var now =
                DateTime.UtcNow;


            var relativeDirectory =
                Path.Combine(
                    "Storage",
                    "TicketAttachments",
                    now.Year.ToString("D4"),
                    now.Month.ToString("D2"),
                    ticket.Id.ToString()
                );


            var fullDirectory =
                Path.Combine(
                    _environment.ContentRootPath,
                    relativeDirectory
                );


            Directory.CreateDirectory(
                fullDirectory
            );


            /*
             * 已经真正保存到磁盘的文件。
             *
             * 如果后面数据库保存失败，
             * 用于删除这些残留文件。
             */
            var savedPhysicalFiles =
                new List<string>();


            var attachments =
                new List<TicketAttachment>();


            try
            {
                /*
                 * ======================================
                 * 8. 保存文件
                 * ======================================
                 */
                foreach (var file in files)
                {
                    var originalFileName =
                        Path.GetFileName(
                            file.FileName
                        );


                    /*
                     * 只保留原扩展名。
                     */
                    var extension =
                        Path.GetExtension(
                            originalFileName
                        );


                    /*
                     * 服务器真正的文件名使用 Guid。
                     *
                     * 不允许直接使用客户原始文件名，
                     * 避免重名。
                     */
                    var storedFileName =
                        $"{Guid.NewGuid():N}{extension}";


                    var fullFilePath =
                        Path.Combine(
                            fullDirectory,
                            storedFileName
                        );

                    /*
                     * 先加入清理列表。
                     *
                     * 即使后面的文件写入中途失败，
                     * catch 中也能够把残留文件删除。
                     */
                    savedPhysicalFiles.Add(
                        fullFilePath
                    );


                    await using (
                        var stream =
                            new FileStream(
                                fullFilePath,
                                FileMode.CreateNew,
                                FileAccess.Write,
                                FileShare.None,
                                81920,
                                useAsync: true
                            )
                    )
                    {
                        await file.CopyToAsync(
                            stream
                        );
                    }


                    /*
                     * 数据库只保存相对路径。
                     */
                    var storagePath =
                        Path.GetRelativePath(
                            _environment.ContentRootPath,
                            fullFilePath
                        )
                        .Replace(
                            '\\',
                            '/'
                        );


                    var attachment =
                        new TicketAttachment
                        {
                            TicketId =
                                ticket.Id,

                            TicketRecordId =
                                ticketRecordId,

                            UploadedByUserId =
                                currentUser.Id,

                            FileName =
                                originalFileName,

                            StoredFileName =
                                storedFileName,

                            StoragePath =
                                storagePath,

                            FileSize =
                                file.Length,

                            ContentType =
                                string.IsNullOrWhiteSpace(
                                    file.ContentType)
                                    ? "application/octet-stream"
                                    : file.ContentType,

                            IsInternal =
                                isInternal,

                            CreatedAt =
                                now
                        };


                    attachments.Add(
                        attachment
                    );
                }


                /*
                 * ======================================
                 * 9. 一次保存数据库
                 * ======================================
                 */
                _dbContext.TicketAttachments
                    .AddRange(
                        attachments
                    );


                await _dbContext
                    .SaveChangesAsync();
            }
            catch
            {
                /*
                 * 如果数据库保存或者某个文件写入失败，
                 * 清理已经产生的物理文件。
                 */
                foreach (
                    var physicalFile
                    in
                    savedPhysicalFiles)
                {
                    try
                    {
                        if (System.IO.File.Exists(
                                physicalFile))
                        {
                            System.IO.File.Delete(
                                physicalFile
                            );
                        }
                    }
                    catch
                    {
                        /*
                         * 清理失败不覆盖原异常。
                         *
                         * 后面正式项目可以记录日志。
                         */
                    }
                }


                throw;
            }


            /*
             * ==========================================
             * 10. 返回附件信息
             * ==========================================
             */
            return StatusCode(
                StatusCodes.Status201Created,
                new
                {
                    message =
                        "附件上传成功",

                    files =
                        attachments.Select(
                            x => new
                            {
                                x.Id,

                                x.TicketId,

                                x.TicketRecordId,

                                x.FileName,

                                x.FileSize,

                                x.ContentType,

                                x.IsInternal,

                                x.CreatedAt
                            }
                        )
                }
            );
        }


        /// <summary>
        /// 查询工单附件。
        ///
        /// GET /api/tickets/{ticketId}/attachments
        /// </summary>
        [HttpGet("{ticketId:int}/attachments")]
        public async Task<IActionResult> GetAttachments(
            int ticketId)
        {
            if (ticketId <= 0)
            {
                return BadRequest(
                    "工单ID无效"
                );
            }


            var currentUser =
                await GetCurrentUserAsync();


            if (currentUser == null)
            {
                return Unauthorized(
                    "当前用户不存在或已停用"
                );
            }


            var ticket =
                await _dbContext.Tickets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Id == ticketId
                    );


            if (ticket == null)
            {
                return NotFound(
                    "工单不存在"
                );
            }


            if (!CanAccessTicket(
                    currentUser,
                    ticket))
            {
                return Forbid();
            }


            IQueryable<TicketAttachment> query =
                _dbContext.TicketAttachments
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.TicketId ==
                            ticketId
                    );


            /*
             * Customer 根本拿不到内部附件。
             *
             * 注意：
             * 不是前端隐藏。
             */
            if (currentUser.Role == "Customer")
            {
                query =
                    query.Where(
                        x => !x.IsInternal
                    );
            }


            var attachments =
                await query
                    .OrderBy(x => x.CreatedAt)
                    .Select(
                        x => new
                        {
                            x.Id,

                            x.TicketId,

                            x.TicketRecordId,

                            x.FileName,

                            x.FileSize,

                            x.ContentType,

                            x.IsInternal,

                            x.UploadedByUserId,

                            uploadedByName =
                                x.UploadedByUser.DisplayName,

                            uploadedByRole =
                                x.UploadedByUser.Role,

                            x.CreatedAt
                        }
                    )
                    .ToListAsync();


            return Ok(
                attachments
            );
        }


        /// <summary>
        /// 下载工单附件。
        ///
        /// GET /api/tickets/attachments/{attachmentId}/download
        /// </summary>
        [HttpGet("attachments/{attachmentId:int}/download")]
        public async Task<IActionResult> DownloadAttachment(
            int attachmentId)
        {
            if (attachmentId <= 0)
            {
                return BadRequest(
                    "附件ID无效"
                );
            }


            /*
             * ==========================================
             * 1. 当前用户
             * ==========================================
             */
            var currentUser =
                await GetCurrentUserAsync();


            if (currentUser == null)
            {
                return Unauthorized(
                    "当前用户不存在或已停用"
                );
            }


            /*
             * ==========================================
             * 2. 附件
             * ==========================================
             */
            var attachment =
                await _dbContext.TicketAttachments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id ==
                            attachmentId
                    );


            if (attachment == null)
            {
                return NotFound(
                    "附件不存在"
                );
            }


            /*
             * ==========================================
             * 3. 再查 Ticket
             * ==========================================
             *
             * 下载文件不能只知道附件ID就放行。
             *
             * 必须重新验证：
             * 当前用户有没有权限看它所属的 Ticket。
             */
            var ticket =
                await _dbContext.Tickets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id ==
                            attachment.TicketId
                    );


            if (ticket == null)
            {
                return NotFound(
                    "附件所属工单不存在"
                );
            }


            if (!CanAccessTicket(
                    currentUser,
                    ticket))
            {
                return Forbid();
            }


            /*
             * Customer 不允许下载内部附件。
             *
             * 即使客户猜到了 attachmentId，
             * 仍然会在这里被拒绝。
             */
            if (currentUser.Role == "Customer"
                &&
                attachment.IsInternal)
            {
                return Forbid();
            }


            /*
             * ==========================================
             * 4. 构造物理路径
             * ==========================================
             */
            var attachmentRoot =
                Path.GetFullPath(
                    Path.Combine(
                        _environment.ContentRootPath,
                        "Storage",
                        "TicketAttachments"
                    )
                );


            var fullFilePath =
                Path.GetFullPath(
                    Path.Combine(
                        _environment.ContentRootPath,
                        attachment.StoragePath
                    )
                );


            /*
             * ==========================================
             * 5. 路径安全检查
             * ==========================================
             *
             * 即使数据库中的 StoragePath
             * 将来因为错误数据被篡改成：
             *
             * ../../appsettings.json
             *
             * 也不能让接口读出去。
             */
            var comparison =
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;


            var rootPrefix =
                attachmentRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                )
                +
                Path.DirectorySeparatorChar;


            if (!fullFilePath.StartsWith(
                    rootPrefix,
                    comparison))
            {
                return BadRequest(
                    "附件存储路径无效"
                );
            }


            /*
             * ==========================================
             * 6. 文件必须真实存在
             * ==========================================
             */
            if (!System.IO.File.Exists(
                    fullFilePath))
            {
                return NotFound(
                    "附件文件不存在"
                );
            }


            /*
             * ==========================================
             * 7. 返回文件
             * ==========================================
             *
             * enableRangeProcessing:
             *
             * 支持浏览器 Range 请求。
             */
            return PhysicalFile(
                fullFilePath,
                string.IsNullOrWhiteSpace(
                    attachment.ContentType)
                    ? "application/octet-stream"
                    : attachment.ContentType,
                attachment.FileName,
                enableRangeProcessing: true
            );
        }


        /// <summary>
        /// 获取数据库中的当前用户。
        ///
        /// 不完全相信 JWT 中登录时的旧状态。
        /// </summary>
        private async Task<User?> GetCurrentUserAsync()
        {
            var userIdText =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );


            if (!int.TryParse(
                    userIdText,
                    out var currentUserId))
            {
                return null;
            }


            return await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                        currentUserId
                        &&
                        x.IsEnabled
                );
        }


        /// <summary>
        /// 当前用户是否有权访问指定工单。
        ///
        /// 注意：
        /// 这是数据权限，不只是角色权限。
        /// </summary>
        private static bool CanAccessTicket(
            User currentUser,
            Ticket ticket)
        {
            switch (currentUser.Role)
            {
                case "Admin":

                case "Support":
                    return true;


                case "Developer":
                    return
                        ticket.AssignedToUserId
                        ==
                        currentUser.Id;


                case "Customer":
                    return
                        currentUser.CustomerId.HasValue
                        &&
                        ticket.CustomerId
                        ==
                        currentUser.CustomerId.Value;


                default:
                    return false;
            }
        }
    }
}