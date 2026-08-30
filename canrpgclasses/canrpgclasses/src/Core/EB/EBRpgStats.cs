using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Progression;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Core.EB
{
    /// <summary>
    /// The caster's spell power per school. Spell damage is <c>SpellPower × coeff</c>, so this is what makes
    /// spells scale: a steady multiplier from character level, and for physical schools the vanilla weapon-damage
    /// blend (weapon tier, talents, gear affinity). A <c>spellpower_&lt;school&gt;</c> stat multiplies on top.
    /// </summary>
    public class EBRpgStats : EntityBehavior
    {
        public const string Name = "canrpgrpgstats";
        public const float BaseSpellPower = 1f;
        private static float PerLevel => BalanceConfig.Global("spellPowerPerLevel", 0.05f);

        public EBRpgStats(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        public static string StatKey(SpellSchool school) => StatKeys.SpellpowerFor(school);

        public float GetSpellPower(SpellSchool school)
        {
            int level = entity.GetBehavior<EBProgression>()?.Level ?? 1;
            float levelMul = 1f + Math.Max(0, level - 1) * PerLevel;

            float gearMul = 1f;
            // Physical spells fold in the matching vanilla weapon-damage stat, guarding a 0 baseline. Ranged
            // reads rangedWeaponsDamage, so bow talents and affinities buff the hunter's special shots too.
            if (school == SpellSchool.PhysicalMelee)
            {
                float md = entity.Stats.GetBlended(StatKeys.MeleeWeaponsDamage);
                gearMul = md > 0.01f ? md : 1f;
            }
            else if (school == SpellSchool.PhysicalRanged)
            {
                float rd = entity.Stats.GetBlended(StatKeys.RangedWeaponsDamage);
                gearMul = rd > 0.01f ? rd : 1f;
            }

            // spellpower_<school> needs no seeding: EntityFloatStats gives the category an implicit base of 1.0,
            // and GetBlended returns 1f for one never touched. Floored at 10%, so a big debuff cuts spell power
            // instead of silently snapping back to full.
            float explicitSp = entity.Stats.GetBlended(StatKey(school));
            float schoolMul = Math.Max(0.1f, explicitSp);

            return BaseSpellPower * levelMul * gearMul * schoolMul;
        }
    }
}
