using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    [SpellRegistration("canrpgclasses:jarring_shot")]
    public class JarringShotSpell : Spell
    {
        public JarringShotSpell()
        {
            var b = Balance;
            DisplayName = "Jarring Shot";
            IconName = "knockout";
            School = SpellSchool.PhysicalRanged;
            Tier = 2;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            RequiresBow = true;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.EmpowerNextShot,
                DamageSpellPowerCoefficient = b.F("coeff", 0.5f),
                StatusEffectId = "walkslow",
                StatusEffectDuration = b.F("slowDuration", 5f),
                StatusEffectAmplifier = b.I("slowAmp", 2),
                EmpowerWindowSeconds = b.F("window", 6f),
                Particles = ParticleSpec.ForSchool(SpellSchool.PhysicalRanged)
            });

            ConfigureCost(defResource: 20f, defCooldown: 9f); // focus
        }
    }
}
