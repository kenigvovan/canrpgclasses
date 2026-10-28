using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

using EffectsApi = effectshud.src.effectshud;
using EffectClientData = effectshud.src.EffectClientData;
using EffectDisplayUtil = effectshud.src.EffectDisplayUtil;
using InvisEffect = effectshud.src.DefaultEffects.InvisibilityEffect;

namespace canrpgclasses.Client.Render
{
    /// <summary>Top-centre frame for the aimed entity: name, health and its visible effects with timers.</summary>
    public class TargetFrame
    {
        private const double HoldMs = 4000;          // keep the last target this long after the crosshair leaves it
        private const double MaxHoldDistance = 40;
        private const double EffectsPollMs = 250;

        /// <summary>Panel height the layout editor drags by; the drawn panel is centred on the same anchor.</summary>
        public const float NominalHeight = 48f;

        private const float BaseWidth = 280f;
        private const float Pad = 6f;
        private const float BarHeight = 16f;
        private const float BaseIconSize = 30f;
        private const float IconGap = 5f;
        private const float BlinkUnder = 3f;         // seconds left when an icon starts pulsing

        private static readonly Vec4f PanelBg = new(0.05f, 0.04f, 0.03f, 0.55f);
        private static readonly Vec4f EnemyAccent = new(1f, 0.82f, 0.35f, 0.9f);   // matches the gold target marker
        private static readonly Vec4f AllyAccent = new(0.35f, 1f, 0.45f, 0.9f);
        private static readonly Vec4f EnemyBar = new(0.78f, 0.18f, 0.14f, 0.95f);
        private static readonly Vec4f AllyBar = new(0.2f, 0.72f, 0.25f, 0.95f);
        private static readonly Vec4f DamageTrail = new(1f, 0.85f, 0.45f, 0.8f);
        private static readonly Vec4f DebuffBorder = new(0.95f, 0.28f, 0.22f, 1f);
        private static readonly Vec4f BuffBorder = new(0.35f, 0.85f, 0.4f, 1f);
        private static readonly Vec4f White = new(1f, 1f, 1f, 1f);

        private readonly ICoreClientAPI capi;
        private readonly Hud2D draw;
        private readonly Func<HudLayout?> layout;
        private float iconSize = BaseIconSize;
        private readonly CairoFont nameFont = CairoFont.WhiteSmallText();
        private readonly CairoFont hpFont = CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold);
        private readonly CairoFont timerFont = CairoFont.WhiteSmallText().WithFontSize(12);

        private Entity? target;
        private double lastSeenMs;
        private double lastPollMs;
        private float trailFrac;
        private readonly List<EffectClientData> debuffs = new();
        private readonly List<EffectClientData> buffs = new();
        // Per typeId: expected expiry (client ms) and full duration, for the drain sweep; reset on reapply.
        private readonly Dictionary<string, (double ExpireMs, double TotalSec)> timing = new();
        private readonly Dictionary<string, string> iconPaths = new();

        public TargetFrame(ICoreClientAPI capi, Hud2D draw, Func<HudLayout?> layout)
        {
            this.capi = capi;
            this.draw = draw;
            this.layout = layout;
        }

