using System;
using canrpgclasses.Core.Net;
using canrpgclasses.Core.Spells;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Client half of <see cref="canrpgclasses.Core.Visuals.SkillFx"/>: builds one-shot visuals from a
    /// <see cref="SkillFxPacket"/>. Delayed passes capture endpoints up front.
    /// </summary>
    public class SkillFxClient
    {
        private readonly ICoreClientAPI capi;

        /// <summary>Drop distance, slightly beyond the server's broadcast radius.</summary>
        private const double MaxViewDistSq = 80.0 * 80.0;

        // Per-second particle budget; over it, particle effects are dropped. Arcs are a mesh and bypass it.
        private const int HardBudgetPerSec = 1600;
        private int particlesThisWindow;
        private long windowStartMs;

        private readonly BoltRenderer boltRenderer;

        public SkillFxClient(ICoreClientAPI capi, BoltRenderer boltRenderer)
        {
            this.capi = capi;
            this.boltRenderer = boltRenderer;
        }

        private int BudgetUsed()
        {
            long now = capi.World?.ElapsedMilliseconds ?? 0;
            if (now - windowStartMs > 1000) { windowStartMs = now; particlesThisWindow = 0; }
            return particlesThisWindow;
        }

        public void OnPacket(SkillFxPacket packet)
        {
            var self = capi.World?.Player?.Entity;
            if (self == null) return;
            var a = new Vec3d(packet.X, packet.Y, packet.Z);
            if (self.Pos.XYZ.SquareDistanceTo(a) > MaxViewDistSq) return;

            bool overBudget = BudgetUsed() > HardBudgetPerSec;

            var b = new Vec3d(packet.X2, packet.Y2, packet.Z2);
            var school = (SpellSchool)packet.School;
            switch ((SkillFxKind)packet.Kind)
            {
                case SkillFxKind.Arc:
                    // Nature arcs go electric blue; its green reads as vines.
                    int boltColor = school == SpellSchool.Nature
                        ? ColorUtil.ToRgba(255, 110, 190, 255)
                        : SchoolColor(school);
                    boltRenderer.Add(a, b, boltColor);
                    break;
                case SkillFxKind.Ribbon:
                    if (overBudget) break;
                    // Staggered thirds so the ribbon runs from one link to the next.
                    EmitRibbon(a, b, 0f, 0.34f);
                    capi.Event.RegisterCallback(_ => EmitRibbon(a, b, 0.34f, 0.67f), 120);
                    capi.Event.RegisterCallback(_ => EmitRibbon(a, b, 0.67f, 1.0f), 240);
                    break;
                case SkillFxKind.RingWave:
                    if (overBudget) break;
                    EmitRingWave(a, packet.Param > 0f ? packet.Param : 1.5f, school);
                    break;
                case SkillFxKind.Trail:
                    if (overBudget) break;
                    EmitTrail(a, b, school);
                    break;
                case SkillFxKind.Pillar:
                    if (overBudget) break;
                    EmitPillar(a, school);
                    break;
                case SkillFxKind.Nova:
                    if (overBudget) break;
                    EmitNova(a, packet.Param > 0f ? packet.Param : 2.5f, school);
                    break;
                case SkillFxKind.Bloom:
                    if (overBudget) break;
                    EmitBloom(a, school);
                    break;
                case SkillFxKind.Spark:
                    if (overBudget) break;
                    EmitSpark(a, b, school);
                    break;
                case SkillFxKind.Rune:
                    if (overBudget) break;
                    EmitRune(a, packet.Param > 0f ? packet.Param : 1.2f, school);
                    break;
            }
        }

        // ---- Ribbon: raised bezier of heal sparkles drifting upward ----

        private void EmitRibbon(Vec3d a, Vec3d b, float from, float to)
        {
            var world = capi.World;
            if (world == null) return;

            Vec3d axis = b.SubCopy(a);
            double len = axis.Length();
            if (len < 0.05) return;
            int n = (int)GameMath.Clamp(len / 0.4, 5, 20);

            // Control point above the midpoint, higher for longer hops.
            double lift = 1.0 + len * 0.1;
            Vec3d mid = new Vec3d(a.X + axis.X * 0.5, a.Y + axis.Y * 0.5 + lift, a.Z + axis.Z * 0.5);

            var heal = ParticleSpec.Heal();
            int color = ColorUtil.ToRgba(heal.ColorA, heal.ColorR, heal.ColorG, heal.ColorB);
            var rnd = world.Rand;

            for (int i = 0; i <= n; i++)
            {
                double t = (double)i / n;
                if (t < from || t > to) continue;
                // Quadratic bezier a-mid-b.
                double it = 1.0 - t;
                Vec3d at = new Vec3d(
                    it * it * a.X + 2 * it * t * mid.X + t * t * b.X,
                    it * it * a.Y + 2 * it * t * mid.Y + t * t * b.Y,
                    it * it * a.Z + 2 * it * t * mid.Z + t * t * b.Z);
                at.Add((rnd.NextDouble() - 0.5) * 0.15, (rnd.NextDouble() - 0.5) * 0.15, (rnd.NextDouble() - 0.5) * 0.15);

                Spawn(at, color, 0.18f, 0.28f, 0.6f, 0.15f, 0.02f);
                if ((i & 1) == 0) Spawn(at, color, 0.10f, 0.16f, 0.6f, 0.20f, 0.02f);
            }
        }

        // ---- RingWave: ground ring expanding from a point ----

        private void EmitRingWave(Vec3d center, float radius, SpellSchool school)
        {
            var world = capi.World;
            if (world == null) return;

            const int count = 40;
            const float life = 0.6f;
            particlesThisWindow += count;
            int color = school == SpellSchool.Holy || school == SpellSchool.Nature
                ? HealColor() : SchoolColor(school);
            const double r0 = 0.3;
            float speed = (float)((radius - r0) / life); // expands by velocity, no ticks

            for (int i = 0; i < count; i++)
            {
                double ang = Math.PI * 2.0 * i / count;
                double dx = Math.Cos(ang), dz = Math.Sin(ang);
                Vec3d at = new Vec3d(center.X + dx * r0, center.Y + 0.1, center.Z + dz * r0);
                var p = new SimpleParticleProperties(1, 1, color, at, at,
                    new Vec3f((float)(dx * speed), 0.03f, (float)(dz * speed)),
                    new Vec3f((float)(dx * speed), 0.08f, (float)(dz * speed)),
                    life, 0f, 0.2f, 0.3f, EnumParticleModel.Quad);
                p.MinPos.Set(at);
                p.AddPos.Set(0, 0, 0);
                p.VertexFlags = 255;
                world.SpawnParticles(p);
            }
        }

        // ---- Trail: a mote streaks a to b over ~0.2s ----

        private void EmitTrail(Vec3d a, Vec3d b, SpellSchool school)
        {
            int color = DamageColor(school);
            const int steps = 5;
            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                Vec3d at = new Vec3d(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
                int delayMs = i * 40;
                if (delayMs == 0) SpawnTrailPuff(at, color);
                else capi.Event.RegisterCallback(_ => SpawnTrailPuff(at, color), delayMs);
            }
        }

        private void SpawnTrailPuff(Vec3d at, int color)
        {
            Spawn(at, color, 0.16f, 0.28f, 0.28f, 0.05f, 0.06f);
            Spawn(at, color, 0.08f, 0.14f, 0.28f, 0.05f, 0.10f);
        }

        // ---- Pillar: a column of light/fire from the ground up on the target ----

        private void EmitPillar(Vec3d foot, SpellSchool school)
        {
            var world = capi.World;
            if (world == null) return;
            int color = DamageColor(school);
            const int rungs = 10;
            const double height = 3.0;
            for (int i = 0; i < rungs; i++)
            {
                double y = foot.Y + height * i / rungs;
                Vec3d at = new Vec3d(foot.X, y, foot.Z);
                Spawn(at, color, 0.22f, 0.4f, 0.5f, -0.4f, 0.12f);
            }
        }

        // ---- Nova: an expanding shell at chest height (vs the ground RingWave) ----

        private void EmitNova(Vec3d center, float radius, SpellSchool school)
        {
            var world = capi.World;
            if (world == null) return;

            const int count = 44;
            const float life = 0.5f;
            particlesThisWindow += count;
            int color = DamageColor(school);
            const double r0 = 0.3;
            float speed = (float)((radius - r0) / life);

            for (int i = 0; i < count; i++)
            {
                double ang = Math.PI * 2.0 * i / count;
                double dx = Math.Cos(ang), dz = Math.Sin(ang);
                float vy = (float)((i % 3 - 1) * 0.6);
                Vec3d at = new Vec3d(center.X + dx * r0, center.Y, center.Z + dz * r0);
                var p = new SimpleParticleProperties(1, 1, color, at, at,
                    new Vec3f((float)(dx * speed), vy, (float)(dz * speed)),
                    new Vec3f((float)(dx * speed), vy, (float)(dz * speed)),
                    life, 0f, 0.2f, 0.35f, EnumParticleModel.Quad);
                p.MinPos.Set(at);
                p.AddPos.Set(0, 0, 0);
                p.VertexFlags = 255;
                world.SpawnParticles(p);
            }
        }

        // ---- Bloom: a rising fountain of petals at a healed target ----

        private void EmitBloom(Vec3d at, SpellSchool school)
        {
            int color = HealColor();
            const int count = 14;
            for (int i = 0; i < count; i++)
            {
                Vec3d p = at.AddCopy(
                    (capi.World.Rand.NextDouble() - 0.5) * 0.5,
                    (capi.World.Rand.NextDouble() - 0.2) * 0.4,
                    (capi.World.Rand.NextDouble() - 0.5) * 0.5);
                Spawn(p, color, 0.14f, 0.26f, 0.7f, 0.35f, 0.06f);
            }
        }

        // ---- Spark: a sharp directional burst flying off the target, away from the caster ----

        private void EmitSpark(Vec3d target, Vec3d source, SpellSchool school)
        {
            var world = capi.World;
            if (world == null) return;

            Vec3d dir = target.SubCopy(source);
            double len = dir.Length();
            if (len < 0.05) dir = new Vec3d(0, 0.2, 0); else dir.Mul(1.0 / len);

            int color = DamageColor(school);
            const int count = 12;
            const float speed = 5f;
            particlesThisWindow += count;
            var rnd = world.Rand;
            for (int i = 0; i < count; i++)
            {
                Vec3f vel = new Vec3f(
                    (float)(dir.X * speed + (rnd.NextDouble() - 0.5) * 2.5),
                    (float)(dir.Y * speed + (rnd.NextDouble() - 0.5) * 2.5 + 1.0),
                    (float)(dir.Z * speed + (rnd.NextDouble() - 0.5) * 2.5));
                var p = new SimpleParticleProperties(1, 1, color, target, target,
                    vel, vel, 0.3f, 0.02f, 0.1f, 0.24f, EnumParticleModel.Quad);
                p.MinPos.Set(target);
                p.AddPos.Set(0, 0, 0);
                p.VertexFlags = 255;
                world.SpawnParticles(p);
            }
        }

        // ---- Rune: a flat ground sigil branded at a point, then fading (no expansion) ----

        private void EmitRune(Vec3d foot, float radius, SpellSchool school)
        {
            var world = capi.World;
            if (world == null) return;
            int color = DamageColor(school);
            const int ring = 28;
            const float life = 0.55f;
            particlesThisWindow += ring + 8;
            double y = foot.Y + 0.06;
            // Outer circle.
            for (int i = 0; i < ring; i++)
            {
                double ang = Math.PI * 2.0 * i / ring;
                Vec3d at = new Vec3d(foot.X + Math.Cos(ang) * radius, y, foot.Z + Math.Sin(ang) * radius);
                Spawn(at, color, 0.16f, 0.26f, life, 0.02f, 0.01f);
            }
            // Inner cross.
            for (int i = 0; i < 8; i++)
            {
                double f = (i - 3.5) / 3.5 * radius;
                Spawn(new Vec3d(foot.X + f, y, foot.Z), color, 0.12f, 0.2f, life, 0.02f, 0.01f);
                Spawn(new Vec3d(foot.X, y, foot.Z + f), color, 0.12f, 0.2f, life, 0.02f, 0.01f);
            }
        }

        private void Spawn(Vec3d at, int color, float minSize, float maxSize, float life, float velY, float jitter)
        {
            particlesThisWindow++;
            var p = new SimpleParticleProperties(1, 1, color, at, at,
                new Vec3f(-jitter, velY * 0.5f, -jitter), new Vec3f(jitter, velY, jitter),
                life, 0f, minSize, maxSize, EnumParticleModel.Quad);
            p.MinPos.Set(at);
            p.AddPos.Set(0, 0, 0);
            p.VertexFlags = 255; // self-lit
            capi.World.SpawnParticles(p);
        }

        private static int SchoolColor(SpellSchool school)
        {
            var s = ParticleSpec.ForSchool(school);
            return ColorUtil.ToRgba(s.ColorA, s.ColorR, s.ColorG, s.ColorB);
        }

        private static int HealColor()
        {
            var s = ParticleSpec.Heal();
            return ColorUtil.ToRgba(s.ColorA, s.ColorR, s.ColorG, s.ColorB);
        }

        /// <summary>School colour, except Nature which uses electric blue like the Arc bolt.</summary>
        private static int DamageColor(SpellSchool school)
            => school == SpellSchool.Nature
                ? ColorUtil.ToRgba(255, 110, 190, 255)
                : SchoolColor(school);
    }
}
