using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using SoftwareServicePlatform.Api.Dtos.Auth;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 用户认证相关接口
    ///
    /// 当前阶段先实现：
    /// 注册
    ///
    /// 后续继续增加：
    /// 登录
    /// JWT
    /// 当前用户信息
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        private readonly IPasswordHasher<User> _passwordHasher;

        private readonly IConfiguration _configuration;

        public AuthController(
            AppDbContext dbContext,
            IPasswordHasher<User> passwordHasher,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
        }

        /// <summary>
        /// 创建客户用户
        ///
        /// POST /api/auth/register
        ///
        /// 当前阶段主要用于学习认证流程。
        ///
        /// 正式系统以后不会允许任何人随意注册，
        /// 而会改成管理员创建用户或受控邀请注册。
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            RegisterRequest request)
        {
            /*
             * 1. 验证用户名
             */
            if (string.IsNullOrWhiteSpace(
                    request.Username))
            {
                return BadRequest(
                    "用户名不能为空"
                );
            }

            /*
             * 2. 验证密码
             *
             * 当前先要求最少8位。
             * 后面可以再增加更完整密码规则。
             */
            if (string.IsNullOrWhiteSpace(
                    request.Password)
                ||
                request.Password.Length < 8)
            {
                return BadRequest(
                    "密码不能少于8位"
                );
            }

            /*
             * 3. 验证显示名称
             */
            if (string.IsNullOrWhiteSpace(
                    request.DisplayName))
            {
                return BadRequest(
                    "用户名称不能为空"
                );
            }

            /*
             * 4. 当前接口只创建客户用户，
             * 所以必须选择客户。
             */
            if (request.CustomerId <= 0)
            {
                return BadRequest(
                    "请选择所属客户"
                );
            }

            /*
             * 对用户名统一处理。
             *
             * 例如：
             *
             * ZhangSan
             * zhangsan
             *
             * 我们当前统一保存为小写，
             * 避免登录账号大小写混乱。
             */
            var username =
                request.Username
                    .Trim()
                    .ToLowerInvariant();

            /*
             * 5. 检查用户名是否已经存在
             */
            var usernameExists =
                await _dbContext.Users.AnyAsync(
                    x => x.Username == username
                );

            if (usernameExists)
            {
                return BadRequest(
                    "用户名已经存在"
                );
            }

            /*
             * 6. 检查客户是否存在
             */
            var customer =
                await _dbContext.Customers
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id ==
                            request.CustomerId
                    );

            if (customer == null)
            {
                return BadRequest(
                    "所属客户不存在"
                );
            }

            /*
             * 7. 如果客户已经停用，
             * 不允许继续给这个客户创建新用户。
             */
            if (!customer.IsEnabled)
            {
                return BadRequest(
                    "所属客户已停用"
                );
            }

            /*
             * 8. 创建 User。
             *
             * 注意：
             * 现在还没有设置 PasswordHash。
             */
            var user = new User
            {
                Username = username,

                DisplayName =
                    request.DisplayName.Trim(),

                Email =
                    request.Email.Trim(),

                Phone =
                    request.Phone.Trim(),

                /*
                 * 当前注册接口只能创建客户用户。
                 *
                 * 前端不能自己传：
                 * Role = Admin
                 *
                 * 否则会形成严重权限漏洞。
                 */
                Role = "Customer",

                CustomerId =
                    request.CustomerId,

                IsEnabled = true,

                CreatedAt =
                    DateTime.UtcNow,

                UpdatedAt =
                    DateTime.UtcNow
            };

            /*
             * 9. 核心步骤：
             *
             * 对明文密码进行 Hash。
             */
            user.PasswordHash =
                _passwordHasher.HashPassword(
                    user,
                    request.Password
                );

            /*
             * 10. 保存数据库
             */
            _dbContext.Users.Add(user);

            await _dbContext.SaveChangesAsync();

            /*
             * 11. 返回给前端。
             *
             * 绝对不要：
             *
             * return Ok(user);
             *
             * 因为 User 中包含 PasswordHash。
             *
             * PasswordHash 也不应该暴露给前端。
             */
            return Ok(new
            {
                message = "用户创建成功",

                user.Id,

                user.Username,

                user.DisplayName,

                user.Role,

                user.CustomerId,

                user.Email,

                user.Phone
            });
        }

        /// <summary>
        /// 用户登录
        ///
        /// POST /api/auth/login
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            LoginRequest request)
        {
            /*
             * 1. 基本参数检查
             */
            if (string.IsNullOrWhiteSpace(
                    request.Username))
            {
                return BadRequest(
                    "请输入用户名"
                );
            }

            if (string.IsNullOrWhiteSpace(
                    request.Password))
            {
                return BadRequest(
                    "请输入密码"
                );
            }

            /*
             * 2. 用户名统一处理。
             *
             * 注册时我们已经统一保存为小写，
             * 所以登录时也统一转小写。
             */
            var username =
                request.Username
                    .Trim()
                    .ToLowerInvariant();

            /*
             * 3. 根据用户名查找用户
             */
            var user =
                await _dbContext.Users
                    .FirstOrDefaultAsync(
                        x =>
                            x.Username ==
                            username
                    );

            /*
             * 4. 用户不存在
             */
            if (user == null)
            {
                return Unauthorized(
                    "用户名或密码错误"
                );
            }

            /*
             * 5. 用户已经被停用
             */
            if (!user.IsEnabled)
            {
                return Unauthorized(
                    "当前用户已停用"
                );
            }

            /*
             * 6. 验证用户输入的密码
             * 是否与数据库中的 PasswordHash 匹配。
             */
            var verifyResult =
                _passwordHasher
                    .VerifyHashedPassword(
                        user,
                        user.PasswordHash,
                        request.Password
                    );

            /*
             * PasswordVerificationResult
             * 可能的结果：
             *
             * Failed
             * Success
             * SuccessRehashNeeded
             */
            if (
                verifyResult ==
                PasswordVerificationResult.Failed
            )
            {
                return Unauthorized(
                    "用户名或密码错误"
                );
            }

            /*
             * 7. 如果是客户用户，
             * 还需要检查所属客户是否仍然存在、是否启用。
             */
            if (user.Role == "Customer")
            {
                if (!user.CustomerId.HasValue)
                {
                    return Unauthorized(
                        "当前用户未绑定客户"
                    );
                }

                var customer =
                    await _dbContext.Customers
                        .FirstOrDefaultAsync(
                            x =>
                                x.Id ==
                                user.CustomerId.Value
                        );

                if (customer == null)
                {
                    return Unauthorized(
                        "所属客户不存在"
                    );
                }

                if (!customer.IsEnabled)
                {
                    return Unauthorized(
                        "所属客户已停用"
                    );
                }
            }

            /*
             * 8. 登录成功，记录最后登录时间
             */
            user.LastLoginAt =
                DateTime.UtcNow;

            user.UpdatedAt =
                DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            /*
    * 登录验证成功以后，
    * 给当前用户签发 JWT Token。
    */
            var token =
                GenerateJwtToken(user);


            /*
             * 把 Token 返回给前端。
             */
            return Ok(new
            {
                message = "登录成功",

                token,

                user = new
                {
                    user.Id,

                    user.Username,

                    user.DisplayName,

                    user.Role,

                    user.CustomerId,

                    user.Email,

                    user.Phone,

                    user.LastLoginAt
                }
            });
        }
        /// <summary>
        /// 获取当前已经登录的用户信息。
        ///
        /// GET /api/auth/me
        ///
        /// 必须携带有效 JWT Token 才能访问。
        /// </summary>
        [Authorize]
        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            /*
             * JWT 验证通过以后，
             * ASP.NET Core 会把 Token 里面的 Claims
             * 放进 ControllerBase.User。
             *
             * 所以这里不需要再查询用户名密码。
             */

            /*
             * UserId
             *
             * 我们生成 JWT 时放进去的是：
             *
             * ClaimTypes.NameIdentifier
             */
            var userIdText =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );

            /*
             * 用户名
             */
            var username =
                User.FindFirstValue(
                    ClaimTypes.Name
                );

            /*
             * 用户角色
             */
            var role =
                User.FindFirstValue(
                    ClaimTypes.Role
                );

            /*
             * 自定义 Claim：
             * displayName
             */
            var displayName =
                User.FindFirstValue(
                    "displayName"
                );

            /*
             * 自定义 Claim：
             * customerId
             *
             * 公司内部用户可能不存在这个 Claim。
             */
            var customerIdText =
                User.FindFirstValue(
                    "customerId"
                );

            /*
             * UserId 从 Token 里取出来是字符串，
             * 转换成 int。
             */
            int.TryParse(
                userIdText,
                out var userId
            );

            /*
             * CustomerId 允许为空。
             */
            int? customerId = null;

            if (
                int.TryParse(
                    customerIdText,
                    out var parsedCustomerId
                )
            )
            {
                customerId =
                    parsedCustomerId;
            }

            /*
             * 返回当前登录身份。
             */
            return Ok(new
            {
                userId,

                username,

                displayName,

                role,

                customerId
            });
        }



        /// <summary>
        /// 给登录成功的用户生成 JWT Token
        /// </summary>
        private string GenerateJwtToken(
            User user)
        {
            /*
             * =========================
             * 1. 准备 Claims
             * =========================
             *
             * Claim 可以理解成：
             *
             * Token 里面携带的
             * “当前用户身份信息”。
             */
            var claims =
                new List<Claim>
                {
            /*
             * 当前用户ID
             */
            new Claim(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()
            ),

            /*
             * 登录账号
             */
            new Claim(
                ClaimTypes.Name,
                user.Username
            ),

            /*
             * 用户角色
             *
             * 后面可以直接：
             *
             * [Authorize(Roles = "Admin")]
             */
            new Claim(
                ClaimTypes.Role,
                user.Role
            ),

            /*
             * 显示名称
             */
            new Claim(
                "displayName",
                user.DisplayName
            )
                };


            /*
             * 客户用户才有 CustomerId。
             *
             * 公司内部人员：
             * CustomerId = null
             *
             * 所以不能无条件加入。
             */
            if (user.CustomerId.HasValue)
            {
                claims.Add(
                    new Claim(
                        "customerId",
                        user.CustomerId.Value
                            .ToString()
                    )
                );
            }


            /*
             * =========================
             * 2. 读取 JWT 配置
             * =========================
             */
            var issuer =
                _configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException(
                    "缺少 Jwt:Issuer"
                );

            var audience =
                _configuration["Jwt:Audience"]
                ?? throw new InvalidOperationException(
                    "缺少 Jwt:Audience"
                );

            var key =
                _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException(
                    "缺少 Jwt:Key"
                );


            /*
             * Token 有效时间。
             *
             * 如果配置不存在，
             * 默认使用120分钟。
             */
            var expireMinutes =
                _configuration.GetValue<int?>(
                    "Jwt:ExpireMinutes"
                ) ?? 120;


            /*
             * =========================
             * 3. 创建签名密钥
             * =========================
             */
            var securityKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(key)
                );


            /*
             * 使用 HMAC SHA256
             * 给 Token 签名。
             */
            var credentials =
                new SigningCredentials(
                    securityKey,
                    SecurityAlgorithms.HmacSha256
                );


            /*
             * =========================
             * 4. 创建 JWT
             * =========================
             */
            var token =
                new JwtSecurityToken(
                    issuer: issuer,

                    audience: audience,

                    claims: claims,

                    /*
                     * 从现在开始生效
                     */
                    notBefore:
                        DateTime.UtcNow,

                    /*
                     * 到什么时候过期
                     */
                    expires:
                        DateTime.UtcNow
                            .AddMinutes(
                                expireMinutes
                            ),

                    /*
                     * Token 签名
                     */
                    signingCredentials:
                        credentials
                );


            /*
             * =========================
             * 5. 转成字符串
             * =========================
             */
            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }
    }
}