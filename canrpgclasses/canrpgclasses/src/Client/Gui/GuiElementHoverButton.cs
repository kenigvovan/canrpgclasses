using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace canrpgclasses.Client.Gui
{
    /// <summary>Text button with a visible hover state; vanilla's 10% white overlay is lost on the brown dialog.</summary>
    public class GuiElementHoverButton : GuiElementTextButton
    {
        private LoadedTexture glowTexture;

        public GuiElementHoverButton(ICoreClientAPI capi, string text, CairoFont font, CairoFont hoverFont,
            ActionConsumable onClick, ElementBounds bounds, EnumButtonStyle style)
            : base(capi, text, font, hoverFont, onClick, bounds, style)
        {
            glowTexture = new LoadedTexture(capi);
        }

        public override void ComposeElements(Context ctxStatic, ImageSurface surfaceStatic)
        {
            base.ComposeElements(ctxStatic, surfaceStatic);

            double w = Bounds.OuterWidth, h = Bounds.OuterHeight;
            using var surface = new ImageSurface(Format.Argb32, (int)w, (int)h);
            using var ctx = genContext(surface);
            ctx.SetSourceRGBA(1, 1, 1, 0.12);
            ctx.Rectangle(0, 0, w, h);
            ctx.Fill();
            var gold = EditorStyle.Gold;
            ctx.SetSourceRGBA(gold[0], gold[1], gold[2], 0.85);
            ctx.LineWidth = scaled(1.5);
            ctx.Rectangle(ctx.LineWidth / 2, ctx.LineWidth / 2, w - ctx.LineWidth, h - ctx.LineWidth);
            ctx.Stroke();
            generateTexture(surface, ref glowTexture);
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            base.RenderInteractiveElements(deltaTime);
            if (Visible && enabled && Bounds.PointInside(api.Input.MouseX, api.Input.MouseY))
                api.Render.Render2DTexturePremultipliedAlpha(glowTexture.TextureId, Bounds, 51f);
        }

        public override void Dispose()
        {
            base.Dispose();
            glowTexture?.Dispose();
        }
    }

    public static class GuiElementHoverButtonHelper
    {
        /// <summary>Drop-in for AddSmallButton with a clear hover highlight.</summary>
        public static GuiComposer AddHoverButton(this GuiComposer composer, string text, ActionConsumable onClick,
            ElementBounds bounds, EnumButtonStyle style = EnumButtonStyle.Normal, string? key = null)
        {
            if (!composer.Composed)
            {
                var font = CairoFont.SmallButtonText(style);
                var pressed = CairoFont.SmallButtonText(style);
                pressed.Color = (double[])GuiStyle.ActiveButtonTextColor.Clone();
                var elem = new GuiElementHoverButton(composer.Api, text, font, pressed, onClick, bounds, style);
                elem.SetOrientation(font.Orientation);
                composer.AddInteractiveElement(elem, key);
            }
            return composer;
        }
    }
}
