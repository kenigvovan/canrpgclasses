using ProtoBuf;

namespace canrpgclasses.Core.Net
{
    /// <summary>One-shot visuals for <see cref="canrpgclasses.Core.Visuals.SkillFx"/>; clients build all geometry from
    /// the endpoints, so each event costs one small packet.</summary>
    public enum SkillFxKind : byte
    {
        /// <summary>Jagged flickering lightning A→B: a damage hop (chain lightning, shield retaliation).</summary>
        Arc = 0,
        /// <summary>Smooth raised ribbon A→B drifting upward: a heal hop (chain heal).</summary>
        Ribbon = 1,
        /// <summary>Ground ring expanding from a point: an AoE/heal landing.</summary>
        RingWave = 2,
        /// <summary>Reserved: the channel beam is driven by watched-attribute state, not this packet.</summary>
        Beam = 3,
        /// <summary>A mote streaks A→B over ~0.2s: makes a direct, projectile-less nuke visibly travel (Mystic Blast).</summary>
        Trail = 4,
        /// <summary>A column of light/fire strikes down on a point: a struck-from-above accent (Sacred Flame). A=B=foot.</summary>
        Pillar = 5,
        /// <summary>An expanding shell burst at chest height (vs the ground RingWave): a mid-air nova (Earthen Jolt).
        /// Param = radius. A=B=chest point.</summary>
        Nova = 6,
        /// <summary>A fountain of petals rising at a healed target - a single-target heal accent (Swift Heal). A=B=chest.</summary>
        Bloom = 7,
        /// <summary>A sharp directional burst flying off the target, away from the caster (Mind Shock). A=target, B=source.</summary>
        Spark = 8,
        /// <summary>A flat ground sigil branded at a point, then fading (Sentence). Param = radius. A=B=foot.</summary>
        Rune = 9,
    }

    /// <summary>server → nearby clients: play a one-shot skill visual between two world points (or at one, for
    /// RingWave). School picks the colour on the client (single palette source: ParticleSpec.ForSchool).</summary>
    [ProtoContract]
    public class SkillFxPacket
    {
        [ProtoMember(1)] public byte Kind;
        [ProtoMember(2)] public byte School;
        [ProtoMember(3)] public double X;
        [ProtoMember(4)] public double Y;
        [ProtoMember(5)] public double Z;
        [ProtoMember(6)] public double X2;
        [ProtoMember(7)] public double Y2;
        [ProtoMember(8)] public double Z2;
        /// <summary>RingWave: final radius. Arc/Ribbon: 0 = default look.</summary>
        [ProtoMember(9)] public float Param;
    }
}
