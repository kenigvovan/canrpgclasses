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
    /// Ortho-stage combat overlay: damage numbers, health and cast bars, target marker, shield/combat readouts.
    /// Projection uses camera state cached per frame in <see cref="OnRenderFrame"/>.
    /// </summary>
    public class CombatOverlayRenderer : IRenderer
    {
        private const float HpRadius = 30f;        // blocks: how far to scan for damaged entities
        private const float HpBarWidth = 44f;
        private const float HpBarHeight = 5f;
        private const double DmgLifetimeMs = 1200; // how long a damage number stays
        private const float DmgRisePerSec = 38f;   // screen pixels a number floats up per second

        // Counterstrike/thorns indicator: icon over the head plus sparkles.
        private const string RiposteEffect = "thorns";
        private const float StatusIconSize = 20f;
        private const double ParticleIntervalMs = 180;

        // Target marker around the Aim/smart-cast pick.
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
        private readonly TargetFrame targetFrame;
        private readonly CairoFont smallFont = CairoFont.WhiteSmallText();
        private readonly CairoFont bigFont = CairoFont.WhiteSmallText().WithFontSize(30);

        private double lastParticleMs;
        private static SimpleParticleProperties? riposteParticles;

        // Nearby living agents, refreshed at ~12Hz and shared by health bars and the target picker.
        private const double NearbyScanIntervalMs = 80;
        private readonly List<Entity> nearby = new();
        private double lastNearbyScanMs;

        // Per-frame projection state shared by TryProject/PickAimedEntity.
        private double eyeX, eyeY, eyeZ;
        private double viewX, viewY, viewZ;
        private readonly double[] pvMat = Mat4d.Create();

        public double RenderOrder => 0.9;
        public int RenderRange => 0;

        public CombatOverlayRenderer(ICoreClientAPI capi, Func<HudLayout?> layout)
        {
            this.capi = capi;
            draw = new Hud2D(capi);
            targetFrame = new TargetFrame(capi, draw, layout);
            capi.Event.RegisterRenderer(this, EnumRenderStage.Ortho, "canrpgclasses-combatoverlay");
        }

        /// <summary>Queue a floating combat number at a world position. <paramref name="heal"/> = green "+N".</summary>
        public void AddDamageNumber(Vec3d pos, float amount, bool heal = false)
        {
            double now = capi.InWorldEllapsedMilliseconds;

            // Spread near-simultaneous numbers at the same spot (e.g. swing + empowered bonus hit).
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
            var aimed = PickAimedEntity(TargetMarkerRange);
            DrawHealthBars(player);
            DrawTargetMarker(player, aimed);
            targetFrame.Draw(player, aimed, deltaTime);
            DrawCastBars(player);
            DrawDamageNumbers();
            DrawSelfShield(player);
            DrawInCombatIndicator(player);
        }

        /// <summary>Corner brackets around the entity an Aim spell would pick (mirrors server LookedAtAgent).</summary>
        private void DrawTargetMarker(EntityPlayer player, Entity? target)
        {
            if (target == null) return;

            var box = target.SelectionBox;
            double px = target.Pos.X, py = target.Pos.Y, pz = target.Pos.Z;
            double hx = box.XSize / 2.0, hz = box.ZSize / 2.0;

            // Screen bounds of the 8 box corners; bail if any is behind the camera.
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

            draw.Line(minX, minY, minX + len, minY, 2f, col);
            draw.Line(minX, minY, minX, minY + len, 2f, col);
            draw.Line(maxX, minY, maxX - len, minY, 2f, col);
            draw.Line(maxX, minY, maxX, minY + len, 2f, col);
            draw.Line(minX, maxY, minX + len, maxY, 2f, col);
            draw.Line(minX, maxY, minX, maxY - len, 2f, col);
            draw.Line(maxX, maxY, maxX - len, maxY, 2f, col);
            draw.Line(maxX, maxY, maxX, maxY - len, 2f, col);
        }

        /// <summary>Client copy of SpellExecutor.LookedAtAgent, in scalar math to avoid per-entity allocations.</summary>
        private Entity? PickAimedEntity(float range)
        {
            Entity? best = null;
            double bestAlong = double.MaxValue;
            // The nearby cache radius covers this range.
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

        /// <summary>Refreshes <see cref="nearby"/>; may be stale by one interval, so readers re-check Alive.
        /// Invisible entities are excluded here.</summary>
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

        /// <summary>Checks the synced flag first: HasEffect only sees the local player's own effects.</summary>
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

                // Shown regardless of health.
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

            // The scan excludes the local player.
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

        /// <summary>Local absorb shield readout: remaining amount and seconds left.</summary>
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

        /// <summary>"In combat" readout above the shield readout.</summary>
        private void DrawInCombatIndicator(EntityPlayer player)
        {
            if (!Core.HarmonyPatches.StunPatches.IsInCombat(player)) return;

            string txt = Lang.Get("canrpgclasses:hud-in-combat");
            float iconSize = draw.TextSize(txt, smallFont).Y * 1.2f;
            DrawBadge("swords-power", txt,
                new Vec4f(0.95f, 0.4f, 0.35f, 1f), new Vec4f(0.95f, 0.4f, 0.35f, 1f),
                capi.Render.FrameHeight * 0.755f - iconSize - 6f);
        }

        /// <summary>Icon + text, centred horizontally at the given row.</summary>
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

        /// <summary>Cast bars over other casters. Entries expire by duration; interrupts arrive as cancel packets.</summary>
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

        private void EmitShieldParticles(Entity e)
        {
            var box = e.SelectionBox;
            Vec3d center = e.Pos.XYZ.Add(0, box.Y2 * 0.5, 0);
            double rxz = Math.Max(box.XSize, box.ZSize) * 0.5 + 0.35;
            double ry = box.Y2 * 0.5 + 0.25;
            var rnd = capi.World.Rand;
            for (int i = 0; i < 10; i++)
            {
                double u = rnd.NextDouble() * 2.0 - 1.0;          // cos(theta), uniform on sphere
                double phi = rnd.NextDouble() * Math.PI * 2.0;
                double s = Math.Sqrt(Math.Max(0.0, 1.0 - u * u));
                var at = new Vec3d(center.X + s * Math.Cos(phi) * rxz,
                                   center.Y + u * ry,
                                   center.Z + s * Math.Sin(phi) * rxz);
                var p = new SimpleParticleProperties(
                    1, 1, ColorUtil.ToRgba(160, 255, 235, 130), // a,r,g,b
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
                ColorUtil.ToRgba(220, 240, 200, 80), // a,r,g,b
                new Vec3d(), new Vec3d(),
                new Vec3f(-0.2f, 0.1f, -0.2f), new Vec3f(0.2f, 0.45f, 0.2f),
                0.7f, 0f, 0.2f, 0.5f, EnumParticleModel.Quad);

            var box = e.SelectionBox;
            Vec3d pos = e.Pos.XYZ;
            p.MinPos.Set(pos.X - box.XSize / 2.0, pos.Y + 0.2, pos.Z - box.ZSize / 2.0);
            p.AddPos.Set(box.XSize, box.Y2, box.ZSize);
            capi.World.SpawnParticles(p);
        }

        /// <summary>Allocation-free inline of MatrixToolsd.Project; false if behind the camera. Y is flipped
        /// because the Ortho screen is top-down.</summary>
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

        // red (low) - yellow - green (high)
        internal static Vec4f HealthColor(float frac)
            => new(Math.Min(1f, 2f * (1f - frac)), Math.Min(1f, 2f * frac), 0.15f, 0.9f);

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Ortho);
            draw.Dispose();
        }
    }
}