        public void Draw(EntityPlayer player, Entity? aimed, float dt)
        {
            var hud = layout();
            var cfg = hud?.For(Core.Talents.TalentState.CurrentClass(player)) ?? new HudLayoutConfig();
            if (!cfg.ShowTarget) { target = null; return; }

            double now = capi.InWorldEllapsedMilliseconds;
            if (aimed != null)
            {
                if (aimed != target) { lastPollMs = 0; timing.Clear(); trailFrac = -1f; }
                target = aimed;
                lastSeenMs = now;
            }
            else if (target != null && (!target.Alive || now - lastSeenMs > HoldMs
                     || target.WatchedAttributes.GetBool(InvisEffect.InvisibleAttr) // don't track through stealth
                     || target.Pos.SquareDistanceTo(player.Pos) > MaxHoldDistance * MaxHoldDistance))
            {
                target = null;
            }
            // Layout editor open with nothing aimed: preview on the player so the frame can be placed.
            bool preview = target == null && hud?.EditMode == true;
            if (target == null && !preview) return;
            var shown = target ?? player;

            if (!preview && now - lastPollMs >= EffectsPollMs) Poll(now);

            bool ally = Core.Execution.SpellExecutor.IsAlly(player, shown);
            var accent = ally ? AllyAccent : EnemyAccent;

            float width = cfg.TargetWidth;
            iconSize = Math.Clamp(BaseIconSize * width / BaseWidth, 20f, 44f);

            string name = shown.GetName() ?? "?";
            float nameH = draw.TextSize(name, nameFont).Y;
            float rowH = iconSize + draw.TextSize("0", timerFont).Y + 2f;
            float height = Pad + nameH + 3f + BarHeight + Pad;

            float x = capi.Render.FrameWidth * cfg.TargetAnchorX - width / 2f;
            float y = capi.Render.FrameHeight * cfg.TargetAnchorY - NominalHeight / 2f;

            DrawPanel(x, y, width, height, accent);

            float ix = x + Pad, iw = width - 2 * Pad;
            float nameX = ix;
            int? level = LevelOf(shown);
            if (level.HasValue)
            {
                string lv = level.Value.ToString();
                var ls = draw.TextSize(lv, hpFont);
                float bw = Math.Max(nameH, ls.X + 6f);
                var lc = LevelColor(level.Value, LevelOf(player) ?? 1);
                draw.Rect(ix, y + Pad, bw, nameH, new Vec4f(0f, 0f, 0f, 0.6f));
                draw.Frame(ix, y + Pad, bw, nameH, 1f, new Vec4f(lc.X, lc.Y, lc.Z, 0.7f));
                draw.TextCentered(lv, hpFont, ix + bw / 2f, y + Pad + nameH / 2f, lc);
                nameX += bw + 5f;
            }
            draw.Text(name, nameFont, nameX, y + Pad, accent);
            // Thin divider fading out from the centre under the name.
            for (int i = 0; i < 4; i++)
            {
                float w = iw * (0.7f - i * 0.15f);
                draw.Rect(x + (width - w) / 2f, y + Pad + nameH + 1f, w, 1f, new Vec4f(accent.X, accent.Y, accent.Z, 0.12f));
            }

            var health = shown.WatchedAttributes.GetTreeAttribute("health");
            float cur = health?.GetFloat("currenthealth") ?? 0f;
            float max = health?.GetFloat("maxhealth") ?? 0f;
            float frac = max > 0f ? Math.Clamp(cur / max, 0f, 1f) : 0f;

            // Trail drains toward the real value so a hit shows as a fading chunk.
            if (trailFrac < frac) trailFrac = frac;
            else trailFrac = Math.Max(frac, trailFrac - dt * 0.5f);

            float by = y + Pad + nameH + 3f;
            var bar = ally ? AllyBar : EnemyBar;
            if (frac < 0.2f) // low health pulse
            {
                float k = 0.75f + 0.25f * (float)Math.Abs(Math.Cos(now / 220.0));
                bar = new Vec4f(bar.X * k, bar.Y * k, bar.Z * k, bar.W);
            }
            float fw = iw * frac;
            draw.Rect(ix, by, iw, BarHeight, new Vec4f(0.02f, 0.02f, 0.02f, 0.8f));
            draw.Rect(ix, by, iw * trailFrac, BarHeight, DamageTrail);
            // Three bands fake a vertical gradient: light top, base, dark bottom.
            draw.Rect(ix, by, fw, BarHeight, bar);
            draw.Rect(ix, by, fw, BarHeight * 0.35f, new Vec4f(1f, 1f, 1f, 0.16f));
            draw.Rect(ix, by + BarHeight * 0.7f, fw, BarHeight * 0.3f, new Vec4f(0f, 0f, 0f, 0.22f));
            for (int i = 1; i < 10; i++) // 10% ticks
                draw.Rect(ix + iw * i / 10f, by + 1f, 1f, BarHeight - 2f, new Vec4f(0f, 0f, 0f, 0.25f));
            draw.Frame(ix - 1f, by - 1f, iw + 2f, BarHeight + 2f, 1f, new Vec4f(0f, 0f, 0f, 0.95f));
            draw.Frame(ix - 2f, by - 2f, iw + 4f, BarHeight + 4f, 1f, new Vec4f(accent.X, accent.Y, accent.Z, 0.25f));
            if (max > 0f)
            {
                // Above the bar, not on it: white on a red/green fill was hard to read.
                string hp = $"{Math.Ceiling(cur)} / {Math.Ceiling(max)}";
                var hs = draw.TextSize(hp, hpFont);
                draw.Text(hp, hpFont, ix + iw - hs.X, y + Pad + (nameH - hs.Y) / 2f, White);
            }

            // Effect rows hang under the panel, not inside it.
            if (preview || !cfg.ShowTargetEffects) return;
            float ry = y + height + 4f;
            if (debuffs.Count > 0) { DrawRow(debuffs, x, ry, width, now); ry += rowH; }
            if (buffs.Count > 0) DrawRow(buffs, x, ry, width, now);
        }

