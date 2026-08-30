using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

using EBEffects = effectshud.src.EBEffectsAffected;
using InvisEffect = effectshud.src.DefaultEffects.InvisibilityEffect;

namespace canrpgclasses.Client.Render
{
    /// <summary>
    /// World-space combat overlay drawn on the Ortho stage: floating damage numbers, health and cast bars, the
    /// target marker and the local shield/combat readouts. World positions are projected with the camera state
    /// cached per frame in <see cref="OnRenderFrame"/>.
    /// </summary>
    public class CombatOverlayRenderer : IRenderer
    {
        private const float HpRadius = 30f;        // blocks: how far to scan for damaged entities
        private const float HpBarWidth = 44f;
        private const float HpBarHeight = 5f;
        private const double DmgLifetimeMs = 1200; // how long a damage number stays
        private const float DmgRisePerSec = 38f;   // screen pixels a number floats up per second

        // Counterstrike/thorns indicator: an icon over the head + sparkles, so attackers see "this one reflects".
        private const string RiposteEffect = "thorns";
        private const float StatusIconSize = 20f;
        private const double ParticleIntervalMs = 180;

        // Target marker: corner brackets around the entity under the crosshair (the Aim/smart-cast pick).
        private const float TargetMarkerRange = 16f; // matches the default Aim spell range
        private static readonly Vec4f TargetMarkerColor = new(1f, 0.9f, 0.35f, 0.95f);    // gold (enemy/neutral)
        private static readonly Vec4f TargetMarkerAllyColor = new(0.35f, 1f, 0.45f, 0.95f); // green (ally)

        private const float CastBarWidth = 60f;
        private const float CastBarHeight = 7f;

        private struct DamageNumber { public Vec3d Pos; public float Amount; public double SpawnMs; public bool Heal; }
        private readonly List<DamageNumber> numbers = new();

        private struct WorldCast { public string Name; public double StartMs; public double DurationMs; }
        // Keyed by caster entity id. The local player is never added here (they have the hotbar cast bar).
        private readonly Dictionary<long, WorldCast> casts = new();

        private readonly ICoreClientAPI capi;
        private readonly Hud2D draw;
        private readonly CairoFont smallFont = CairoFont.WhiteSmallText();
        private readonly CairoFont bigFont = CairoFont.WhiteSmallText().WithFontSize(30);

        private double lastParticleMs;
        private static SimpleParticleProperties? riposteParticles;

        // Nearby living agents, refreshed at ~12Hz and shared by the health bars and the target picker - one
        // spatial query a few times a second instead of GetEntitiesAround twice every frame.
        private const double NearbyScanIntervalMs = 80;
        private readonly List<Entity> nearby = new();
        private double lastNearbyScanMs;

        // Per-frame projection state, computed ONCE and shared by every TryProject/PickAimedEntity call (dozens
        // per frame: 8 target-marker corners plus one per health bar / damage number / cast bar).
        private double eyeX, eyeY, eyeZ;
        private double viewX, viewY, viewZ;
        private readonly double[] pvMat = Mat4d.Create();

        public double RenderOrder => 0.9;
        public int RenderRange => 0;

        public CombatOverlayRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;
            draw = new Hud2D(capi);
            capi.Event.RegisterRenderer(this, EnumRenderStage.Ortho, "canrpgclasses-combatoverlay");
        }

        /// <summary>Queue a floating combat number at a world position. <paramref name="heal"/> = green "+N".</summary>
        public void AddDamageNumber(Vec3d pos, float amount, bool heal = false)
        {
            double now = capi.InWorldEllapsedMilliseconds;

            // Nudge sideways per already-fresh number at ~the same spot, so near-simultaneous hits on the same
            // target (a melee swing landing together with its separate empowered-bonus hit) don't render exactly
            // on top of each other and go unreadable.
            int collisions = 0;
            foreach (var n in numbers)
            {
                if (now - n.SpawnMs > 250) continue;
                double dx = n.Pos.X - pos.X, dz = n.Pos.Z - pos.Z;
                if (dx * dx + dz * dz < 0.09) collisions++; // within ~0.3 blocks
            }
            if (collisions > 0)
            {
                double angle = collisions * 2.4; // spread successive collisions around a small circle
                pos = pos.AddCopy(new Vec3d(Math.Cos(angle) * 0.35, 0, Math.Sin(angle) * 0.35));
            }

            numbers.Add(new DamageNumber { Pos = pos, Amount = amount, SpawnMs = now, Heal = heal });
        }

