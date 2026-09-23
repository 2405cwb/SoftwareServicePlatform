using System.Security.Cryptography;
using System.Text;

namespace SoftwareServicePlatform.Api.Services.ClientUpdates
{
    /// <summary>
    /// 一次性激活码生成与校验工具。
    ///
    /// 激活码只用于第一次激活软件，
    /// 真正长期使用的是激活成功后返回的设备 UpdateToken。
    /// </summary>
    public static class ClientActivationCodeService
    {
        /// <summary>
        /// 生成适合人工输入的一次性激活码。
        ///
        /// 示例：
        /// A1B2-C3D4-E5F6-7890
        /// </summary>
        public static string GenerateCode()
        {
            var bytes = RandomNumberGenerator.GetBytes(8);
            var hex = Convert.ToHexString(bytes);

            return string.Join(
                "-",
                Enumerable.Range(0, 4)
                    .Select(index => hex.Substring(index * 4, 4))
            );
        }

        /// <summary>
        /// 标准化用户输入。
        ///
        /// 用户输入：
        /// a1b2-c3d4-e5f6-7890
        /// A1B2 C3D4 E5F6 7890
        ///
        /// 最终都转换为：
        /// A1B2C3D4E5F67890
        /// </summary>
        public static string NormalizeCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            return new string(
                code
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToUpperInvariant)
                    .ToArray()
            );
        }

        /// <summary>
        /// 数据库只保存激活码 SHA256，
        /// 不保存明文。
        /// </summary>
        public static string HashCode(string code)
        {
            var normalized = NormalizeCode(code);
            var bytes = Encoding.UTF8.GetBytes(normalized);

            return Convert.ToHexString(
                SHA256.HashData(bytes)
            );
        }
    }
}
