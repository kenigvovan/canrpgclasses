using canrpgclasses.Core.Spells;

namespace canrpgclasses.Druid.Spells
{
    [SpellRegistration("canrpgclasses:nature_bolt")]
    public class NatureBoltSpell : Spell
    {
        public NatureBoltSpell()
        {
            var b = Balance;
            DisplayName = "Nature Bolt";
            IconName = "bolt-spell-cast";
            School = SpellSchool.Nature;
            Tier = 1;
            Range = 24;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 2.0f);

            float coeff = b.F("coeff", 2.0f);
            DescArgs = new object[] { coeff };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0f,
                TrailFx = true,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });

            ConfigureCost(defResource: 20f, defCooldown: 0f); // mana
        }
    }
}
