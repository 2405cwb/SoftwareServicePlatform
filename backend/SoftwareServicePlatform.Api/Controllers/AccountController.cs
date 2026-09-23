using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Dtos.Auth;
using SoftwareServicePlatform.Api.Models;
using System.Security.Claims;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 当前登录账号自己的账户安全操作。
    ///
    /// 与 UsersController 的区别：
    /// UsersController 是管理员管理别人；
    /// AccountController 是当前用户管理自己。
    /// </summary>
    [ApiController]
    [Route("api/account")]
    [Authorize]
    public class AccountController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AccountController(
            AppDbContext dbContext,
            IPasswordHasher<User> passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }

        /// <summary>
        /// 当前登录用户修改自己的密码。
        ///
        /// POST /api/account/change-password
        ///
        /// 适用于：
        /// Admin / Support / Developer / Sales / Customer。
        /// </summary>
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(
            ChangePasswordRequest request,
            CancellationToken cancellationToken)
        {
            var userIdText = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (!int.TryParse(userIdText, out var userId))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            {
                return BadRequest("请输入当前密码");
            }

            if (
                string.IsNullOrWhiteSpace(request.NewPassword)
                || request.NewPassword.Length < 8
            )
            {
                return BadRequest("新密码不能少于8位");
            }

            if (request.NewPassword != request.ConfirmPassword)
            {
                return BadRequest("两次输入的新密码不一致");
            }

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(
                    x => x.Id == userId,
                    cancellationToken
                );

            if (user == null)
            {
                return Unauthorized();
            }

            if (!user.IsEnabled)
            {
                return Unauthorized("当前用户已停用");
            }

            var currentPasswordResult =
                _passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    request.CurrentPassword
                );

            /*
             * 当前密码错误时返回 400 而不是 401。
             *
             * 前端 apiFetch 遇到 401 会认为 JWT 已失效并自动退出登录，
             * 但“当前密码输错”并不代表登录状态失效。
             */
            if (currentPasswordResult == PasswordVerificationResult.Failed)
            {
                return BadRequest("当前密码不正确");
            }

            var samePasswordResult =
                _passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    request.NewPassword
                );

            if (samePasswordResult != PasswordVerificationResult.Failed)
            {
                return BadRequest("新密码不能与当前密码相同");
            }

            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                request.NewPassword
            );

            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                message = "密码修改成功，请重新登录"
            });
        }
    }
}
