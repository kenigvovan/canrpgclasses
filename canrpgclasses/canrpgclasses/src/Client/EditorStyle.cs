using Cairo;
using Vintagestory.API.Client;

namespace canrpgclasses.Client
{
    /// <summary>
    /// The look the content editors share with the rest of the mod's windows: the same gold/accent/muted palette
    /// and the same hairline rule between blocks as the class picker and the talent tree, so an admin window
    /// doesn't read as a different mod.
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

        /// <summary>A hairline separator between blocks, drawn along the top edge of its bounds.</summary>
        public static void Rule(Context ctx, ImageSurface surface, ElementBounds bounds)
        {
            ctx.SetSourceRGBA(GuiStyle.DialogBorderColor[0], GuiStyle.DialogBorderColor[1],
                GuiStyle.DialogBorderColor[2], 0.65);
            ctx.LineWidth = 1;
            ctx.MoveTo(bounds.drawX, bounds.drawY);
            ctx.LineTo(bounds.drawX + bounds.OuterWidth, bounds.drawY);
            ctx.Stroke();
        }
    }
}
