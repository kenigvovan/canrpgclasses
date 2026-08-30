using System;
using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;

namespace canrpgclasses.Client.Gui
{
    /// <summary>
    /// The spell tooltip that follows the cursor in the spellbook, class picker and talent window. Vanilla's
    /// hover text is single-colour, so the coloured lines from <see cref="SpellTooltip.BuildLines"/> are baked
    /// into one Cairo texture instead - re-baked only when the hovered spell changes.
    /// </summary>
    public class GuiElementSpellTooltip : GuiElement
    {
        /// <summary>Widest the tooltip gets before flavour text wraps (unscaled pixels).</summary>
        private const double MaxWidth = 400;

        private const double PadX = 8;
        private const double PadY = 6;

        private LoadedTexture texture;
        private string? shownKey;

        /// <summary>Someone asked for a tooltip during this frame's render pass. Only a hit raises it: several
        /// grids share one tooltip, so if a miss could hide it, whichever grid rendered last would always win.
        /// The element is added to the composer last, after every grid has had its say.</summary>
        private bool requested;

        /// <summary>Body font. Settable so a dialog can size the tooltip to its own text; the colours come from the
        /// lines themselves, this only carries size/face.</summary>
        public CairoFont Font = CairoFont.WhiteSmallishText();

        public GuiElementSpellTooltip(ICoreClientAPI capi, ElementBounds bounds) : base(capi, bounds)
        {
            texture = new LoadedTexture(capi);
        }

        /// <summary>"Nothing under my cursor" - deliberately a no-op on visibility, see <see cref="requested"/>.
        /// The tooltip hides on its own once no one asks for it in a frame.</summary>
        public void Clear() { }

        /// <summary>Shows <paramref name="lines"/> for this frame. <paramref name="key"/> identifies what is being
        /// described (usually the spell id) so an unchanged tooltip is not re-baked every frame.</summary>
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

            // Flavour text is the only line that wraps; everything else is a short generated line.
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
                // DrawTextLine draws with whatever is already on the context - it does not apply the font itself,
                // so without this every line would come out in the last colour set above (and on the wrong
                // baseline, since it reads ctx.FontExtents).
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

            // z 500, same as vanilla's own hover text. It has to clear the scrollbar handle, which draws at 200 -
            // anything lower and a tooltip next to a scrollable list gets a slider drawn through it.
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
