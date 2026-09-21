namespace SoftwareServicePlatform.Api.Services.ClientUpdates
{
    /// <summary>
    /// 客户端版本号比较器。
    ///
    /// 主要兼容：
    ///
    /// 1.0
    /// 1.0.0
    /// 1.2.10
    /// v2.0.0
    /// 2.0.0-beta
    ///
    /// 正式版本高于同主版本的预发布版本：
    ///
    /// 2.0.0 > 2.0.0-beta
    ///
    /// 当前项目版本号以数字版本为主，
    /// 所以这里不额外引入 NuGet.Versioning 依赖。
    /// </summary>
    public static class ClientVersionComparer
    {
        public static int Compare(
            string? left,
            string? right)
        {
            var a = Parse(left);
            var b = Parse(right);

            var max = Math.Max(
                a.NumericParts.Count,
                b.NumericParts.Count
            );

            for (var i = 0; i < max; i++)
            {
                var av =
                    i < a.NumericParts.Count
                        ? a.NumericParts[i]
                        : 0;

                var bv =
                    i < b.NumericParts.Count
                        ? b.NumericParts[i]
                        : 0;

                var compare =
                    av.CompareTo(bv);

                if (compare != 0)
                {
                    return compare;
                }
            }

            /*
             * 数字主体完全一致时：
             *
             * 2.0.0
             * 高于
             * 2.0.0-beta
             */
            if (
                string.IsNullOrWhiteSpace(
                    a.PreRelease)
                &&
                !string.IsNullOrWhiteSpace(
                    b.PreRelease)
            )
            {
                return 1;
            }

            if (
                !string.IsNullOrWhiteSpace(
                    a.PreRelease)
                &&
                string.IsNullOrWhiteSpace(
                    b.PreRelease)
            )
            {
                return -1;
            }

            return string.Compare(
                a.PreRelease,
                b.PreRelease,
                StringComparison.OrdinalIgnoreCase
            );
        }


        private static ParsedVersion Parse(
            string? value)
        {
            var text =
                (value ?? string.Empty)
                    .Trim();

            if (
                text.StartsWith(
                    "v",
                    StringComparison.OrdinalIgnoreCase)
            )
            {
                text =
                    text[1..];
            }

            /*
             * + 后面属于 build metadata，
             * 不参与版本高低判断。
             */
            var plusIndex =
                text.IndexOf('+');

            if (plusIndex >= 0)
            {
                text =
                    text[..plusIndex];
            }

            string preRelease =
                string.Empty;

            var dashIndex =
                text.IndexOf('-');

            if (dashIndex >= 0)
            {
                preRelease =
                    text[(dashIndex + 1)..];

                text =
                    text[..dashIndex];
            }

            var parts =
                new List<long>();

            foreach (
                var part
                in text.Split(
                    '.',
                    StringSplitOptions.RemoveEmptyEntries
                )
            )
            {
                /*
                 * 如果出现类似：
                 *
                 * 1.0.0.20260918
                 *
                 * 仍然按数字比较。
                 *
                 * 如果某一段不是纯数字，
                 * 尽量取开头连续数字。
                 */
                var digits =
                    new string(
                        part
                            .TakeWhile(char.IsDigit)
                            .ToArray()
                    );

                if (
                    long.TryParse(
                        digits,
                        out var number)
                )
                {
                    parts.Add(number);
                }
                else
                {
                    parts.Add(0);
                }
            }

            if (parts.Count == 0)
            {
                parts.Add(0);
            }

            return new ParsedVersion(
                parts,
                preRelease
            );
        }


        private sealed record ParsedVersion(
            List<long> NumericParts,
            string PreRelease
        );
    }
}
