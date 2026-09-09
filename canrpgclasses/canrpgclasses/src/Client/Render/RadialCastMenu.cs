using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace canrpgclasses.Client.Render
{
    /// <summary>
    /// Shows a skill bar as a ring around the screen centre while its key is held: the mouse direction picks a
    /// slot and releasing casts it. Any bar can be opened this way, so there are as many wheels as bars.
    /// </summary>
    public class RadialCastMenu : IRenderer
    {
        private const float Radius = 150f;
        private const float DeadZone = 40f;

        private readonly ICoreClientAPI capi;
        private readonly SpellHotbar hotbar;
        private readonly SpellClickInput cursor;
        private readonly Hud2D draw;
        private readonly CairoFont font = CairoFont.WhiteSmallText();

        private int openBar = -1;
        private int holdKey = -1;
        private int selected = -1;

        public double RenderOrder => 0.91;
        public int RenderRange => 0;

        public RadialCastMenu(ICoreClientAPI capi, SpellHotbar hotbar, SpellClickInput cursor)
        {
            this.capi = capi;
            this.hotbar = hotbar;
            this.cursor = cursor;
            draw = new Hud2D(capi);
            capi.Event.RegisterRenderer(this, EnumRenderStage.Ortho, "canrpgclasses-radial");
        }

        public bool IsOpen => openBar >= 0;

        /// <summary>Opens a bar as a wheel for as long as <paramref name="keyCode"/> is held.</summary>
        public void Open(int bar, int keyCode)
        {
            if (IsOpen) return;
            if (hotbar.SlotCount(bar) <= 0) return;

            openBar = bar;
            holdKey = keyCode;
            selected = -1;
            cursor.OpenCursor();
        }

        private void Close(bool cast)
        {
            if (!IsOpen) return;
            int bar = openBar;
            openBar = -1;
            cursor.CloseCursor();
            if (cast && selected >= 0) hotbar.CastSlot(bar, selected);
            selected = -1;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (stage != EnumRenderStage.Ortho || !IsOpen) return;

            if (holdKey >= 0 && holdKey < capi.Input.KeyboardKeyStateRaw.Length && !capi.Input.KeyboardKeyStateRaw[holdKey])
            {
                Close(true);
                return;
            }

            int n = hotbar.SlotCount(openBar);
            var bar = hotbar.BarAt(openBar);
            if (n <= 0 || bar == null) { Close(false); return; }

            float cx = capi.Render.FrameWidth / 2f;
            float cy = capi.Render.FrameHeight / 2f;
            float dx = capi.Input.MouseX - cx;
            float dy = capi.Input.MouseY - cy;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            selected = -1;
            if (dist >= DeadZone)
            {
                // Angle from straight up, clockwise, so the first slot sits at the top.
                double angle = Math.Atan2(dx, -dy);
                if (angle < 0) angle += Math.PI * 2;
                selected = (int)Math.Floor(angle / (Math.PI * 2 / n) + 0.5) % n;
            }

            float icon = bar.SlotSize;
            draw.Circle(cx, cy, Radius + icon * 0.75f, new Vec4f(0f, 0f, 0f, 0.35f));

            var player = capi.World?.Player?.Entity;
            for (int i = 0; i < n; i++)
            {
                double a = i * (Math.PI * 2 / n);
                float ex = cx + (float)Math.Sin(a) * Radius;
                float ey = cy - (float)Math.Cos(a) * Radius;
                bool hot = i == selected;
                float half = icon / 2f;

                draw.Rect(ex - half, ey - half, icon, icon, new Vec4f(0f, 0f, 0f, 0.65f));

                var spell = hotbar.SpellAt(openBar, i);
                if (spell != null)
                {
                    var tex = canrpgclassesModSystem.ClientInstance?.Icons?.GetTex(
                        IconLoader.PathFor(spell.IconName, spell.LocalId));
                    bool ready = player == null || hotbar.IsCastable(player, spell.Id);
                    if (tex != null && tex.TextureId != 0)
                        draw.Icon(tex, ex - half, ey - half, icon, icon, ready ? null : new Vec4f(0.45f, 0.45f, 0.5f, 0.85f));
                    else
                        draw.TextCentered(spell.DisplayName, font, ex, ey, new Vec4f(0.9f, 0.9f, 1f, 1f));

                    if (hot) draw.TextCentered(spell.DisplayName, font, cx, cy, new Vec4f(1f, 0.95f, 0.8f, 1f));
                }

                draw.Frame(ex - half, ey - half, icon, icon, hot ? 3f : 1.5f,
                    hot ? new Vec4f(1f, 0.9f, 0.4f, 1f) : new Vec4f(0.8f, 0.8f, 0.85f, 0.6f));
            }
        }

        public void Dispose()
        {
            Close(false);
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Ortho);
            draw.Dispose();
        }
    }
}
