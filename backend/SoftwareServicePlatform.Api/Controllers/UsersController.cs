using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using Microsoft.AspNetCore.Identity;
using SoftwareServicePlatform.Api.Dtos.Users;
using SoftwareServicePlatform.Api.Models;
using System.Security.Claims; 
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

        private readonly IPasswordHasher<User> _passwordHasher;
        public UsersController(
            AppDbContext dbContext,
            IPasswordHasher<User> passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
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


        /// <summary>
        /// 管理员创建公司内部用户。
        ///
        /// POST:
        ///
        /// /api/users
        ///
        /// 只允许创建：
        /// Admin
        /// Support
        /// Developer
        /// Sales
        ///
        /// Customer 用户暂时不通过这个接口创建。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateInternalUser(
            CreateInternalUserRequest request)
        {
            /*
             * =====================================
             * 1. 检查用户名
             * =====================================
             */
            if (string.IsNullOrWhiteSpace(
                    request.Username))
            {
                return BadRequest(
                    "用户名不能为空"
                );
            }


            /*
             * =====================================
             * 2. 检查显示名称
             * =====================================
             */
            if (string.IsNullOrWhiteSpace(
                    request.DisplayName))
            {
                return BadRequest(
                    "用户名称不能为空"
                );
            }


            /*
             * =====================================
             * 3. 检查密码
             * =====================================
             *
             * 与现有注册接口保持一致：
             * 最低8位。
             */
            if (
                string.IsNullOrWhiteSpace(
                    request.Password)
                ||
                request.Password.Length < 8
            )
            {
                return BadRequest(
                    "密码不能少于8位"
                );
            }


            /*
             * =====================================
             * 4. 验证角色
             * =====================================
             *
             * 注意：
             *
             * 不能直接相信前端传过来的 Role。
             *
             * 前端即使手工发送：
             *
             * Role = "SuperAdmin"
             *
             * 后端也必须拒绝。
             */
            var allowedRoles = new[]
            {
        "Admin",
        "Support",
        "Developer",
        "Sales"
    };


            if (!allowedRoles.Contains(
                    request.Role))
            {
                return BadRequest(
                    "用户角色不正确"
                );
            }


            /*
             * =====================================
             * 5. 统一处理用户名
             * =====================================
             *
             * 和 AuthController 保持一致。
             *
             * ZhangSan
             * zhangsan
             *
             * 最终统一保存：
             *
             * zhangsan
             */
            var username =
                request.Username
                    .Trim()
                    .ToLowerInvariant();


            /*
             * =====================================
             * 6. 用户名不能重复
             * =====================================
             */
            var usernameExists =
                await _dbContext.Users
                    .AnyAsync(
                        x =>
                            x.Username == username
                    );


            if (usernameExists)
            {
                return BadRequest(
                    "用户名已经存在"
                );
            }


            /*
             * =====================================
             * 7. 创建内部用户
             * =====================================
             */
            var user =
                new User
                {
                    Username =
                        username,

                    DisplayName =
                        request.DisplayName.Trim(),

                    Role =
                        request.Role,

                    Email =
                        request.Email?.Trim()
                        ?? string.Empty,

                    Phone =
                        request.Phone?.Trim()
                        ?? string.Empty,

                    /*
                     * 公司内部账号
                     * 不属于某个客户。
                     */
                    CustomerId =
                        null,

                    IsEnabled =
                        true,

                    CreatedAt =
                        DateTime.UtcNow,

                    UpdatedAt =
                        DateTime.UtcNow
                };


            /*
             * =====================================
             * 8. 密码 Hash
             * =====================================
             *
             * 数据库绝对不保存明文密码。
             */
            user.PasswordHash =
                _passwordHasher.HashPassword(
                    user,
                    request.Password
                );


            /*
             * =====================================
             * 9. 保存数据库
             * =====================================
             */
            _dbContext.Users.Add(
                user
            );


            await _dbContext.SaveChangesAsync();


            /*
             * =====================================
             * 10. 返回安全数据
             * =====================================
             *
             * 不返回：
             *
             * Password
             * PasswordHash
             */
            return Ok(
                new
                {
                    message =
                        "内部用户创建成功",

                    user.Id,

                    user.Username,

                    user.DisplayName,

                    user.Role,

                    user.Email,

                    user.Phone,

                    user.IsEnabled,

                    user.CreatedAt
                }
            );
        }

        /// <summary>
        /// 修改内部用户。
        ///
        /// PUT /api/users/{id}
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateInternalUser(
            int id,
            UpdateInternalUserRequest request)
        {
            /*
             * 1. 查询用户
             */
            var user =
                await _dbContext.Users
                    .FirstOrDefaultAsync(
                        x => x.Id == id
                    );

            if (user == null)
            {
                return NotFound(
                    "用户不存在"
                );
            }


            


            /*
             * 2. 显示名称不能为空
             */
            if (string.IsNullOrWhiteSpace(
                    request.DisplayName))
            {
                return BadRequest(
                    "用户名称不能为空"
                );
            }


            /*
             * 3. 检查角色
             */
            var allowedRoles = new[]
    {
    "Admin",
    "Support",
    "Developer",
    "Sales",
    "Customer"
};

            /*
 * =====================================
 * Customer角色必须绑定客户
 * =====================================
 */
            if (request.Role == "Customer")
            {
                /*
                 * Customer 必须选择所属客户。
                 */
                if (
                    !request.CustomerId.HasValue
                    ||
                    request.CustomerId.Value <= 0
                )
                {
                    return BadRequest(
                        "客户用户必须选择所属客户"
                    );
                }


                /*
                 * 检查客户是否真的存在。
                 */
                var customer =
                    await _dbContext.Customers
                        .FirstOrDefaultAsync(
                            x =>
                                x.Id
                                ==
                                request.CustomerId.Value
                        );


                if (customer == null)
                {
                    return BadRequest(
                        "所属客户不存在"
                    );
                }


                /*
                 * 不允许把账号绑定到已经停用的客户。
                 */
                if (!customer.IsEnabled)
                {
                    return BadRequest(
                        "所属客户已停用"
                    );
                }
            }
            if (!allowedRoles.Contains(
                    request.Role))
            {
                return BadRequest(
                    "用户角色不正确"
                );
            }


            /*
             * 4. 获取当前正在操作的管理员ID
             *
             * 用于防止管理员把自己停掉。
             */
            var currentUserIdText =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );


            int.TryParse(
                currentUserIdText,
                out var currentUserId
            );


            /*
             * 当前管理员不能停用自己。
             *
             * 否则容易出现：
             *
             * admin 登录
             * ↓
             * 把自己点成停用
             * ↓
             * 后面无法继续管理系统
             */
            if (
                id == currentUserId
                &&
                !request.IsEnabled
            )
            {
                return BadRequest(
                    "不能停用当前正在登录的管理员账号"
                );
            }


            /*
             * 当前管理员也不能把自己
             * 从 Admin 改成其他角色。
             */
            if (
                id == currentUserId
                &&
                request.Role != "Admin"
            )
            {
                return BadRequest(
                    "不能修改当前登录管理员自己的角色"
                );
            }


            /*
             * 5. 修改字段
             *
             * 当前不允许修改 Username。
             * 登录用户名创建后先保持固定，
             * 能让账号审计更清楚。
             */
            user.DisplayName =
                request.DisplayName.Trim();

            user.Role =
                request.Role;

            user.Email =
                request.Email?.Trim()
                ?? string.Empty;

            user.Phone =
                request.Phone?.Trim()
                ?? string.Empty;

            user.IsEnabled =
                request.IsEnabled;

            /*
 * Customer角色：
 * 保存 CustomerId。
 *
 * 公司内部人员：
 * CustomerId 必须清空。
 */
            if (request.Role == "Customer")
            {
                user.CustomerId =
                    request.CustomerId;
            }
            else
            {
                user.CustomerId =
                    null;
            }

            user.UpdatedAt =
                DateTime.UtcNow;


            /*
             * 6. 保存
             */
            await _dbContext.SaveChangesAsync();


            /*
             * 7. 返回安全信息
             */
            return Ok(new
            {
                message = "用户修改成功",

                user.Id,

                user.Username,

                user.DisplayName,

                user.Role,

                user.Email,

                user.Phone,

                user.IsEnabled,

                user.UpdatedAt
            });
        }

        /// <summary>
        /// 管理员重置指定用户密码。
        ///
        /// POST /api/users/{id}/reset-password
        /// </summary>
        [HttpPost("{id}/reset-password")]
        public async Task<IActionResult> ResetUserPassword(
            int id,
            ResetUserPasswordRequest request)
        {
            /*
             * 新密码最少8位。
             */
            if (
                string.IsNullOrWhiteSpace(request.NewPassword)
                ||
                request.NewPassword.Length < 8
            )
            {
                return BadRequest(
                    "新密码不能少于8位"
                );
            }


            /*
             * 查询用户。
             */
            var user =
                await _dbContext.Users
                    .FirstOrDefaultAsync(
                        x => x.Id == id
                    );


            if (user == null)
            {
                return NotFound(
                    "用户不存在"
                );
            }


            /*
             * 重新生成密码Hash。
             *
             * 数据库永远不保存明文密码。
             */
            user.PasswordHash =
                _passwordHasher.HashPassword(
                    user,
                    request.NewPassword
                );


            user.UpdatedAt =
                DateTime.UtcNow;


            await _dbContext.SaveChangesAsync();


            return Ok(
                new
                {
                    message = "用户密码重置成功",

                    user.Id,

                    user.Username,

                    user.DisplayName
                }
            );
        }


    }
}