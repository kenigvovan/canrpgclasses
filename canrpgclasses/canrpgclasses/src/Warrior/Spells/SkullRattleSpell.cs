using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    [SpellRegistration("canrpgclasses:skull_rattle")]
    public class SkullRattleSpell : Spell
    {
        public SkullRattleSpell()
        {
            var b = Balance;
            DisplayName = "Skull Rattle";
            IconName = "knockout";
            School = SpellSchool.PhysicalMelee;
            Tier = 3;
            Range = 4;

            Target.Type = TargetType.Aim;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = b.F("coeff", 1.0f),
                Knockback = 0f
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Stun,
                StatusEffectDuration = b.F("stun", 2f)
            });

            ConfigureCost(defResource: 15f, defCooldown: 15f); // rage
        }
    }
}
