using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:mystic_burst")]
    public class MysticBurstSpell : Spell
    {
        public MysticBurstSpell()
        {
            var b = Balance;
            DisplayName = "Mystic Burst";
            IconName = "star-satellites";
            School = SpellSchool.Arcane;
            Tier = 3;
            Range = b.F("range", 5f);

            Target.Type = TargetType.Area;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            // A spender: it hits with the charged arcane spell power, then the charges are stripped.
            ConsumesCasterEffectId = MageEffectIds.MysticCharges;

            float coeff = b.F("coeff", 1.5f);
            DescArgs = new object[] { coeff };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0f,
                Particles = ParticleSpec.ForSchool(SpellSchool.Arcane)
            });

            ConfigureCost(defResource: 35f, defCooldown: 6f); // mana
        }
    }
}
