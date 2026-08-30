using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    [SpellRegistration("canrpgclasses:earthen_jolt")]
    public class EarthenJoltSpell : Spell
    {
        public EarthenJoltSpell()
        {
            var b = Balance;
            DisplayName = "Earthen Jolt";
            IconName = "peaks";
            School = SpellSchool.Nature;
            Tier = 1;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Instant;

            float coeff = b.F("coeff", 1.5f);
            DescArgs = new object[] { coeff };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0f,
                NovaFx = true,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });

            ConfigureCost(defResource: 25f, defCooldown: 6f); // mana
        }
    }
}
