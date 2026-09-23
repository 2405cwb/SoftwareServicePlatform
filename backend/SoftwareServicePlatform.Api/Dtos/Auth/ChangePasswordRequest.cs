namespace SoftwareServicePlatform.Api.Dtos.Auth
{
    /// <summary>
    /// 当前登录用户修改自己的密码。
    /// </summary>
    public sealed class ChangePasswordRequest
    {
        /// <summary>
        /// 当前正在使用的密码。
        /// </summary>
        public string CurrentPassword { get; set; } = string.Empty;

        /// <summary>
        /// 新密码。
        /// </summary>
        public string NewPassword { get; set; } = string.Empty;

        /// <summary>
        /// 再次输入新密码，防止用户输错。
        /// </summary>
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
