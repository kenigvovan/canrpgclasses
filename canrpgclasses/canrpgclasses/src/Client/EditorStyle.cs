using Cairo;
using Vintagestory.API.Client;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Shared palette and separators so the content editors match the rest of the mod's windows.
    /// </summary>
    public static class EditorStyle
    {
        public static readonly double[] Gold = { 0.95, 0.88, 0.55, 1 };
        public static readonly double[] Accent = { 0.55, 0.82, 1.00, 1 };
        public static readonly double[] Muted = { 0.68, 0.66, 0.62, 1 };
        public static readonly double[] Warn = { 0.95, 0.62, 0.45, 1 };

        public static CairoFont Heading() => CairoFont.WhiteSmallishText().WithColor(Gold);

        public static CairoFont FieldLabel() => CairoFont.WhiteDetailText().WithColor(Muted);

        public static CairoFont Body() => CairoFont.WhiteDetailText();

        public static CairoFont Status(bool problem)
            => CairoFont.WhiteDetailText().WithColor(problem ? Warn : Gold);

        /// <summary>Hairline along the top edge, for static elements (absolute coordinates).</summary>
        public static void Rule(Context ctx, ImageSurface surface, ElementBounds bounds)
            => Line(ctx, bounds.drawX, bounds.drawY, bounds.OuterWidth);

        /// <summary>Same hairline for interactive custom-draws, whose surface origin is (0, 0).</summary>
        public static void RuleLocal(Context ctx, ImageSurface surface, ElementBounds bounds)
            => Line(ctx, 0, 1, bounds.OuterWidth);

        private static void Line(Context ctx, double x, double y, double width)
        {
            ctx.SetSourceRGBA(GuiStyle.DialogBorderColor[0], GuiStyle.DialogBorderColor[1],
                GuiStyle.DialogBorderColor[2], 0.65);
            ctx.LineWidth = 1;
            ctx.MoveTo(x, y);
            ctx.LineTo(x + width, y);
            ctx.Stroke();
        }
    }
}
