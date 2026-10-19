namespace canrpgclasses.Client
{
    /// <summary>Shared number formatting so a stat reads the same in every window.</summary>
    public static class SheetFormat
    {
        /// <summary>Signed bonus: "+2.5" / "-1".</summary>
        public static string Signed(float v) => (v >= 0f ? "+" : "") + v.ToString("0.##");

        public static string Signed(int v) => (v >= 0 ? "+" : "") + v;

        /// <summary>A 0-based fraction as whole percent, without the sign: 0.145 -> "14".</summary>
        public static string Pct(float v) => (v * 100f).ToString("0");

        /// <summary>A multiplier or a raw count, trimmed: 2.20 -> "2.2", 14 -> "14".</summary>
        public static string Mult(float v) => v.ToString("0.##");
    }
}