        /// <summary>Two-tone backplate, double border and accent corner brackets (same motif as the target marker).</summary>
        private void DrawPanel(float x, float y, float w, float h, Vec4f accent)
        {
            draw.Rect(x, y, w, h, PanelBg);
            draw.Rect(x, y + h * 0.5f, w, h * 0.5f, new Vec4f(0f, 0f, 0f, 0.18f));
            draw.Frame(x - 1f, y - 1f, w + 2f, h + 2f, 1f, new Vec4f(0f, 0f, 0f, 0.8f));
            draw.Frame(x, y, w, h, 1f, new Vec4f(accent.X, accent.Y, accent.Z, 0.22f));

            const float len = 10f, t = 2f;
            float r = x + w, b = y + h;
            draw.Line(x, y, x + len, y, t, accent); draw.Line(x, y, x, y + len, t, accent);
            draw.Line(r, y, r - len, y, t, accent); draw.Line(r, y, r, y + len, t, accent);
            draw.Line(x, b, x + len, b, t, accent); draw.Line(x, b, x, b - len, t, accent);
            draw.Line(r, b, r - len, b, t, accent); draw.Line(r, b, r, b - len, t, accent);
        }

        private void Poll(double now)
        {
            lastPollMs = now;
            debuffs.Clear();
            buffs.Clear();
            foreach (var ef in EffectsApi.GetPublicEffects(target!))
            {
                (ef.positive ? buffs : debuffs).Add(ef);
                if (ef.infinite) continue;
                double expire = now + ef.duration * 1000.0;
                // Expiry moved by more than the poll jitter: applied or refreshed, so this is the new full duration.
                if (!timing.TryGetValue(ef.typeId, out var t) || Math.Abs(t.ExpireMs - expire) > 600)
                    timing[ef.typeId] = (expire, Math.Max(ef.duration, 0.1));
                else
                    timing[ef.typeId] = (expire, t.TotalSec);
            }
            debuffs.Sort((a, b) => Remaining(a, now).CompareTo(Remaining(b, now)));
            buffs.Sort((a, b) => Remaining(a, now).CompareTo(Remaining(b, now)));
        }

        private double Remaining(EffectClientData ef, double now)
            => ef.infinite || !timing.TryGetValue(ef.typeId, out var t) ? double.MaxValue : (t.ExpireMs - now) / 1000.0;

