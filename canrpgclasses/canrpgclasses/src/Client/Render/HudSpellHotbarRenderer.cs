using System;
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

        // Edit-mode drag state: 0 = none, 1 = slot block, 2 = resource block, 3 = cast bar, 4 = combo points.
        private int draggingBlock;
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

            int n = hotbar.SlotCount;
            float size = cfg.SlotSize, pad = cfg.SlotPadding;
            float blockW = cfg.Vertical ? size : n * size + (n - 1) * pad;
            float blockH = cfg.Vertical ? n * size + (n - 1) * pad : size;
            float left = screenW * cfg.SlotAnchorX - blockW / 2f;
            float top = screenH * cfg.SlotAnchorY - blockH / 2f;

            // The aura stance currently active (synced) - its slot gets a glowing "on" border.
            string activeAura = player.WatchedAttributes.GetString(Core.AttrKeys.ActiveAura, "");

            for (int i = 0; i < n; i++)
            {
                float sx = cfg.Vertical ? left : left + i * (size + pad);
                float sy = cfg.Vertical ? top + i * (size + pad) : top;

                draw.Rect(sx, sy, size, size, new Vec4f(0f, 0f, 0f, 0.55f));

                var spell = hotbar.SpellAt(i);
                if (spell != null)
                {
                    DrawSpellIcon(sx, sy, size, spell);
                    DrawCooldown(sx, sy, size, spell, cooldowns);
                    if (!string.IsNullOrEmpty(activeAura) && spell.Id == activeAura)
                        draw.Frame(sx - 2, sy - 2, size + 4, size + 4, 2.5f, new Vec4f(0.45f, 1f, 0.55f, 0.95f));
                }

                draw.Frame(sx, sy, size, size, 1.5f, new Vec4f(0.8f, 0.8f, 0.85f, 0.6f));

                string key = HotkeyText(i);
                if (key.Length > 0) draw.Text(key, keyFont, sx + 3, sy + 1, new Vec4f(0.85f, 0.95f, 1f, 1f));
            }

            if (cfg.ShowResources) DrawResources(player, cfg, screenW, screenH);
            if (cfg.ShowCombo) DrawComboPoints(player, cfg, screenW, screenH);
            DrawCastBar(cfg, screenW, screenH);

            if (layout.EditMode) HandleEditDrag(cfg, screenW, screenH, left, top, blockW, blockH);
            else draggingBlock = 0;
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

        private void DrawSpellIcon(float x, float y, float size, Spell spell)
        {
            var tex = canrpgclassesModSystem.ClientInstance?.Icons?.GetTex(
                IconLoader.PathFor(spell.IconName, spell.LocalId));
            if (tex != null && tex.TextureId != 0)
            {
                draw.Icon(tex, x, y, size, size);
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

        /// <summary>While the HUD settings panel is open: outline the four blocks and let the player drag each to a
        /// new anchor. The cursor is free (a dialog is open), so raw mouse state is the right input here.
        /// dragOffset keeps the grab point so the block doesn't jump under the cursor.</summary>
        private void HandleEditDrag(HudLayoutConfig cfg, float screenW, float screenH,
            float slotLeft, float slotTop, float blockW, float blockH)
        {
            float mx = capi.Input.MouseX, my = capi.Input.MouseY;
            bool down = capi.Input.MouseButton.Left;

            var slots = (X: slotLeft, Y: slotTop, W: blockW, H: blockH);
            var res = Block(screenW, screenH, cfg.ResAnchorX, cfg.ResAnchorY, cfg.ResWidth, cfg.ResThickness, cfg.ResVertical);
            var cast = Block(screenW, screenH, cfg.CastAnchorX, cfg.CastAnchorY, cfg.CastWidth, cfg.CastThickness, cfg.CastVertical);

            float comboSpan = Math.Max(0f, (ResourceState.ComboMax - 1) * cfg.ComboPipSpacing);
            float comboLen = comboSpan + cfg.ComboPipRadius * 2f;
            float comboThick = cfg.ComboPipRadius * 2f;
            var combo = Block(screenW, screenH, cfg.ComboAnchorX, cfg.ComboAnchorY, comboLen, comboThick, cfg.ComboVertical);

            bool overSlots = In(mx, my, slots);
            bool overRes = cfg.ShowResources && In(mx, my, res);
            bool overCast = In(mx, my, cast);
            bool overCombo = cfg.ShowCombo && In(mx, my, combo);

            var hi = new Vec4f(1f, 0.85f, 0.25f, 0.95f);
            var norm = new Vec4f(0.55f, 0.8f, 1f, 0.75f);
            Outline(slots, (draggingBlock == 1 || (overSlots && draggingBlock == 0)) ? hi : norm);
            if (cfg.ShowResources) Outline(res, (draggingBlock == 2 || (overRes && draggingBlock == 0)) ? hi : norm);
            Outline(cast, (draggingBlock == 3 || (overCast && draggingBlock == 0)) ? hi : norm);
            if (cfg.ShowCombo) Outline(combo, (draggingBlock == 4 || (overCombo && draggingBlock == 0)) ? hi : norm);

            if (draggingBlock == 0 && down && !wasMouseDown)
            {
                if (overSlots) { draggingBlock = 1; Grab(slots, mx, my); }
                else if (overRes) { draggingBlock = 2; Grab(res, mx, my); }
                else if (overCast) { draggingBlock = 3; Grab(cast, mx, my); }
                else if (overCombo) { draggingBlock = 4; Grab(combo, mx, my); }
            }

            if (draggingBlock != 0)
            {
                if (down)
                {
                    float fx = Math.Clamp((mx + dragOffsetX) / screenW, 0f, 1f);
                    float fy = Math.Clamp((my + dragOffsetY) / screenH, 0f, 1f);
                    switch (draggingBlock)
                    {
                        case 1: cfg.SlotAnchorX = fx; cfg.SlotAnchorY = fy; break;
                        case 2: cfg.ResAnchorX = fx; cfg.ResAnchorY = fy; break;
                        case 3: cfg.CastAnchorX = fx; cfg.CastAnchorY = fy; break;
                        case 4: cfg.ComboAnchorX = fx; cfg.ComboAnchorY = fy; break;
                    }
                }
                else { draggingBlock = 0; layout.Save(); }
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

        /// <summary>The key currently bound to this slot's cast hotkey (reflects rebinds), or the slot number.</summary>
        private string HotkeyText(int index)
        {
            if (index < 0 || index >= SpellHotbar.HotkeyCodes.Length) return "";
            var hk = capi.Input?.GetHotKeyByCode(SpellHotbar.HotkeyCodes[index]);
            string? key = hk?.CurrentMapping?.PrimaryAsString();
            return string.IsNullOrEmpty(key) ? (index + 1).ToString() : key!;
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