        public void AddWorldCast(long entityId, string name, float durationSeconds)
            => casts[entityId] = new WorldCast
            {
                Name = name,
                StartMs = capi.InWorldEllapsedMilliseconds,
                DurationMs = durationSeconds * 1000.0
            };

        public void RemoveWorldCast(long entityId) => casts.Remove(entityId);

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (stage != EnumRenderStage.Ortho) return;

            var player = capi.World?.Player?.Entity;
            if (player == null) return;

            var r = capi.Render;
            eyeX = player.Pos.X + player.LocalEyePos.X;
            eyeY = player.Pos.Y + player.LocalEyePos.Y;
            eyeZ = player.Pos.Z + player.LocalEyePos.Z;
            var vf = player.Pos.GetViewVector(); // unit vector by construction (sin/cos of pitch/yaw)
            viewX = vf.X; viewY = vf.Y; viewZ = vf.Z;
            Mat4d.Mul(pvMat, r.PerspectiveProjectionMat, r.PerspectiveViewMat);

            RefreshNearby(player);
            DrawHealthBars(player);
            DrawTargetMarker(player);
            DrawCastBars(player);
            DrawDamageNumbers();
            DrawSelfShield(player);
            DrawInCombatIndicator(player);
        }

        /// <summary>Corner brackets around the entity currently under the crosshair - the one an Aim spell (or a
        /// smart-cast heal) would pick. Mirrors the server's LookedAtAgent so the player sees the same pick before
        /// casting.</summary>
        private void DrawTargetMarker(EntityPlayer player)
        {
            var target = PickAimedEntity(TargetMarkerRange);
            if (target == null) return;

            var box = target.SelectionBox;
            double px = target.Pos.X, py = target.Pos.Y, pz = target.Pos.Z;
            double hx = box.XSize / 2.0, hz = box.ZSize / 2.0;

            // Project the 8 box corners and take the screen-space bounds. Bail if any corner is behind the camera.
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                double cx = px + ((i & 1) == 0 ? -hx : hx);
                double cy = py + ((i & 2) == 0 ? 0.0 : box.Y2);
                double cz = pz + ((i & 4) == 0 ? -hz : hz);
                if (!TryProject(cx, cy, cz, out float sx, out float sy)) return;
                if (sx < minX) minX = sx; if (sy < minY) minY = sy;
                if (sx > maxX) maxX = sx; if (sy > maxY) maxY = sy;
            }

            float len = Math.Min(14f, Math.Min(maxX - minX, maxY - minY) * 0.4f);
            if (len < 2f) return;

            bool ally = Core.Execution.SpellExecutor.IsAlly(player, target);
            var col = ally ? TargetMarkerAllyColor : TargetMarkerColor;

            // Four L-shaped corner brackets.
            draw.Line(minX, minY, minX + len, minY, 2f, col);
            draw.Line(minX, minY, minX, minY + len, 2f, col);
            draw.Line(maxX, minY, maxX - len, minY, 2f, col);
            draw.Line(maxX, minY, maxX, minY + len, 2f, col);
            draw.Line(minX, maxY, minX + len, maxY, 2f, col);
            draw.Line(minX, maxY, minX, maxY - len, 2f, col);
            draw.Line(maxX, maxY, maxX - len, maxY, 2f, col);
            draw.Line(maxX, maxY, maxX, maxY - len, 2f, col);
        }

        /// <summary>Client-side copy of SpellExecutor.LookedAtAgent: the living agent nearest along the look ray.
        /// Uses the frame-cached eye/view and plain scalar math - a per-entity Vec3d chain would allocate two or
        /// three vectors per nearby entity every frame.</summary>
        private Entity? PickAimedEntity(float range)
        {
            Entity? best = null;
            double bestAlong = double.MaxValue;
            // Reuse the shared nearby cache (radius ⊇ this pick range); the along/perp test applies the range.
            foreach (var e in nearby)
            {
                if (e == null || !e.Alive) continue;
                double tx = e.Pos.X - eyeX;
                double ty = e.Pos.Y + e.SelectionBox.Y2 * 0.5 - eyeY;
                double tz = e.Pos.Z - eyeZ;
                double along = tx * viewX + ty * viewY + tz * viewZ;
                if (along <= 0 || along > range) continue;
                double lenSq = tx * tx + ty * ty + tz * tz;
                double perpSq = lenSq - along * along;
                double radius = Math.Max(e.SelectionBox.XSize, e.SelectionBox.ZSize) * 0.5 + 0.4;
                if (perpSq > radius * radius) continue;
                if (along < bestAlong) { bestAlong = along; best = e; }
            }
            return best;
        }

        /// <summary>Refreshes <see cref="nearby"/> (living agents within HpRadius) at ~12Hz instead of every frame.
        /// The cached entities may go stale for up to one interval, so users re-check Alive. Invisible entities are
        /// excluded outright - health bars and the target marker both read this cache, so hiding them here hides
        /// them from both at once.</summary>
        private void RefreshNearby(EntityPlayer player)
        {
            double now = capi.InWorldEllapsedMilliseconds;
            if (lastNearbyScanMs != 0 && now - lastNearbyScanMs < NearbyScanIntervalMs) return;
            lastNearbyScanMs = now;
            nearby.Clear();
            Vec3d eye = player.Pos.XYZ.Add(player.LocalEyePos);
            foreach (var e in player.World.GetEntitiesAround(eye, HpRadius, HpRadius,
                         e => e != player && e.Alive && e is EntityAgent && !IsInvisibleToObserver(e)))
                nearby.Add(e);
        }

        /// <summary>True if the entity should be hidden from this client's overlay because it's invisible. The
        /// synced flag comes first: HasEffect only sees the local player's own effects, not another entity's.</summary>
        private static bool IsInvisibleToObserver(Entity e)
        {
            if (e.WatchedAttributes.GetBool(InvisEffect.InvisibleAttr)) return true;
            return e.GetBehavior<EBEffects>()?.HasEffect("invisibility") == true;
        }

        private void DrawHealthBars(EntityPlayer player)
        {
            bool emit = capi.InWorldEllapsedMilliseconds - lastParticleMs > ParticleIntervalMs;
            var icons = canrpgclassesModSystem.ClientInstance?.Icons;

            foreach (var e in nearby)
            {
                if (e == null || !e.Alive) continue;

                // Counterstrike/thorns indicator - shown regardless of health so attackers see it before striking.
                if (e.GetBehavior<EBEffects>()?.HasEffect(RiposteEffect) == true)
                {
                    var tex = icons?.GetTex(IconLoader.PathFor("body-balance"));
                    if (tex != null && TryProject(e.Pos.X, e.Pos.Y + e.SelectionBox.Y2 + 0.8, e.Pos.Z, out float ix, out float iy))
                        draw.Icon(tex, ix - StatusIconSize / 2f, iy - StatusIconSize, StatusIconSize, StatusIconSize);
                    if (emit) EmitRiposteParticles(e);
                }

                if (emit && HasActiveShield(e)) EmitShieldParticles(e);

                var tree = e.WatchedAttributes.GetTreeAttribute("health");
                if (tree == null) continue;
                float cur = tree.GetFloat("currenthealth");
                float max = tree.GetFloat("maxhealth");
                if (max <= 0f || cur >= max) continue; // only show damaged entities

                if (!TryProject(e.Pos.X, e.Pos.Y + e.SelectionBox.Y2 + 0.35, e.Pos.Z, out float sx, out float sy)) continue;

                float frac = Math.Clamp(cur / max, 0f, 1f);
                float bx = sx - HpBarWidth / 2f, by = sy - HpBarHeight;
                draw.Rect(bx, by, HpBarWidth, HpBarHeight, new Vec4f(0f, 0f, 0f, 0.6f));
                draw.Rect(bx, by, HpBarWidth * frac, HpBarHeight, HealthColor(frac));
                draw.Frame(bx, by, HpBarWidth, HpBarHeight, 1f, new Vec4f(0.85f, 0.85f, 0.9f, 0.6f));
            }

            // Your own Counterstrike: the scan above excludes yourself, so emit on yourself too (visual
            // confirmation; other players still see it through their own overlay).
            if (emit && player.GetBehavior<EBEffects>()?.HasEffect(RiposteEffect) == true) EmitRiposteParticles(player);
            if (emit && HasActiveShield(player)) EmitShieldParticles(player);

            if (emit) lastParticleMs = capi.InWorldEllapsedMilliseconds;
        }

        private static bool HasActiveShield(Entity e)
        {
            var wa = e.WatchedAttributes;
            if (wa == null || wa.GetFloat(Core.CombatFlags.Absorb, 0f) <= 0f) return false;
            return e.World.ElapsedMilliseconds <= wa.GetLong(Core.CombatFlags.AbsorbUntil, 0);
        }

        /// <summary>Screen readout of the local player's own absorb shield: a holy-ring icon with the remaining
        /// amount and seconds left, centred just above the resource bar, while a shield is up.</summary>
        private void DrawSelfShield(EntityPlayer player)
        {
            var wa = player.WatchedAttributes;
            if (wa == null) return;
            float amount = wa.GetFloat(Core.CombatFlags.Absorb, 0f);
            if (amount <= 0f) return;
            long until = wa.GetLong(Core.CombatFlags.AbsorbUntil, 0);
            long now = player.World.ElapsedMilliseconds;
            if (now > until) return;

            string txt = $"{Math.Round(amount)}  {(until - now) / 1000f:0.0}s";
            DrawBadge("power-ring", txt, new Vec4f(0.72f, 0.9f, 1f, 1f), new Vec4f(0.6f, 0.85f, 1f, 1f),
                capi.Render.FrameHeight * 0.755f);
        }

        /// <summary>Small "in combat" readout stacked directly above the shield readout while the general combat
        /// tag (which also gates Stealth casting) is active.</summary>
        private void DrawInCombatIndicator(EntityPlayer player)
        {
            if (!Core.HarmonyPatches.StunPatches.IsInCombat(player)) return;

            string txt = Lang.Get("canrpgclasses:hud-in-combat");
            float iconSize = draw.TextSize(txt, smallFont).Y * 1.2f;
            DrawBadge("swords-power", txt,
                new Vec4f(0.95f, 0.4f, 0.35f, 1f), new Vec4f(0.95f, 0.4f, 0.35f, 1f),
                capi.Render.FrameHeight * 0.755f - iconSize - 6f);
        }

        /// <summary>Icon + text, centred horizontally at the given screen row - the shared shape of the shield and
        /// in-combat readouts.</summary>
        private void DrawBadge(string iconName, string text, Vec4f textColor, Vec4f iconTint, float y)
        {
            var icons = canrpgclassesModSystem.ClientInstance?.Icons;
            var tex = icons?.GetTex(IconLoader.PathFor(iconName));

            var ts = draw.TextSize(text, smallFont);
            float iconSize = ts.Y * 1.2f;
            const float gap = 5f;
            float x0 = capi.Render.FrameWidth * 0.5f - (iconSize + gap + ts.X) / 2f;

            if (tex != null) draw.Icon(tex, x0, y, iconSize, iconSize, iconTint);
            draw.Text(text, smallFont, x0 + iconSize + gap, y + (iconSize - ts.Y) / 2f, textColor);
        }

        /// <summary>Cast bars over other casters' heads, so the player can see a channel and try to interrupt it.
        /// Entries come from the network handlers and expire on their own when the duration elapses (the server's
        /// cancel packet hides interrupted ones).</summary>
        private void DrawCastBars(EntityPlayer player)
        {
            if (casts.Count == 0) return;
            double now = capi.InWorldEllapsedMilliseconds;
            List<long>? expired = null;

            foreach (var kv in casts)
            {
                var c = kv.Value;
                float frac = c.DurationMs > 0 ? (float)((now - c.StartMs) / c.DurationMs) : 1f;
                if (frac >= 1f) { (expired ??= new List<long>()).Add(kv.Key); continue; }

                var e = player.World.GetEntityById(kv.Key);
                if (e == null || !e.Alive) { (expired ??= new List<long>()).Add(kv.Key); continue; }

                if (!TryProject(e.Pos.X, e.Pos.Y + e.SelectionBox.Y2 + 0.55, e.Pos.Z, out float sx, out float sy)) continue;

                float bx = sx - CastBarWidth / 2f, by = sy - CastBarHeight;
                draw.Rect(bx, by, CastBarWidth, CastBarHeight, new Vec4f(0f, 0f, 0f, 0.6f));
                draw.Rect(bx, by, CastBarWidth * Math.Clamp(frac, 0f, 1f), CastBarHeight, new Vec4f(0.55f, 0.75f, 1f, 0.95f));
                draw.Frame(bx, by, CastBarWidth, CastBarHeight, 1f, new Vec4f(0.85f, 0.85f, 0.9f, 0.6f));

                var ts = draw.TextSize(c.Name, smallFont);
                draw.Text(c.Name, smallFont, sx - ts.X / 2f, by - ts.Y - 1f, new Vec4f(0.85f, 0.9f, 1f, 1f));
            }

            if (expired != null) foreach (var id in expired) casts.Remove(id);
        }

        private void DrawDamageNumbers()
        {
            double now = capi.InWorldEllapsedMilliseconds;

            for (int i = numbers.Count - 1; i >= 0; i--)
            {
                var n = numbers[i];
                double age = now - n.SpawnMs;
                if (age >= DmgLifetimeMs) { numbers.RemoveAt(i); continue; }

                if (!TryProject(n.Pos.X, n.Pos.Y, n.Pos.Z, out float sx, out float sy)) continue;

                float t = (float)(age / DmgLifetimeMs);
                float rise = (float)(age / 1000.0) * DmgRisePerSec;
                string amount = n.Amount.ToString("0.0");
                string txt = n.Heal ? "+" + amount : amount;

                float alpha = 1f - t;
                var color = n.Heal
                    ? new Vec4f(0.4f, 1f, 0.45f, alpha)   // green heal
                    : new Vec4f(1f, 0.85f, 0.3f, alpha);  // gold damage
                draw.TextCentered(txt, bigFont, sx, sy - rise, color);
            }
        }

        // A glowing golden shell of particles around the entity - the "shield bubble" while absorb is active.
        private void EmitShieldParticles(Entity e)
        {
            var box = e.SelectionBox;
            Vec3d center = e.Pos.XYZ.Add(0, box.Y2 * 0.5, 0);
            double rxz = Math.Max(box.XSize, box.ZSize) * 0.5 + 0.35;
            double ry = box.Y2 * 0.5 + 0.25;
            var rnd = capi.World.Rand;
            for (int i = 0; i < 10; i++)
            {
                double u = rnd.NextDouble() * 2.0 - 1.0;          // cos(theta) → uniform on sphere
                double phi = rnd.NextDouble() * Math.PI * 2.0;
                double s = Math.Sqrt(Math.Max(0.0, 1.0 - u * u));
                var at = new Vec3d(center.X + s * Math.Cos(phi) * rxz,
                                   center.Y + u * ry,
                                   center.Z + s * Math.Sin(phi) * rxz);
                var p = new SimpleParticleProperties(
                    1, 1, ColorUtil.ToRgba(160, 255, 235, 130), // a,r,g,b → holy gold, semi-transparent
                    at, at,
                    new Vec3f(-0.05f, 0f, -0.05f), new Vec3f(0.05f, 0.1f, 0.05f),
                    0.6f, 0f, 0.25f, 0.5f, EnumParticleModel.Quad);
                p.MinPos.Set(at);
                p.AddPos.Set(0, 0, 0);
                p.VertexFlags = 255; // self-lit (glow)
                capi.World.SpawnParticles(p);
            }
        }

        private void EmitRiposteParticles(Entity e)
        {
            var p = riposteParticles ??= new SimpleParticleProperties(
                1, 2,
                ColorUtil.ToRgba(220, 240, 200, 80), // a,r,g,b → golden, semi-transparent
                new Vec3d(), new Vec3d(),
                new Vec3f(-0.2f, 0.1f, -0.2f), new Vec3f(0.2f, 0.45f, 0.2f),
                0.7f, 0f, 0.2f, 0.5f, EnumParticleModel.Quad);

            var box = e.SelectionBox;
            Vec3d pos = e.Pos.XYZ;
            p.MinPos.Set(pos.X - box.XSize / 2.0, pos.Y + 0.2, pos.Z - box.ZSize / 2.0);
            p.AddPos.Set(box.XSize, box.Y2, box.ZSize);
            capi.World.SpawnParticles(p);
        }

        /// <summary>Projects a world position to screen pixels using the per-frame camera state. Returns false if
        /// the point is behind the camera. Inlines MatrixToolsd.Project's math (column-major mat*vec on the
        /// premultiplied projection*view): the API method allocates two arrays and a Vec3d per call, and this runs
        /// dozens of times per frame. GL's y is bottom-up, the Ortho screen is top-down, hence the flip.</summary>
        private bool TryProject(double wx, double wy, double wz, out float screenX, out float screenY)
        {
            screenX = 0; screenY = 0;
            double dot = (wx - eyeX) * viewX + (wy - eyeY) * viewY + (wz - eyeZ) * viewZ;
            if (dot <= 0) return false;

            double sx = pvMat[0] * wx + pvMat[4] * wy + pvMat[8] * wz + pvMat[12];
            double sy = pvMat[1] * wx + pvMat[5] * wy + pvMat[9] * wz + pvMat[13];
            double sw = pvMat[3] * wx + pvMat[7] * wy + pvMat[11] * wz + pvMat[15];

            var r = capi.Render;
            screenX = (float)((sx / sw + 1.0) * (r.FrameWidth / 2));
            screenY = r.FrameHeight - (float)((sy / sw + 1.0) * (r.FrameHeight / 2));
            return true;
        }

        // red (low) → yellow (mid) → green (high)
        private static Vec4f HealthColor(float frac)
            => new(Math.Min(1f, 2f * (1f - frac)), Math.Min(1f, 2f * frac), 0.15f, 0.9f);

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Ortho);
            draw.Dispose();
        }
    }
}
