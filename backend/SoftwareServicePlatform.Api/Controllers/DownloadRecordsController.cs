using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 软件安装包下载记录管理。
    ///
    /// 当前仅允许：
    ///
    /// Admin
    /// Support
    ///
    /// 查看下载历史。
    /// </summary>
    [ApiController]
    [Route("api/download-records")]
    [Authorize(Roles = "Admin,Support")]
    public class DownloadRecordsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;


        public DownloadRecordsController(
            AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }


        /// <summary>
        /// 分页查询软件下载记录。
        ///
        /// GET:
        ///
        /// /api/download-records
        ///
        /// 支持：
        ///
        /// page
        /// pageSize
        /// keyword
        ///
        /// 示例：
        ///
        /// /api/download-records?page=1&pageSize=20
        ///
        /// /api/download-records?page=1&pageSize=20&keyword=道路
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDownloadRecords(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? keyword = null)
        {
            /*
             * ==========================================
             * 1. 参数保护
             * ==========================================
             */

            if (page < 1)
            {
                page = 1;
            }


            if (pageSize < 1)
            {
                pageSize = 20;
            }


            /*
             * 防止一次请求数据库过多数据。
             */
            if (pageSize > 100)
            {
                pageSize = 100;
            }


            /*
             * ==========================================
             * 2. 创建基础查询
             * ==========================================
             *
             * 下载记录本身已经保存了：
             *
             * CustomerName
             * SoftwareName
             * Version
             * UserName
             *
             * 等历史快照。
             *
             * 因此这里查询下载历史时，
             * 不需要 Include 关联表。
             */
            var query =
                _dbContext.DownloadRecords
                    .AsNoTracking()
                    .AsQueryable();


            /*
             * ==========================================
             * 3. 关键字搜索
             * ==========================================
             *
             * 可以搜索：
             *
             * 用户名
             * 用户显示名称
             * 客户名称
             * 软件名称
             * 版本号
             * 文件名
             */
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword =
                    keyword.Trim();


                var pattern =
                    $"%{keyword}%";


                query =
                    query.Where(
                        x =>
                            EF.Functions.ILike(
                                x.UserName,
                                pattern
                            )
                            ||
                            EF.Functions.ILike(
                                x.UserDisplayName,
                                pattern
                            )
                            ||
                            EF.Functions.ILike(
                                x.CustomerName,
                                pattern
                            )
                            ||
                            EF.Functions.ILike(
                                x.SoftwareName,
                                pattern
                            )
                            ||
                            EF.Functions.ILike(
                                x.Version,
                                pattern
                            )
                            ||
                            EF.Functions.ILike(
                                x.FileName,
                                pattern
                            )
                    );
            }


            /*
             * ==========================================
             * 4. 查询记录总数
             * ==========================================
             */
            var total =
                await query.CountAsync();


            /*
             * ==========================================
             * 5. 分页查询
             * ==========================================
             *
             * 最新下载记录排在最前面。
             */
            var items =
                await query

                    .OrderByDescending(
                        x => x.DownloadedAt
                    )

                    .ThenByDescending(
                        x => x.Id
                    )

                    .Skip(
                        (page - 1)
                        * pageSize
                    )

                    .Take(
                        pageSize
                    )

                    .Select(
                        x => new
                        {
                            x.Id,

                            x.UserId,

                            x.UserName,

                            x.UserDisplayName,

                            x.CustomerId,

                            x.CustomerName,

                            x.SoftwareId,

                            x.SoftwareName,

                            x.SoftwareVersionId,

                            x.Version,

                            x.FileName,

                            x.FileSize,

                            x.DownloadedAt
                        }
                    )

                    .ToListAsync();


            /*
             * ==========================================
             * 6. 总页数
             * ==========================================
             */
            var totalPages =
                total == 0
                    ? 0
                    : (int)Math.Ceiling(
                        total
                        /
                        (double)pageSize
                    );


            /*
             * ==========================================
             * 7. 返回分页结果
             * ==========================================
             */
            return Ok(
                new
                {
                    page,

                    pageSize,

                    total,

                    totalPages,

                    items
                }
            );
        }
    }
}