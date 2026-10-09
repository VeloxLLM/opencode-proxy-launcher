using System.Globalization;

namespace OpenCodeProxyLauncher
{
    /// <summary>数字显示单位：默认 / 万 / 千万 / 亿。</summary>
    internal static class Units
    {
        public const string Raw = "raw";
        public const string Wan = "wan";
        public const string QianWan = "qianwan";
        public const string Yi = "yi";

        public static readonly string[] All = { Raw, Wan, QianWan, Yi };

        public static string Normalize(string unit)
        {
            foreach (string candidate in All)
            {
                if (candidate == unit)
                {
                    return candidate;
                }
            }

            return Raw;
        }

        public static string Label(string unit)
        {
            switch (Normalize(unit))
            {
                case Wan:
                    return "万";
                case QianWan:
                    return "千万";
                case Yi:
                    return "亿";
                default:
                    return Strings.IsEnglish ? "Raw" : "默认";
            }
        }

        public static string Number(long value, string unit)
        {
            switch (Normalize(unit))
            {
                case Wan:
                    return Scale(value / 1e4) + "万";
                case QianWan:
                    return Scale(value / 1e7) + "千万";
                case Yi:
                    return Scale(value / 1e8) + "亿";
                default:
                    return value.ToString("N0", CultureInfo.InvariantCulture);
            }
        }

        private static string Scale(double value)
        {
            if (value >= 1000)
            {
                return value.ToString("N0", CultureInfo.InvariantCulture);
            }

            if (value >= 100)
            {
                return value.ToString("N1", CultureInfo.InvariantCulture);
            }

            return value.ToString("N2", CultureInfo.InvariantCulture);
        }
    }
}
