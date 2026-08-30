using System;
using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace canrpgclasses.Client.Gui
{
    /// <summary>
    /// Grid of icon cells with hover, click and drag&amp;drop between two grids (spellbook, talent tree).
    /// Modelled on vanilla's <c>GuiElementSkillItemGrid</c>, but nothing is baked per cell into the static layer -
    /// the reusable pieces are tinted per cell at draw time, so a change of contents never needs a recompose.
    /// </summary>
    public class GuiElementIconGrid : GuiElement
    {
        /// <summary>How far the cursor must travel with the button down before it counts as a drag rather than a
        /// click. Below this, a slightly shaky click still selects the cell.</summary>
        private const int DragThresholdPx = 4;

        private const long DoubleClickMs = 400;

        public override bool Focusable => true;

        private readonly int cols;
        private readonly int rows;
        private readonly double unscaledCellSize;
        private readonly double unscaledLabelHeight;
        private readonly double unscaledPadding;

        private LoadedTexture plateTexture;
        private LoadedTexture hoverTexture;
        private LoadedTexture borderTexture;
        private LoadedTexture badgeTexture;
        private readonly Dictionary<string, LoadedTexture> textCache = new();

        /// <summary>Captions wrapped to the cell width, cached because measuring text is a Cairo round trip and
        /// this would otherwise run for every visible cell every frame.</summary>
        private readonly Dictionary<string, string[]> labelCache = new();

        /// <summary>A caption longer than two lines is cut rather than pushed into the row below.</summary>
        private const int MaxLabelLines = 2;

        /// <summary>Cells in reading order (row-major), length cols*rows. Short lists are fine - missing entries
        /// simply aren't drawn. The owner replaces this list whenever its data changes.</summary>
        public List<IconGridCell> Cells = new();

        /// <summary>Cell drawn with the selected border, or -1.</summary>
        public int SelectedIndex = -1;

        /// <summary>Cell under the cursor this frame, or -1. Refreshed in the render pass.</summary>
        public int HoverIndex { get; private set; } = -1;

        public CairoFont LabelFont = CairoFont.WhiteDetailText();

        /// <summary>Badge text. Deliberately unstroked: the generated text texture is sized from the glyph extents
        /// alone, so a stroke bleeds past the right edge and the last character gets clipped ("0/1" → "0/").
        /// Readability comes from the dark plate behind it instead.</summary>
        public CairoFont BadgeFont = CairoFont.WhiteDetailText();

        public double[] DefaultFrameColor = { 0.80, 0.80, 0.85, 0.60 };

        public double[] DefaultPlateColor = { 0.10, 0.10, 0.12, 0.75 };

        public double[] BadgeBackColor = { 0.02, 0.02, 0.03, 0.72 };

        /// <summary>Put the badge in the gutter under the cell instead of its top-left corner - the talent tree's
        /// rank counter belongs there, where it never covers the icon. Right-aligned to the cell so it stays clear
        /// of the dependency arrow, which runs down the middle.</summary>
        public bool BadgeBelow;

        /// <summary>Extra static drawing behind the cells, given the Cairo context and a lookup from cell index to
        /// that cell's rectangle. The talent tree draws its dependency arrows here.
        ///
        /// Runs during <see cref="ComposeElements"/>, so <see cref="Cells"/> must already be filled when the
        /// composer is composed - and re-drawing it means recomposing (which is fine: the arrows only change when
        /// a talent point is spent).</summary>
        public Action<Context, System.Func<int, (double X, double Y, double W, double H)>>? OnComposeBackground;

        public Action<int, EnumMouseButton>? OnCellClick;
        public Action<int>? OnCellDoubleClick;

        /// <summary>Called every frame with the hovered cell index (or -1) - this is where the owner drives the
        /// tooltip.</summary>
        public Action<int>? OnCellHover;

        /// <summary>Shared with the other grid in the same dialog. Null disables dragging entirely.</summary>
        public IconDragState? Drag;

        /// <summary>Whether a cell can start a drag (false for the hotbar-slot grid's empty slots).</summary>
        public System.Func<int, bool>? CanDragFrom;

        /// <summary>Whether this grid accepts drops at all.</summary>
        public bool AcceptsDrop;

        /// <summary>Drop happened: (target cell index, dragged id).</summary>
        public Action<int, string>? OnDrop;

        private int pressedIndex = -1;
        private int pressX, pressY;
        private long lastClickMs;
        private int lastClickIndex = -1;

        public GuiElementIconGrid(ICoreClientAPI capi, ElementBounds bounds, int cols, int rows,
            double cellSize, double labelHeight = 0, double padding = 4) : base(capi, bounds)
        {
            this.cols = cols;
            this.rows = rows;
            unscaledCellSize = cellSize;
            unscaledLabelHeight = labelHeight;
            unscaledPadding = padding;

            plateTexture = new LoadedTexture(capi);
            hoverTexture = new LoadedTexture(capi);
            borderTexture = new LoadedTexture(capi);
            badgeTexture = new LoadedTexture(capi);

            Bounds.fixedWidth = cols * (cellSize + padding);
            Bounds.fixedHeight = rows * (cellSize + labelHeight + padding);
        }

        /// <summary>Cell index at a screen position, or -1. Public so a dialog can hit-test the other grid on a
        /// drop.</summary>
        public int CellAt(int mouseX, int mouseY)
        {
            double stepX = scaled(unscaledCellSize + unscaledPadding);
            double stepY = scaled(unscaledCellSize + unscaledLabelHeight + unscaledPadding);
            double dx = mouseX - Bounds.absX;
            double dy = mouseY - Bounds.absY;
            if (dx < 0 || dy < 0) return -1;

            int col = (int)(dx / stepX);
            int row = (int)(dy / stepY);
            if (col < 0 || col >= cols || row < 0 || row >= rows) return -1;

            // Inside the cell proper, not the padding gutter after it.
            if (dx - col * stepX > scaled(unscaledCellSize)) return -1;

            int index = row * cols + col;
            if (index >= Cells.Count || Cells[index] == null || Cells[index].Hidden) return -1;
            return index;
        }

        public override void ComposeElements(Context ctxStatic, ImageSurface surface)
        {
            Bounds.CalcWorldBounds();
            double size = scaled(unscaledCellSize);

            // Slot plate: the recessed square every cell sits in. Baked white so a cell can tint it to show its
            // state (a taken talent reads as blue at a glance, not just by its border).
            BakeCellTexture(ref plateTexture, size, ctx =>
            {
                ctx.SetSourceRGBA(1, 1, 1, 1);
                RoundRectangle(ctx, 0, 0, size, size, GuiStyle.ElementBGRadius);
                ctx.Fill();
                EmbossRoundRectangleElement(ctx, 0, 0, size, size, true, 2, -1);
            });

            // Backdrop for the corner badge - a hotkey letter or "2/3" drawn straight onto a bright icon is
            // unreadable without something behind it.
            BakeCellTexture(ref badgeTexture, size, ctx =>
            {
                ctx.SetSourceRGBA(1, 1, 1, 1);
                RoundRectangle(ctx, 0, 0, size, size, GuiStyle.ElementBGRadius);
                ctx.Fill();
            });

            // Hover: a bright outline plus the faintest wash. A solid wash (what this used to be) washed the icon
            // out and made it harder to tell what you were pointing at.
            BakeCellTexture(ref hoverTexture, size, ctx =>
            {
                ctx.SetSourceRGBA(1, 1, 1, 0.10);
                RoundRectangle(ctx, 0, 0, size, size, GuiStyle.ElementBGRadius);
                ctx.Fill();
                ctx.SetSourceRGBA(1, 1, 1, 0.85);
                ctx.LineWidth = 2.0;
                RoundRectangle(ctx, 1, 1, size - 2, size - 2, GuiStyle.ElementBGRadius);
                ctx.Stroke();
            });

            BakeCellTexture(ref borderTexture, size, ctx =>
            {
                ctx.SetSourceRGBA(1, 1, 1, 1);
                ctx.LineWidth = 2.0;
                RoundRectangle(ctx, 1, 1, size - 2, size - 2, GuiStyle.ElementBGRadius);
                ctx.Stroke();
            });

            OnComposeBackground?.Invoke(ctxStatic, CellRectDraw);
        }

        /// <summary>Rectangle of a cell in the static surface's coordinate space.</summary>
        private (double X, double Y, double W, double H) CellRectDraw(int index)
        {
            double size = scaled(unscaledCellSize);
            double stepX = scaled(unscaledCellSize + unscaledPadding);
            double stepY = scaled(unscaledCellSize + unscaledLabelHeight + unscaledPadding);
            return (Bounds.drawX + (index % cols) * stepX, Bounds.drawY + (index / cols) * stepY, size, size);
        }

        private void BakeCellTexture(ref LoadedTexture into, double size, Action<Context> draw)
        {
            var surface = new ImageSurface(Format.Argb32, (int)size, (int)size);
            var ctx = genContext(surface);
            draw(ctx);
            generateTexture(surface, ref into, true);
            ctx.Dispose();
            surface.Dispose();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            double size = scaled(unscaledCellSize);
            double stepX = scaled(unscaledCellSize + unscaledPadding);
            double stepY = scaled(unscaledCellSize + unscaledLabelHeight + unscaledPadding);

            HoverIndex = CellAt(api.Input.MouseX, api.Input.MouseY);

            for (int i = 0; i < Cells.Count && i < cols * rows; i++)
            {
                var cell = Cells[i];
                if (cell == null || cell.Hidden) continue;

                double x = Bounds.renderX + (i % cols) * stepX;
                double y = Bounds.renderY + (i / cols) * stepY;

                api.Render.Render2DTexture(plateTexture.TextureId, (float)x, (float)y, (float)size, (float)size, 50f,
                    ToVec(cell.PlateColor ?? DefaultPlateColor));

                if (!cell.IsEmpty)
                {
                    if (cell.Icon != null && cell.Icon.TextureId != 0)
                    {
                        // Inset enough that the tinted plate shows as a border around the art - at 2px the icon
                        // covered the plate and the cell's state was only visible at the outline.
                        double inset = scaled(5);
                        api.Render.Render2DTexture(cell.Icon.TextureId, (float)(x + inset), (float)(y + inset),
                            (float)(size - inset * 2), (float)(size - inset * 2), 50f, ToVec(cell.IconTint));
                    }
                    else if (!string.IsNullOrEmpty(cell.IconFallback))
                    {
                        DrawTextCentered(cell.IconFallback!, LabelFont, x + size / 2, y + size / 2, cell.IconTint);
                    }
                }

                if (i == HoverIndex)
                    api.Render.Render2DTexture(hoverTexture.TextureId, (float)x, (float)y, (float)size, (float)size, 50f);

                var frameColor = ToVec(cell.FrameColor ?? DefaultFrameColor);
                api.Render.Render2DTexture(borderTexture.TextureId, (float)x, (float)y, (float)size, (float)size, 50f,
                    frameColor);
                if (cell.Emphasize)
                    api.Render.Render2DTexture(borderTexture.TextureId, (float)(x - 1), (float)(y - 1),
                        (float)(size + 2), (float)(size + 2), 50f, frameColor);

                if (!string.IsNullOrEmpty(cell.Badge)) DrawBadge(cell.Badge!, x, y, size);

                if (!string.IsNullOrEmpty(cell.Label) && unscaledLabelHeight > 0)
                    DrawLabel(cell.Label!, x, y + size, size, scaled(unscaledLabelHeight));
            }

            OnCellHover?.Invoke(HoverIndex);

            // The dragged icon follows the cursor, drawn by one grid only so it keeps rendering while the cursor is
            // over the other one - the source by default, or whoever the dialog nominated (a source inside a clip
            // region cannot draw outside it).
            if (Drag is { Active: true } drag && ReferenceEquals(drag.GhostRenderer ?? drag.Source, this))
            {
                if (drag.Released) { drag.Clear(); }
                else if (drag.Icon != null && drag.Icon.TextureId != 0)
                {
                    float ghost = (float)scaled(unscaledCellSize * 0.6);
                    // Up and to the left of the cursor: below-right put the ghost straight over the cell being
                    // aimed at, hiding the drop target under the thing you're dropping.
                    // Above the scrollbar handle (200) so the ghost stays visible while dragging past a scrollable
                    // list, but below the tooltip (500).
                    api.Render.Render2DTexture(drag.Icon.TextureId, api.Input.MouseX - ghost - 8,
                        api.Input.MouseY - ghost - 8, ghost, ghost, 300f);
                }
            }
        }

        private LoadedTexture? TextTex(string text, CairoFont font)
        {
            string key = font.UnscaledFontsize + " " + text;
            if (textCache.TryGetValue(key, out var hit)) return hit;
            LoadedTexture tex;
            try { tex = api.Gui.TextTexture.GenTextTexture(text, font); }
            catch { return null; }
            textCache[key] = tex;
            return tex;
        }

        private void DrawText(string text, CairoFont font, double x, double y, double[]? color)
        {
            var tex = TextTex(text, font);
            if (tex == null) return;
            api.Render.Render2DTexture(tex.TextureId, (float)x, (float)y, tex.Width, tex.Height, 51f, ToVec(color));
        }

        private void DrawBadge(string text, double cellX, double cellY, double size)
        {
            var tex = TextTex(text, BadgeFont);
            if (tex == null) return;

            double padX = scaled(3), padY = scaled(1);
            double w = tex.Width + padX * 2, h = tex.Height + padY * 2;

            // Below the cell it hugs the right edge - never wider than the cell, so it cannot reach into the
            // neighbouring column, and clear of the dependency arrow that runs down the middle.
            double x = BadgeBelow ? cellX + Math.Max(0, size - w) : cellX + scaled(2);
            double y = BadgeBelow ? cellY + size + scaled(1) : cellY + scaled(2);

            api.Render.Render2DTexture(badgeTexture.TextureId, (float)x, (float)y, (float)w, (float)h, 51f,
                ToVec(BadgeBackColor));
            api.Render.Render2DTexture(tex.TextureId, (float)(x + padX), (float)(y + padY), tex.Width, tex.Height, 52f);
        }

        /// <summary>The caption under a cell, wrapped to the cell's width. A spell name is routinely wider than
        /// its icon, and drawn as one centred line it ran straight over the neighbouring cells' captions.</summary>
        private void DrawLabel(string text, double cellX, double labelY, double cellWidth, double labelHeight)
        {
            if (!labelCache.TryGetValue(text, out var lines))
            {
                var wrapped = api.Gui.Text.Lineize(LabelFont, text, cellWidth);
                lines = new string[Math.Min(MaxLabelLines, wrapped.Length)];
                for (int i = 0; i < lines.Length; i++) lines[i] = wrapped[i].Text.TrimEnd();
                labelCache[text] = lines;
            }

            double lineH = api.Gui.Text.GetLineHeight(LabelFont);
            double y = labelY + Math.Max(0, (labelHeight - lines.Length * lineH) / 2);
            foreach (var line in lines)
            {
                DrawTextCentered(line, LabelFont, cellX + cellWidth / 2, y + lineH / 2, null);
                y += lineH;
            }
        }

        private void DrawTextCentered(string text, CairoFont font, double cx, double cy, double[]? color)
        {
            var tex = TextTex(text, font);
            if (tex == null) return;
            DrawText(text, font, cx - tex.Width / 2.0, cy - tex.Height / 2.0, color);
        }

        private static Vec4f? ToVec(double[]? c)
            => c == null ? null : new Vec4f((float)c[0], (float)c[1], (float)c[2], (float)c[3]);

        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            base.OnMouseDownOnElement(api, args);

            int index = CellAt(args.X, args.Y);
            if (index < 0) return;

            if (args.Button == EnumMouseButton.Left)
            {
                pressedIndex = index;
                pressX = args.X;
                pressY = args.Y;

                long now = api.ElapsedMilliseconds;
                if (index == lastClickIndex && now - lastClickMs < DoubleClickMs)
                {
                    lastClickIndex = -1;
                    OnCellDoubleClick?.Invoke(index);
                    args.Handled = true;
                    return;
                }
                lastClickMs = now;
                lastClickIndex = index;
            }

            OnCellClick?.Invoke(index, args.Button);
            args.Handled = true;
        }

        public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
        {
            base.OnMouseMove(api, args);
            if (Drag == null || pressedIndex < 0 || Drag.Active) return;

            if (Math.Abs(args.X - pressX) < DragThresholdPx && Math.Abs(args.Y - pressY) < DragThresholdPx) return;
            if (CanDragFrom != null && !CanDragFrom(pressedIndex)) { pressedIndex = -1; return; }

            var cell = pressedIndex < Cells.Count ? Cells[pressedIndex] : null;
            if (cell == null || cell.IsEmpty) { pressedIndex = -1; return; }

            Drag.Begin(this, cell.Id!, cell.Icon);
        }

        // Delivered to every element in the composer, not just the one under the cursor - which is exactly what a
        // cross-element drop needs.
        public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
        {
            base.OnMouseUp(api, args);
            pressedIndex = -1;

            if (Drag is not { Active: true } drag) return;

            if (AcceptsDrop && !drag.Consumed && !string.IsNullOrEmpty(drag.Id))
            {
                int index = CellAt(args.X, args.Y);
                if (index >= 0)
                {
                    drag.Consumed = true;
                    OnDrop?.Invoke(index, drag.Id!);
                }
            }

            drag.Released = true;
        }

        public override void Dispose()
        {
            base.Dispose();
            plateTexture?.Dispose();
            hoverTexture?.Dispose();
            borderTexture?.Dispose();
            badgeTexture?.Dispose();
            foreach (var tex in textCache.Values) tex.Dispose();
            textCache.Clear();
            labelCache.Clear();
        }
    }
}
