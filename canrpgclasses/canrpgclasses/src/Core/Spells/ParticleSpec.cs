using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace canrpgclasses.Core.Spells
{
    /// <summary>
    /// Per-spell visual: a particle burst at an impact point, emitted server-side so it reaches every nearby
    /// client. Self-contained, so a spell file tweaks colour/amount/spread without touching the spawn code.
    /// </summary>
    public class ParticleSpec
    {
        public int ColorA = 200, ColorR = 255, ColorG = 255, ColorB = 255;

        public float MinQuantity = 10f;
        public float AddQuantity = 8f;

        public float MinSize = 0.3f;
        public float MaxSize = 0.6f;

        public float LifeLength = 0.5f;
        public float Gravity;

        // Per-axis velocity spread. VelocityY biases the vertical centre (upward burst).
        public float VelocityHoriz = 1.2f;
        public float VelocityY = 0.6f;

        /// <summary>Self-illuminated particles (holy/fire/arcane look). 0 = lit by world light.</summary>
        public bool Glow;

        public EnumParticleModel Model = EnumParticleModel.Quad;

        /// <summary>Emits the burst centred on a target entity's selection box (impacts, buffs).</summary>
        public void Emit(IWorldAccessor world, Entity target)
        {
            if (world == null || target == null) return;
            var box = target.SelectionBox;
            Vec3d pos = target.Pos.XYZ;
            Vec3d min = new Vec3d(pos.X - box.XSize / 2.0, pos.Y + 0.1, pos.Z - box.ZSize / 2.0);
            Vec3d add = new Vec3d(box.XSize, System.Math.Max(0.2, box.Y2), box.ZSize);
            Emit(world, min, add);
        }

        /// <summary>Emits the burst over a box (MinPos .. MinPos+AddPos) in world space.</summary>
        public void Emit(IWorldAccessor world, Vec3d minPos, Vec3d addPos)
        {
            if (world == null) return;
            var p = new SimpleParticleProperties(
                MinQuantity, MinQuantity + AddQuantity,
                ColorUtil.ToRgba(ColorA, ColorR, ColorG, ColorB),
                minPos, minPos.AddCopy(addPos),
                new Vec3f(-VelocityHoriz, 0f, -VelocityHoriz),
                new Vec3f(VelocityHoriz, VelocityY, VelocityHoriz),
                LifeLength, Gravity, MinSize, MaxSize, Model);
            p.MinPos.Set(minPos);
            p.AddPos.Set(addPos);
            if (Glow) p.VertexFlags = 255;
            world.SpawnParticles(p);
        }

        /// <summary>Emits the burst spread over a horizontal DISC (a circular carpet) centred on <paramref name="center"/>,
        /// instead of a square box - for round AoE zones (Hallowed Ground). Particles are placed with area-uniform polar
        /// sampling (r = radius·√u) so they don't bunch up in the middle.</summary>
        public void EmitDisc(IWorldAccessor world, Vec3d center, float radius)
        {
            if (world == null || radius <= 0f) return;
            var rnd = world.Rand;
            int n = (int)System.Math.Round(MinQuantity + AddQuantity * 0.5f);
            for (int i = 0; i < n; i++)
            {
                double ang = rnd.NextDouble() * System.Math.PI * 2.0;
                double rr = radius * System.Math.Sqrt(rnd.NextDouble());
                Vec3d at = new Vec3d(center.X + System.Math.Cos(ang) * rr, center.Y + 0.05, center.Z + System.Math.Sin(ang) * rr);
                var p = new SimpleParticleProperties(
                    1, 1,
                    ColorUtil.ToRgba(ColorA, ColorR, ColorG, ColorB),
                    at, at,
                    new Vec3f(-VelocityHoriz, 0f, -VelocityHoriz),
                    new Vec3f(VelocityHoriz, VelocityY, VelocityHoriz),
                    LifeLength, Gravity, MinSize, MaxSize, Model);
                p.MinPos.Set(at);
                p.AddPos.Set(0, 0.25, 0);
                if (Glow) p.VertexFlags = 255;
                world.SpawnParticles(p);
            }
        }

        public static ParticleSpec ForSchool(SpellSchool school)
        {
            return school switch
            {
                SpellSchool.Holy    => new ParticleSpec { ColorA = 220, ColorR = 255, ColorG = 230, ColorB = 130, Glow = true, VelocityY = 0.8f },
                SpellSchool.Fire    => new ParticleSpec { ColorA = 220, ColorR = 255, ColorG = 140, ColorB = 40,  Glow = true, Gravity = -0.1f },
                SpellSchool.Frost   => new ParticleSpec { ColorA = 220, ColorR = 150, ColorG = 220, ColorB = 255, Glow = true },
                SpellSchool.Arcane  => new ParticleSpec { ColorA = 220, ColorR = 200, ColorG = 120, ColorB = 255, Glow = true },
                SpellSchool.Nature  => new ParticleSpec { ColorA = 220, ColorR = 130, ColorG = 230, ColorB = 110 },
                SpellSchool.Shadow  => new ParticleSpec { ColorA = 220, ColorR = 120, ColorG = 60,  ColorB = 150, Glow = true },
                _                   => new ParticleSpec { ColorA = 220, ColorR = 200, ColorG = 40,  ColorB = 40 }, // physical: blood red
            };
        }

        public static ParticleSpec Heal()
            => new ParticleSpec { ColorA = 220, ColorR = 120, ColorG = 255, ColorB = 130, Glow = true, Gravity = -0.2f, VelocityY = 1.0f, MinQuantity = 12f, AddQuantity = 10f };
    }
}
