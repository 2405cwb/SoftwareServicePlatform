using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 用户管理接口。
    ///
    /// 当前阶段：
    /// 只允许 Admin 管理用户。
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _dbContext;


        public UsersController(
            AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }


        /// <summary>
        /// 获取全部用户。
        ///
        /// GET:
        ///
        /// /api/users
        ///
        /// 注意：
        /// 绝对不能把 PasswordHash 返回给前端。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            /*
             * AsNoTracking：
             *
             * 当前只是查询，
             * 不准备修改这些 User，
             * 所以不需要 EF Core 跟踪。
             */
            var users =
                await _dbContext.Users

                    .AsNoTracking()

                    /*
                     * 用户可能属于一个 Customer。
                     *
                     * 我们前端除了 CustomerId，
                     * 还希望显示客户名称。
                     */
                    .Include(
                        x => x.Customer
                    )

                    /*
                     * 最近创建的用户排前面。
                     */
                    .OrderByDescending(
                        x => x.CreatedAt
                    )

                    /*
                     * 不直接：
                     *
                     * return Ok(users)
                     *
                     * 因为 User 里面有：
                     *
                     * PasswordHash
                     *
                     * 这个字段绝对不能发送到浏览器。
                     */
                    .Select(
                        x => new
                        {
                            x.Id,

                            x.Username,

                            x.DisplayName,

                            x.Role,

                            x.Email,

                            x.Phone,

                            x.IsEnabled,

                            x.CustomerId,

                            CustomerName =
                                x.Customer != null
                                    ? x.Customer.Name
                                    : string.Empty,

                            x.LastLoginAt,

                            x.CreatedAt,

                            x.UpdatedAt
                        }
                    )

                    .ToListAsync();


            return Ok(users);
        }
    }
}