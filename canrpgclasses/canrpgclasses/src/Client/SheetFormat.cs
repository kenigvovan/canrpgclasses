namespace canrpgclasses.Client
{
    /// <summary>How numbers read across the mod's windows: a signed bonus, a percentage, a multiplier. Shared so
    /// the same stat can't show as "+12%" in one panel and "0.12" in another.</summary>
    public static class SheetFormat
    {
        /// <summary>"+2.5" / "-1" - a bonus, where the sign is the point.</summary>
        public static string Signed(float v) => (v >= 0f ? "+" : "") + v.ToString("0.##");

        public static string Signed(int v) => (v >= 0 ? "+" : "") + v;

        /// <summary>A 0-based fraction as whole percent, without the sign: 0.145 -> "14".</summary>
        public static string Pct(float v) => (v * 100f).ToString("0");

        /// <summary>A multiplier or a raw count, trimmed: 2.20 -> "2.2", 14 -> "14".</summary>
        public static string Mult(float v) => v.ToString("0.##");
    }
}
