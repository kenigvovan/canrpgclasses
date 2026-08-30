using canrpgclasses.Core.Net;
using canrpgclasses.Core.Spells;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace canrpgclasses.Core.Visuals
{
    /// <summary>
    /// Server-side one-shot skill visuals: broadcasts a small packet per event and each client builds the
    /// particles locally. This is the "between two points" layer (chain hops, retaliation arcs) that the
    /// burst-only <see cref="ParticleSpec"/> can't do. No-op on the client or before the channel exists.
    /// </summary>
    public static class SkillFx
    {
        /// <summary>How far the effect is broadcast; matches the cast-bar broadcast so anything you can watch
        /// being cast, you can also see land.</summary>
        public const float BroadcastRadius = 64f;

        public static void Arc(IWorldAccessor world, Entity a, Entity b, SpellSchool school)
            => Send(world, SkillFxKind.Arc, Anchor(a), Anchor(b), school, 0f);

        public static void Arc(IWorldAccessor world, Vec3d a, Vec3d b, SpellSchool school)
            => Send(world, SkillFxKind.Arc, a, b, school, 0f);

        public static void Ribbon(IWorldAccessor world, Entity a, Entity b, SpellSchool school)
            => Send(world, SkillFxKind.Ribbon, Anchor(a), Anchor(b), school, 0f);

        public static void RingWave(IWorldAccessor world, Vec3d center, float radius, SpellSchool school)
            => Send(world, SkillFxKind.RingWave, center, center, school, radius);

        /// <summary>A mote streaks caster → target: a direct, projectile-less nuke becomes visibly delivered.</summary>
        public static void Trail(IWorldAccessor world, Entity a, Entity b, SpellSchool school)
            => Send(world, SkillFxKind.Trail, Anchor(a), Anchor(b), school, 0f);

        /// <summary>A column strikes down on the target: a struck-from-above accent.</summary>
        public static void Pillar(IWorldAccessor world, Entity at, SpellSchool school)
            => Send(world, SkillFxKind.Pillar, Foot(at), Foot(at), school, 0f);

        /// <summary>An expanding shell burst at the target's chest (a mid-air nova, distinct from the ground ring).</summary>
        public static void Nova(IWorldAccessor world, Entity at, float radius, SpellSchool school)
            => Send(world, SkillFxKind.Nova, Anchor(at), Anchor(at), school, radius);

        /// <summary>A rising fountain of petals at a healed target - a single-target heal accent.</summary>
        public static void Bloom(IWorldAccessor world, Entity at, SpellSchool school)
            => Send(world, SkillFxKind.Bloom, Anchor(at), Anchor(at), school, 0f);

        /// <summary>A sharp burst flying off the target, away from the caster. A carries the target, B the source,
        /// so the client draws the spray along source → target.</summary>
        public static void Spark(IWorldAccessor world, Entity target, Entity source, SpellSchool school)
            => Send(world, SkillFxKind.Spark, Anchor(target), Anchor(source), school, 0f);

        /// <summary>A flat ground sigil branded under the target, then fading.</summary>
        public static void Rune(IWorldAccessor world, Entity at, float radius, SpellSchool school)
            => Send(world, SkillFxKind.Rune, Foot(at), Foot(at), school, radius);

        /// <summary>A flat ground sigil at a fixed world point (a zone centre), then fading.</summary>
        public static void Rune(IWorldAccessor world, Vec3d at, float radius, SpellSchool school)
            => Send(world, SkillFxKind.Rune, at, at, school, radius);

        /// <summary>Chest height - where a bolt visually connects to a body (feet for a ring, but rings pass
        /// their own center).</summary>
        private static Vec3d Anchor(Entity e)
        {
            var pos = e.Pos.XYZ;
            pos.Y += e.SelectionBox.Y2 * 0.6;
            return pos;
        }

        private static Vec3d Foot(Entity e) => e.Pos.XYZ;

        private static void Send(IWorldAccessor world, SkillFxKind kind, Vec3d a, Vec3d b, SpellSchool school, float param)
        {
            if (world == null || world.Side != EnumAppSide.Server) return;
            var mod = canrpgclassesModSystem.For(world.Api);
            if (mod?.ServerChannel == null) return;

            var packet = new SkillFxPacket
            {
                Kind = (byte)kind,
                School = (byte)school,
                X = a.X, Y = a.Y, Z = a.Z,
                X2 = b.X, Y2 = b.Y, Z2 = b.Z,
                Param = param
            };
            foreach (var p in world.GetPlayersAround(a, BroadcastRadius, BroadcastRadius))
            {
                if (p is IServerPlayer sp) mod.ServerChannel.SendPacket(packet, sp);
            }
        }
    }
}
