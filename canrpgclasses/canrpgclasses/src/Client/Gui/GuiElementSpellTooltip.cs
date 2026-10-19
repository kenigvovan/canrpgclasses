using System;
using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;

namespace canrpgclasses.Client.Gui
{
    /// <summary>
    /// Cursor-following spell tooltip. Vanilla hover text is single-colour, so <see cref="SpellTooltip.BuildLines"/>
    /// is baked into a Cairo texture, re-baked only when the spell changes.
    /// </summary>
    public class GuiElementSpellTooltip : GuiElement
    {
        /// <summary>Widest the tooltip gets before flavour text wraps (unscaled pixels).</summary>
        private const double MaxWidth = 400;

        private const double PadX = 8;
        private const double PadY = 6;

        private LoadedTexture texture;
        private string? shownKey;

        /// <summary>Raised by a hit this frame; misses never hide it, since several grids share one tooltip.
        /// Must be added to the composer after the grids.</summary>
        private bool requested;

        /// <summary>Body font size/face; colours come from the lines.</summary>
        public CairoFont Font = CairoFont.WhiteSmallishText();

        public GuiElementSpellTooltip(ICoreClientAPI capi, ElementBounds bounds) : base(capi, bounds)
        {
            texture = new LoadedTexture(capi);
        }

        /// <summary>Intentionally a no-op, see <see cref="requested"/>.</summary>
        public void Clear() { }

        /// <summary>Shows <paramref name="lines"/> this frame; <paramref name="key"/> avoids re-baking an unchanged tooltip.</summary>
        public void SetLines(string key, List<SpellTooltip.Line> lines)
        {
            requested = true;
            if (key == shownKey) return;
            shownKey = key;
            Bake(lines);
        }

        private void Bake(List<SpellTooltip.Line> lines)
        {
            double lineHeight = api.Gui.Text.GetLineHeight(Font);
            double maxTextWidth = scaled(MaxWidth) - 2 * scaled(PadX);

            // Only flavour text wraps.
            var laid = new List<(string text, double[] color)>();
            double widest = 0;
            foreach (var line in lines)
            {
                if (line.Wrap)
                {
                    foreach (var tl in api.Gui.Text.Lineize(Font, line.Text, maxTextWidth))
                        laid.Add((tl.Text.TrimEnd(), line.Color));
                }
                else laid.Add((line.Text, line.Color));
            }

            foreach (var (text, _) in laid)
                widest = Math.Max(widest, Font.GetTextExtents(text).Width);

            int w = (int)Math.Ceiling(Math.Min(widest, maxTextWidth) + 2 * scaled(PadX));
            int h = (int)Math.Ceiling(laid.Count * lineHeight + 2 * scaled(PadY));
            if (w <= 0 || h <= 0) { requested = false; return; }

            var surface = new ImageSurface(Format.Argb32, w, h);
            var ctx = genContext(surface);

            ctx.SetSourceRGBA(0.06, 0.05, 0.04, 0.94);
            RoundRectangle(ctx, 0, 0, w, h, GuiStyle.DialogBGRadius);
            ctx.Fill();
            ctx.SetSourceRGBA(0.55, 0.48, 0.38, 0.9);
            ctx.LineWidth = 2.0;
            RoundRectangle(ctx, 1, 1, w - 2, h - 2, GuiStyle.DialogBGRadius);
            ctx.Stroke();

            double y = scaled(PadY);
            foreach (var (text, color) in laid)
            {
                var font = Font.Clone().WithColor(color);
                // DrawTextLine doesn't apply the font itself; colour and ctx.FontExtents come from the context.
                font.SetupContext(ctx);
                api.Gui.Text.DrawTextLine(ctx, font, text, scaled(PadX), y);
                y += lineHeight;
            }

            api.Gui.LoadOrUpdateCairoTexture(surface, true, ref texture);
            ctx.Dispose();
            surface.Dispose();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            bool show = requested;
            requested = false;
            if (!show || texture.TextureId == 0) return;

            float x = api.Input.MouseX + 16;
            float y = api.Input.MouseY + 16;
            if (x + texture.Width > api.Render.FrameWidth) x = api.Input.MouseX - texture.Width - 8;
            if (y + texture.Height > api.Render.FrameHeight) y = api.Render.FrameHeight - texture.Height - 4;
            if (x < 0) x = 0;
            if (y < 0) y = 0;

            // z 500 like vanilla hover text; must clear the scrollbar handle at 200.
            api.Render.Render2DTexture(texture.TextureId, x, y, texture.Width, texture.Height, 500f);
        }

        public override void Dispose()
        {
            base.Dispose();
            texture?.Dispose();
        }
    }

    public static class GuiElementSpellTooltipHelper
    {
        public static GuiComposer AddSpellTooltip(this GuiComposer composer, ElementBounds bounds, string key)
        {
            if (!composer.Composed)
                composer.AddInteractiveElement(new GuiElementSpellTooltip(composer.Api, bounds), key);
            return composer;
        }

        public static GuiElementSpellTooltip GetSpellTooltip(this GuiComposer composer, string key)
            => (GuiElementSpellTooltip)composer.GetElement(key);
    }
}
