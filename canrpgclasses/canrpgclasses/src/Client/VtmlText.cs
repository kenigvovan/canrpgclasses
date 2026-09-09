using System.Globalization;
using Vintagestory.API.Common;

namespace canrpgclasses.Client
{
    /// <summary>Colour-coded VTML runs for the windows drawing text through a richtext element (talent summaries,
    /// character sheet). Lang and authored text is escaped, and the shared palette keeps the windows in step.</summary>
    public static class VtmlText
    {
        // Row colours, as hex because VTML wants hex.
        public const string RowLabel = "#C9C4BC";
        public const string Good = "#86D97F";
        public const string Bad = "#E08A8A";
        public const string Neutral = "#DCD5C8";
        public const string Inactive = "#8A8A93";

        public static readonly string Gold = Hex(EditorStyle.Gold);
        public static readonly string Accent = Hex(EditorStyle.Accent);
        public static readonly string Muted = Hex(EditorStyle.Muted);
        public static readonly string Warn = Hex(EditorStyle.Warn);

        public static string Esc(string? text)
            => (text ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        public static string Color(string hex, string text)
            => $"<font color=\"{hex}\">{Esc(text)}</font>";

        public static string Bold(string hex, string text)
            => $"<font color=\"{hex}\" weight=\"bold\">{Esc(text)}</font>";

        public static string Hex(double[] rgba) => VtmlUtil.toHexColor(rgba);

        public static string Hex(float r, float g, float b)
            => "#" + Byte(r) + Byte(g) + Byte(b);

        private static string Byte(float v)
            => ((int)(System.Math.Clamp(v, 0f, 1f) * 255f + 0.5f)).ToString("X2", CultureInfo.InvariantCulture);
    }
}
