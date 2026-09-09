namespace SoftwareServicePlatform.Api.Dtos.Users
{
    /// <summary>
    /// 管理员修改内部用户。
    /// </summary>
    public class UpdateInternalUserRequest
    {
        /// <summary>
        /// 显示名称。
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 角色：
        /// Admin / Support / Developer / Sales
        /// </summary>
        public string Role { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用账号。
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// 所属客户ID。
        ///
        /// Customer 用户必须有值。
        /// 内部用户为 null。
        /// </summary>
        public int? CustomerId { get; set; }
    }
}