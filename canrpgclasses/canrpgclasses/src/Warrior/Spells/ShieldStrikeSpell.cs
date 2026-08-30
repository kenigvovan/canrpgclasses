using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    [SpellRegistration("canrpgclasses:shield_strike")]
    public class ShieldStrikeSpell : Spell
    {
        public ShieldStrikeSpell()
        {
            var b = Balance;
            DisplayName = "Shield Strike";
            IconName = "cross-shield";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;
            RequiresShield = true;
            Range = 4;

            Target.Type = TargetType.Aim;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = b.F("coeff", 2.5f),
                Knockback = b.F("knockback", 0.3f),
                Particles = ParticleSpec.ForSchool(SpellSchool.PhysicalMelee)
            });

            ConfigureCost(defResource: 15f, defCooldown: 6f); // rage
        }
    }
}
