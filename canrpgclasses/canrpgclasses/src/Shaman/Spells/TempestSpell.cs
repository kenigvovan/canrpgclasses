using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    /// <summary>The knockback radiates from the shaman, so everyone caught is thrown outward.</summary>
    [SpellRegistration("canrpgclasses:tempest")]
    public class TempestSpell : Spell
    {
        public TempestSpell()
        {
            var b = Balance;
            DisplayName = "Tempest";
            IconName = "thunder-struck";
            School = SpellSchool.Nature;
            Tier = 4;
            Range = b.F("radius", 7f);

            Target.Type = TargetType.Area;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;

            float coeff = b.F("coeff", 1.2f);
            DescArgs = new object[] { coeff, (int)b.F("radius", 7f) };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = b.F("knockback", 2.0f),
                NovaFx = true,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });

            ConfigureCost(defResource: 25f, defCooldown: 30f); // mana
        }
    }
}