        private void DrawRow(List<EffectClientData> list, float x0, float y0, float width, double now)
        {
            var icons = canrpgclassesModSystem.ClientInstance?.Icons;
            float s = iconSize;
            int fit = Math.Max(1, (int)((width + IconGap) / (s + IconGap)));

            for (int i = 0; i < list.Count && i < fit; i++)
            {
                var ef = list[i];
                float x = x0 + i * (s + IconGap);
                float y = y0;
                double left = Remaining(ef, now);
                bool timed = !ef.infinite && left < double.MaxValue;

                // Pulse in the last seconds.
                float alpha = timed && left < BlinkUnder ? 0.55f + 0.45f * (float)Math.Abs(Math.Cos(now / 160.0)) : 1f;
                var border = ef.positive ? BuffBorder : DebuffBorder;

                draw.Rect(x, y, s, s, new Vec4f(0f, 0f, 0f, 0.65f));
                var tex = icons?.GetTex(IconPath(ef.typeId));
                if (tex != null)
                    draw.Icon(tex, x + 2f, y + 2f, s - 4f, s - 4f, new Vec4f(1f, 1f, 1f, alpha));
                else
                {
                    draw.Rect(x + 2f, y + 2f, s - 4f, s - 4f, new Vec4f(border.X, border.Y, border.Z, 0.35f * alpha));
                    string name = EffectDisplayUtil.EffectName(ef.typeId);
                    draw.TextCentered(name.Length > 0 ? name.Substring(0, 1) : "?", nameFont, x + s / 2f, y + s / 2f, White);
                }

                if (timed && timing.TryGetValue(ef.typeId, out var t))
                    draw.CooldownSweep(x + 2f, y + 2f, s - 4f, (float)(left / t.TotalSec), new Vec4f(0f, 0f, 0f, 0.5f)); // shade grows as it runs out

                draw.Frame(x, y, s, s, 1.5f, new Vec4f(border.X, border.Y, border.Z, alpha));

                if (ef.tier > 1)
                {
                    string stacks = ef.tier.ToString();
                    var ts = draw.TextSize(stacks, timerFont);
                    draw.Text(stacks, timerFont, x + s - ts.X - 2f, y + s - ts.Y, White);
                }
                if (timed)
                    draw.TextCentered(FormatShort(left), timerFont, x + s / 2f, y + s + 7f, TimerColor(left));
            }
        }

        /// <summary>IconLoader base path (no extension) for an effect's registered icon.</summary>
        private string IconPath(string typeId)
        {
            if (iconPaths.TryGetValue(typeId, out var path)) return path;
            string loc = EffectDisplayUtil.ResolveIcon(typeId)?.ToString() ?? "";
            int dot = loc.LastIndexOf('.');
            path = dot > loc.LastIndexOf('/') ? loc.Substring(0, dot) : loc;
            iconPaths[typeId] = path;
            return path;
        }

        /// <summary>Player: current class level. Mob: canrpgmobs level, none for vanilla mobs.</summary>
        private static int? LevelOf(Entity e)
            => e is EntityPlayer
                ? Math.Max(1, e.WatchedAttributes.GetInt(Core.AttrKeys.Level(Core.Talents.TalentState.CurrentClass(e)), 1))
                : Core.Progression.XpRules.MobLevel(e);

        // WoW-style difficulty: red +5, orange +3, yellow near, green below, grey once XP drops to half or less.
        private static Vec4f LevelColor(int target, int mine)
        {
            int diff = target - mine;
            if (diff >= 5) return new Vec4f(1f, 0.2f, 0.15f, 1f);
            if (diff >= 3) return new Vec4f(1f, 0.55f, 0.15f, 1f);
            if (diff >= -2) return new Vec4f(1f, 0.9f, 0.2f, 1f);
            if (Core.Progression.XpRules.LevelFactor(mine, target) <= 0.5f) return new Vec4f(0.6f, 0.6f, 0.6f, 1f);
            return new Vec4f(0.35f, 0.9f, 0.35f, 1f);
        }

        private static Vec4f TimerColor(double left)
            => left <= BlinkUnder ? new Vec4f(1f, 0.35f, 0.3f, 1f)
             : left <= 10 ? new Vec4f(1f, 0.85f, 0.35f, 1f)
             : White;

        // Seconds under a minute, else m:ss.
        private static string FormatShort(double seconds)
            => seconds < 60 ? Math.Max(0, Math.Ceiling(seconds)).ToString() : EffectDisplayUtil.FormatTime(seconds);
    }
}
