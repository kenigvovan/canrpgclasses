using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;
using canrpgclasses.Priest;
using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Client.Render
{
    /// <summary>
    /// Always-on spell hotbar HUD: bound spell slots with cooldown sweeps, the class resource bar, combo pips and
    /// the cast bar. A renderer rather than a dialog because everything is placed by the fraction-of-screen
    /// anchors in <see cref="HudLayout"/>, which no retained-mode layout can express.
    /// </summary>
    public class HudSpellHotbarRenderer : IRenderer
    {
        private readonly ICoreClientAPI capi;
        private readonly SpellHotbar hotbar;
        private readonly HudLayout layout;
        private readonly Hud2D draw;

        private readonly CairoFont barFont = CairoFont.WhiteSmallText();
        private readonly CairoFont keyFont = CairoFont.WhiteDetailText();

        // Edit-mode drag: the movable blocks of this frame and which one is being dragged (-1 = none).
        private readonly List<((float X, float Y, float W, float H) Rect, Action<float, float> SetAnchor)> editBlocks = new();
        private int dragging = -1;
        private float dragOffsetX, dragOffsetY;
        private bool wasMouseDown;

        // Cast bar (channelled casts), driven by the network handler.
        private string? castSpellName;
        private double castStartMs;
        private double castEndMs;

        public double RenderOrder => 0.9;
        public int RenderRange => 0;

        public HudSpellHotbarRenderer(ICoreClientAPI capi, SpellHotbar hotbar, HudLayout layout)
        {
            this.capi = capi;
            this.hotbar = hotbar;
            this.layout = layout;
            draw = new Hud2D(capi);
            capi.Event.RegisterRenderer(this, EnumRenderStage.Ortho, "canrpgclasses-hotbar");
        }

        public void ShowCastBar(string spellName, float seconds)
        {
            castSpellName = spellName;
            castStartMs = capi.InWorldEllapsedMilliseconds;
            castEndMs = castStartMs + seconds * 1000.0;
        }

        public void HideCastBar() => castSpellName = null;

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (stage != EnumRenderStage.Ortho) return;

            var player = capi.World?.Player?.Entity;
            if (player == null) return;

            hotbar.RefreshSlots();

            var cooldowns = player.GetBehavior<EBSpellCooldowns>();
            var cfg = layout.For(Core.Talents.TalentState.CurrentClass(player));

            float screenW = capi.Render.FrameWidth;
            float screenH = capi.Render.FrameHeight;

            // The aura stance currently active (synced) - its slot gets a glowing "on" border.
            string activeAura = player.WatchedAttributes.GetString(Core.AttrKeys.ActiveAura, "");

            // Slots are clickable whenever the cursor is free, so the hovered one is highlighted then.
            (int Bar, int Slot) hovered = layout.EditMode || capi.Input.MouseGrabbed
                ? (-1, -1)
                : SlotAt(capi.Input.MouseX, capi.Input.MouseY);

            for (int b = 0; b < hotbar.BarCount; b++)
            {
                var bar = hotbar.BarAt(b);
                if (bar is not { Visible: true, Radial: false }) continue;
                DrawBar(player, b, bar, cooldowns, activeAura, hovered, screenW, screenH);
            }

            if (cfg.ShowResources) DrawResources(player, cfg, screenW, screenH);
            if (cfg.ShowCombo) DrawComboPoints(player, cfg, screenW, screenH);
            DrawCastBar(cfg, screenW, screenH);

            if (layout.EditMode) HandleEditDrag(cfg, screenW, screenH);
            else dragging = -1;
        }

        /// <summary>Top-left corner and overall size of a bar's slot block.</summary>
        private static (float Left, float Top, float W, float H) BarBlock(Bar bar, float screenW, float screenH)
        {
            int n = bar.Slots.Length;
            float size = bar.SlotSize, pad = bar.SlotPadding;
            float w = bar.Vertical ? size : n * size + (n - 1) * pad;
            float h = bar.Vertical ? n * size + (n - 1) * pad : size;
            return (screenW * bar.AnchorX - w / 2f, screenH * bar.AnchorY - h / 2f, w, h);
        }

        private static (float X, float Y) SlotPos(Bar bar, float left, float top, int slot)
        {
            float step = bar.SlotSize + bar.SlotPadding;
            return bar.Vertical ? (left, top + slot * step) : (left + slot * step, top);
        }

        private void DrawBar(EntityPlayer player, int barIndex, Bar bar, EBSpellCooldowns? cooldowns,
            string activeAura, (int Bar, int Slot) hovered, float screenW, float screenH)
        {
            var block = BarBlock(bar, screenW, screenH);
            float size = bar.SlotSize;

            for (int i = 0; i < bar.Slots.Length; i++)
            {
                var (sx, sy) = SlotPos(bar, block.Left, block.Top, i);
                bool isHovered = hovered.Bar == barIndex && hovered.Slot == i;

                draw.Rect(sx, sy, size, size, new Vec4f(0f, 0f, 0f, 0.55f));

                var spell = hotbar.SpellAt(barIndex, i);
                if (spell != null)
                {
                    bool ready = !layout.DimUnavailable || hotbar.IsCastable(player, spell.Id);
                    DrawSpellIcon(sx, sy, size, spell, ready);
                    DrawCooldown(sx, sy, size, spell, cooldowns);
                    if (!string.IsNullOrEmpty(activeAura) && spell.Id == activeAura)
                        draw.Frame(sx - 2, sy - 2, size + 4, size + 4, 2.5f, new Vec4f(0.45f, 1f, 0.55f, 0.95f));

                    if (isHovered)
                    {
                        draw.TextCentered(spell.DisplayName, barFont, sx + size / 2f, sy - 10f,
                            new Vec4f(1f, 0.95f, 0.8f, 1f));
                    }
                }

                if (hotbar.SequenceAt(barIndex, i).Length > 1)
                    draw.Rect(sx + size - 7, sy + size - 7, 5, 5, new Vec4f(1f, 0.8f, 0.3f, 0.9f));

                draw.Frame(sx, sy, size, size, isHovered ? 2.5f : 1.5f,
                    isHovered ? new Vec4f(1f, 0.9f, 0.5f, 0.95f) : new Vec4f(0.8f, 0.8f, 0.85f, 0.6f));

                string key = HotkeyText(barIndex, i);
                if (key.Length > 0) draw.Text(key, keyFont, sx + 3, sy + 1, new Vec4f(0.85f, 0.95f, 1f, 1f));
            }
        }

        /// <summary>Bounds of a length/thickness bar centred on the given anchor fraction - shared by the resource
        /// bar, cast bar and combo block so their drag rects and draw rects always agree.</summary>
        private static (float X, float Y, float W, float H) Block(float screenW, float screenH,
            float anchorX, float anchorY, float length, float thickness, bool vert)
        {
            float cx = screenW * anchorX, cy = screenH * anchorY;
            return vert
                ? (cx - thickness / 2f, cy - length / 2f, thickness, length)
                : (cx - length / 2f, cy - thickness / 2f, length, thickness);
        }

        /// <summary>The channelled-cast progress bar. Horizontal fills left→right; vertical fills bottom→up (same
        /// convention as the resource bar).</summary>
        private void DrawCastBar(HudLayoutConfig cfg, float screenW, float screenH)
        {
            if (castSpellName == null) return;
            double now = capi.InWorldEllapsedMilliseconds;
            if (now >= castEndMs) { castSpellName = null; return; }

            float frac = (float)Math.Clamp((now - castStartMs) / (castEndMs - castStartMs), 0.0, 1.0);
            var b = Block(screenW, screenH, cfg.CastAnchorX, cfg.CastAnchorY, cfg.CastWidth, cfg.CastThickness, cfg.CastVertical);

            draw.Rect(b.X, b.Y, b.W, b.H, new Vec4f(0f, 0f, 0f, 0.6f));
            if (cfg.CastVertical) draw.Rect(b.X, b.Y + b.H * (1 - frac), b.W, b.H * frac, new Vec4f(0.55f, 0.75f, 0.95f, 0.9f));
            else draw.Rect(b.X, b.Y, b.W * frac, b.H, new Vec4f(0.55f, 0.75f, 0.95f, 0.9f));
            draw.Frame(b.X, b.Y, b.W, b.H, 1.5f, new Vec4f(0.85f, 0.85f, 0.9f, 0.7f));

            draw.TextCentered(castSpellName, barFont, b.X + b.W / 2, b.Y + b.H / 2, new Vec4f(0.95f, 0.95f, 1f, 1f));
        }

        /// <summary>The class resource bar (energy/rage/mana). Horizontal fills left→right; vertical bottom→up.
        /// Combo points are a separate, independently-placed block - see <see cref="DrawComboPoints"/>.</summary>
        private void DrawResources(EntityPlayer player, HudLayoutConfig cfg, float screenW, float screenH)
        {
            var pool = ResourceState.PrimaryPool(player);
            if (pool == null) return;

            var b = Block(screenW, screenH, cfg.ResAnchorX, cfg.ResAnchorY, cfg.ResWidth, cfg.ResThickness, cfg.ResVertical);

            float max = ResourceState.EffectiveMax(player, pool);
            float val = ResourceState.Get(player, pool);
            float frac = max > 0 ? Math.Clamp(val / max, 0f, 1f) : 0f;

            var bg = new Vec4f(0f, 0f, 0f, 0.55f);
            var border = new Vec4f(0.8f, 0.8f, 0.85f, 0.6f);
            var fill = new Vec4f(pool.ColorR, pool.ColorG, pool.ColorB, 0.9f);

            draw.Rect(b.X, b.Y, b.W, b.H, bg);
            if (cfg.ResVertical) draw.Rect(b.X, b.Y + b.H * (1 - frac), b.W, b.H * frac, fill);
            else draw.Rect(b.X, b.Y, b.W * frac, b.H, fill);
            draw.Frame(b.X, b.Y, b.W, b.H, 1f, border);

            draw.TextCentered($"{(int)val}/{(int)max}", barFont, b.X + b.W / 2, b.Y + b.H / 2, new Vec4f(0.95f, 0.95f, 1f, 1f));

            // Druid form pools: a second thin bar under the primary (mana). Bear shows RAGE, Cat shows ENERGY -
            // mutually exclusive (one form at a time). Both are secondary pools whose value syncs via
            // WatchedAttributes; the pool defs are read statically (no client registry needed).
            if (Core.Talents.TalentState.CurrentClass(player) != Druid.DruidRage.ClassId) return;

            string aura = player.WatchedAttributes.GetString(Core.AttrKeys.ActiveAura, "");
            bool inBear = aura == Druid.DruidEffectIds.UrsineFormSpellId;
            bool inCat = aura == Druid.DruidEffectIds.FelineFormSpellId;

            var rp = Druid.DruidRage.RagePool;
            float rval = ResourceState.Get(player, rp);
            var ep = Druid.DruidEnergy.EnergyPool;
            float eval = ResourceState.Get(player, ep);

            ResourcePoolDef? formPool = null;
            float formVal = 0f;
            if (inBear) { formPool = rp; formVal = rval; }
            else if (inCat) { formPool = ep; formVal = eval; }
            else if (rval > 0f) { formPool = rp; formVal = rval; }
            if (formPool == null) return;

            float pmax = ResourceState.EffectiveMax(player, formPool);
            float pfrac = pmax > 0 ? Math.Clamp(formVal / pmax, 0f, 1f) : 0f;
            float sy = b.Y + b.H + b.H * 0.25f + 2f;
            float sh = b.H * 0.6f;
            draw.Rect(b.X, sy, b.W, sh, bg);
            draw.Rect(b.X, sy, b.W * pfrac, sh, new Vec4f(formPool.ColorR, formPool.ColorG, formPool.ColorB, 0.9f));
            draw.Frame(b.X, sy, b.W, sh, 1f, border);
        }

        /// <summary>Client-side tier of an effectshud effect on the player. The server-authoritative GetEffectTier
        /// reads the server-only activeEffects dict; on the client the synced state lives in
        /// onlyClientsActiveEffects, so the HUD must read there. 0 when the effect isn't present.</summary>
        private static int ClientEffectTier(EntityPlayer player, string effectId)
        {
            var eb = player.GetBehavior<EBEffects>();
            if (eb?.onlyClientsActiveEffects != null && eb.onlyClientsActiveEffects.TryGetValue(effectId, out var d)) return d.tier;
            return 0;
        }

        /// <summary>Combo-point pips at their own anchor, independent of wherever the resource bar sits. The priest
        /// shows Shadow Orbs in the same block, the shaman Storm Charge.</summary>
        private void DrawComboPoints(EntityPlayer player, HudLayoutConfig cfg, float screenW, float screenH)
        {
            var cls = ResourceState.CurrentClass(player);

            int filled, maxN;
            Vec4f full;
            if (cls?.UsesComboPoints == true)
            {
                filled = ResourceState.Combo(player);
                maxN = ResourceState.ComboMax;
                full = new Vec4f(0.95f, 0.25f, 0.2f, 1f); // combo red
            }
            else if (cls?.Id == "priest")
            {
                // Shadow Orbs ride the same pip block, but - unlike the always-shown combo pips - only appear once
                // at least one orb is banked.
                maxN = ShadowOrbs.Cap;
                filled = Math.Min(maxN, Math.Max(0, ClientEffectTier(player, PriestEffectIds.ShadowOrbs)));
                if (filled <= 0) return;
                full = new Vec4f(0.62f, 0.40f, 0.92f, 1f); // shadow violet
            }
            else if (cls?.Id == "shaman")
            {
                // Storm Charge: hidden until the first stack lands, full pips = the next Arc Bolt / Forked
                // Lightning / Mending Wave is instant.
                maxN = Shaman.MaelstromEffect.Cap;
                filled = Math.Min(maxN, Math.Max(0, ClientEffectTier(player, Shaman.ShamanEffectIds.Maelstrom)));
                if (filled <= 0) return;
                full = new Vec4f(0.2f, 0.75f, 0.85f, 1f); // shaman teal
            }
            else return;

            float pipR = cfg.ComboPipRadius;
            float spacing = cfg.ComboPipSpacing;
            float span = Math.Max(0, maxN - 1) * spacing;
            float cx = screenW * cfg.ComboAnchorX;
            float cy = screenH * cfg.ComboAnchorY;

            var empty = new Vec4f(0.15f, 0.15f, 0.15f, 0.7f);
            var border = new Vec4f(0.85f, 0.85f, 0.9f, 0.7f);
            for (int i = 0; i < maxN; i++)
            {
                float px = cfg.ComboVertical ? cx : cx - span / 2f + i * spacing;
                float py = cfg.ComboVertical ? cy - span / 2f + i * spacing : cy;
                draw.Circle(px, py, pipR, i < filled ? full : empty);
                draw.Ring(px, py, pipR, border);
            }
        }

        /// <summary>The bar and slot under a screen point, or (-1, -1). Used by the click and drop handlers.</summary>
        public (int Bar, int Slot) SlotAt(float mx, float my)
        {
            float screenW = capi.Render.FrameWidth, screenH = capi.Render.FrameHeight;

            for (int b = 0; b < hotbar.BarCount; b++)
            {
                var bar = hotbar.BarAt(b);
                if (bar is not { Visible: true, Radial: false }) continue;

                var block = BarBlock(bar, screenW, screenH);
                for (int i = 0; i < bar.Slots.Length; i++)
                {
                    var (sx, sy) = SlotPos(bar, block.Left, block.Top, i);
                    if (mx >= sx && mx <= sx + bar.SlotSize && my >= sy && my <= sy + bar.SlotSize) return (b, i);
                }
            }
            return (-1, -1);
        }

        private void DrawSpellIcon(float x, float y, float size, Spell spell, bool ready = true)
        {
            var tint = ready ? null : new Vec4f(0.45f, 0.45f, 0.5f, 0.85f);
            var tex = canrpgclassesModSystem.ClientInstance?.Icons?.GetTex(
                IconLoader.PathFor(spell.IconName, spell.LocalId));
            if (tex != null && tex.TextureId != 0)
            {
                draw.Icon(tex, x, y, size, size, tint);
                return;
            }

            // Placeholder: school-tinted fill + abbreviated name until a real icon exists.
            var school = SchoolColor(spell.School);
            draw.Rect(x + 2, y + 2, size - 4, size - 4, new Vec4f(school.R, school.G, school.B, 0.45f));
            draw.TextCentered(Abbreviate(spell.DisplayName), barFont, x + size / 2, y + size / 2,
                new Vec4f(0.95f, 0.9f, 1f, 1f));
        }

        private void DrawCooldown(float x, float y, float size, Spell spell, EBSpellCooldowns? cooldowns)
        {
            if (cooldowns == null) return;
            float remaining = cooldowns.RemainingSeconds(spell.CooldownKey);
            float total = cooldowns.TotalSeconds(spell.CooldownKey);

            // The global cooldown sweeps every GCD-bound slot too. The longer of the two wins, and the sweep's
            // total comes from whichever one is being drawn.
            if (spell.TriggersGlobalCooldown)
            {
                float gcdRemaining = cooldowns.RemainingSeconds(Spell.GlobalCooldownKey);
                if (gcdRemaining > remaining)
                {
                    remaining = gcdRemaining;
                    total = cooldowns.TotalSeconds(Spell.GlobalCooldownKey);
                }
            }
            if (remaining <= 0) return;

            float fraction = total > 0 ? Math.Min(1f, remaining / total) : 1f;
            draw.CooldownSweep(x, y, size, 1f - fraction, new Vec4f(0f, 0f, 0f, 0.6f));
        }

        /// <summary>While the HUD settings panel is open: outline every movable block and let the player drag it to
        /// a new anchor. Blocks are collected into a list rather than switched on by index, so any number of bars
        /// works. The cursor is free (a dialog is open), so raw mouse state is the right input here.</summary>
        private void HandleEditDrag(HudLayoutConfig cfg, float screenW, float screenH)
        {
            float mx = capi.Input.MouseX, my = capi.Input.MouseY;
            bool down = capi.Input.MouseButton.Left;

            editBlocks.Clear();

            for (int b = 0; b < hotbar.BarCount; b++)
            {
                var bar = hotbar.BarAt(b);
                if (bar is not { Visible: true, Radial: false }) continue;
                var block = BarBlock(bar, screenW, screenH);
                editBlocks.Add(((block.Left, block.Top, block.W, block.H),
                    (fx, fy) => { bar.AnchorX = fx; bar.AnchorY = fy; }));
            }

            if (cfg.ShowResources)
                editBlocks.Add((Block(screenW, screenH, cfg.ResAnchorX, cfg.ResAnchorY, cfg.ResWidth, cfg.ResThickness, cfg.ResVertical),
                    (fx, fy) => { cfg.ResAnchorX = fx; cfg.ResAnchorY = fy; }));

            editBlocks.Add((Block(screenW, screenH, cfg.CastAnchorX, cfg.CastAnchorY, cfg.CastWidth, cfg.CastThickness, cfg.CastVertical),
                (fx, fy) => { cfg.CastAnchorX = fx; cfg.CastAnchorY = fy; }));

            if (cfg.ShowCombo)
            {
                float comboSpan = Math.Max(0f, (ResourceState.ComboMax - 1) * cfg.ComboPipSpacing);
                editBlocks.Add((Block(screenW, screenH, cfg.ComboAnchorX, cfg.ComboAnchorY,
                        comboSpan + cfg.ComboPipRadius * 2f, cfg.ComboPipRadius * 2f, cfg.ComboVertical),
                    (fx, fy) => { cfg.ComboAnchorX = fx; cfg.ComboAnchorY = fy; }));
            }

            if (dragging >= editBlocks.Count) dragging = -1;

            var hi = new Vec4f(1f, 0.85f, 0.25f, 0.95f);
            var norm = new Vec4f(0.55f, 0.8f, 1f, 0.75f);
            int over = -1;
            for (int i = 0; i < editBlocks.Count; i++)
                if (In(mx, my, editBlocks[i].Rect)) { over = i; break; }

            for (int i = 0; i < editBlocks.Count; i++)
                Outline(editBlocks[i].Rect, (dragging == i || (over == i && dragging < 0)) ? hi : norm);

            if (dragging < 0 && down && !wasMouseDown && over >= 0)
            {
                dragging = over;
                Grab(editBlocks[over].Rect, mx, my);
            }

            if (dragging >= 0)
            {
                if (down)
                    editBlocks[dragging].SetAnchor(
                        Math.Clamp((mx + dragOffsetX) / screenW, 0f, 1f),
                        Math.Clamp((my + dragOffsetY) / screenH, 0f, 1f));
                else
                {
                    dragging = -1;
                    layout.Save();
                    hotbar.SaveBars();
                }
            }

            wasMouseDown = down;
        }

        private void Grab((float X, float Y, float W, float H) b, float mx, float my)
        {
            dragOffsetX = b.X + b.W / 2f - mx;
            dragOffsetY = b.Y + b.H / 2f - my;
        }

        private void Outline((float X, float Y, float W, float H) b, Vec4f color)
            => draw.Frame(b.X - 3, b.Y - 3, b.W + 6, b.H + 6, 2f, color);

        private static bool In(float x, float y, (float X, float Y, float W, float H) b)
            => x >= b.X && x <= b.X + b.W && y >= b.Y && y <= b.Y + b.H;

        /// <summary>The key currently bound to this slot's cast hotkey, or nothing when it has none.</summary>
        private string HotkeyText(int bar, int slot)
        {
            if (bar < 0 || bar >= SpellHotbar.HotkeyCodes.Length) return "";
            if (slot < 0 || slot >= SpellHotbar.HotkeyCodes[bar].Length) return "";

            var hk = capi.Input?.GetHotKeyByCode(SpellHotbar.HotkeyCodes[bar][slot]);
            if (hk?.CurrentMapping == null || hk.CurrentMapping.KeyCode == (int)GlKeys.Unknown) return "";
            return hk.CurrentMapping.PrimaryAsString() ?? "";
        }

        private static (float R, float G, float B) SchoolColor(SpellSchool school) => school switch
        {
            SpellSchool.PhysicalMelee => (0.85f, 0.35f, 0.30f),
            SpellSchool.PhysicalRanged => (0.80f, 0.65f, 0.30f),
            SpellSchool.Fire => (0.95f, 0.45f, 0.15f),
            SpellSchool.Frost => (0.40f, 0.75f, 0.95f),
            SpellSchool.Arcane => (0.70f, 0.40f, 0.90f),
            SpellSchool.Holy => (0.95f, 0.90f, 0.55f),
            SpellSchool.Nature => (0.40f, 0.80f, 0.40f),
            SpellSchool.Shadow => (0.45f, 0.35f, 0.55f),
            _ => (0.6f, 0.6f, 0.6f)
        };

        private static string Abbreviate(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            var parts = name.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return char.ToUpperInvariant(parts[0][0]).ToString() + char.ToUpperInvariant(parts[1][0]);
            return name.Length >= 3 ? name.Substring(0, 3) : name;
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Ortho);
            draw.Dispose();
        }
    }
}
