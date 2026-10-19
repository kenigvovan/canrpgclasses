using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Progression;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Core.EB
{
    /// <summary>Spell power per school (spell damage = SpellPower x coeff): level multiplier, vanilla weapon-damage
    /// blend for physical schools, times the <c>spellpower_&lt;school&gt;</c> stat.</summary>
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
            // Physical spells fold in the vanilla weapon-damage stat (guarding a 0 baseline), so bow talents
            // and affinities also buff hunter shots.
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

            // No seeding needed: GetBlended returns 1f for an untouched stat. Floored at 10% so a big debuff
            // still cuts spell power.
            float explicitSp = entity.Stats.GetBlended(StatKey(school));
            float schoolMul = Math.Max(0.1f, explicitSp);

            return BaseSpellPower * levelMul * gearMul * schoolMul;
        }
    }
}
