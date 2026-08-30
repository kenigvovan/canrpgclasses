using System.Collections.Generic;
using Vintagestory.API.Common;

namespace canrpgclasses.Core.Spells
{
    /// <summary>
    /// Maps a <see cref="SpellSchool"/> to the engine damage type. Worn armor only reduces Blunt/Slashing/Piercing,
    /// so magical schools become <see cref="EnumDamageType.Injury"/> - armor-piercing, with no elemental baggage.
    /// Fire and Frost keep their real type so vanilla burning and cold still apply.
    /// </summary>
    public static class DamageSchools
    {
        public sealed class Def
        {
            public readonly EnumDamageType EngineType;
            /// <summary>True if worn armor mitigates it (the three physical types). Magical schools are false.</summary>
            public readonly bool Physical;
            /// <summary>1.0-based resist stat key, read via StatExtensions.ReductionStat.</summary>
            public readonly string ResistStat;
            /// <summary>Its mirror on the attacker: penetration, subtracted from the victim's resist.</summary>
            public readonly string PenStat;

            public Def(EnumDamageType engineType, bool physical, string resistStat)
            {
                EngineType = engineType;
                Physical = physical;
                ResistStat = resistStat;
                PenStat = resistStat.Replace("canrpgResist_", "canrpgPen_");
            }
        }

        private static readonly Dictionary<SpellSchool, Def> Defs = new()
        {
            [SpellSchool.PhysicalMelee]  = new Def(EnumDamageType.PiercingAttack, true,  "canrpgResist_physical"),
            [SpellSchool.PhysicalRanged] = new Def(EnumDamageType.PiercingAttack, true,  "canrpgResist_physical"),
            [SpellSchool.Fire]   = new Def(EnumDamageType.Fire,   false, "canrpgResist_fire"),
            [SpellSchool.Frost]  = new Def(EnumDamageType.Frost,  false, "canrpgResist_frost"),
            [SpellSchool.Holy]   = new Def(EnumDamageType.Injury, false, "canrpgResist_holy"),
            [SpellSchool.Shadow] = new Def(EnumDamageType.Injury, false, "canrpgResist_shadow"),
            [SpellSchool.Fel]    = new Def(EnumDamageType.Injury, false, "canrpgResist_fel"),
            [SpellSchool.Arcane] = new Def(EnumDamageType.Injury, false, "canrpgResist_arcane"),
            [SpellSchool.Nature] = new Def(EnumDamageType.Injury, false, "canrpgResist_nature"),
        };

        public static Def For(SpellSchool school) => Defs.TryGetValue(school, out var d) ? d : Defs[SpellSchool.PhysicalMelee];
    }

    /// <summary>A <see cref="DamageSource"/> carrying the spell's school, so the damage patch can apply school
    /// resists while the engine still sees an ordinary DamageSource.</summary>
    public class CanrpgDamageSource : DamageSource
    {
        public SpellSchool School = SpellSchool.PhysicalMelee;
    }
}
