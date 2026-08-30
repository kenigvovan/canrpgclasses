using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    [SpellRegistration("canrpgclasses:second_breath")]
    public class SecondBreathSpell : Spell
    {
        public SecondBreathSpell()
        {
            var b = Balance;
            DisplayName = "Second Breath";
            IconName = "bandage-roll";
            School = SpellSchool.Nature;
            Tier = 2;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = b.F("healCoeff", 2f),
                HealMissingHealthFraction = b.F("missingFraction", 0.4f),
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 30f, defCooldown: 30f); // focus
        }
    }
}
