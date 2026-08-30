using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    [SpellRegistration("canrpgclasses:second_wind")]
    public class SecondWindSpell : Spell
    {
        public SecondWindSpell()
        {
            var b = Balance;
            DisplayName = "Second Wind";
            IconName = "embrassed-energy";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = b.F("healCoeff", 7f),
                Particles = ParticleSpec.Heal() // green healing sparkle on yourself
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "regeneration",
                StatusEffectDuration = b.F("regenDuration", 8f),
                StatusEffectAmplifier = b.I("regenAmp", 2),
                StatusEffectAmplifierCap = b.I("regenCap", 3)
            });

            ConfigureCost(defResource: 35f, defCooldown: 30f); // energy
        }
    }
}
