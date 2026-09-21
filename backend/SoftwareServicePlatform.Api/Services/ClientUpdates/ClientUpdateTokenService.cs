using System.Security.Cryptography;
using System.Text;

namespace SoftwareServicePlatform.Api.Services.ClientUpdates
{
    /// <summary>
    /// 客户端更新 Token 的生成与哈希工具。
    ///
    /// Token 只用于“某个客户 + 某个软件”的自动更新能力，
    /// 不等价于平台登录账号，也不能访问工单、客户等业务接口。
    /// </summary>
    public static class ClientUpdateTokenService
    {
        private const string Prefix =
            "ssp_upd_";


        /// <summary>
        /// 生成高随机度 Token。
        /// </summary>
        public static string GenerateToken()
        {
            var randomBytes =
                RandomNumberGenerator
                    .GetBytes(32);

            var base64 =
                Convert
                    .ToBase64String(
                        randomBytes
                    )
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');

            return Prefix + base64;
        }


        /// <summary>
        /// 服务端数据库只保存 SHA256，
        /// 不保存明文 Token。
        /// </summary>
        public static string HashToken(
            string token)
        {
            var bytes =
                Encoding.UTF8
                    .GetBytes(token);

            return Convert
                .ToHexString(
                    SHA256.HashData(
                        bytes
                    )
                );
        }


        /// <summary>
        /// 后台只展示 Token 前缀，
        /// 让管理员能区分“是哪一个凭证”，
        /// 但不能从数据库恢复完整 Token。
        /// </summary>
        public static string GetDisplayPrefix(
            string token)
        {
            const int maxLength = 20;

            if (token.Length <= maxLength)
            {
                return token;
            }

            return token[..maxLength] + "...";
        }
    }
}
