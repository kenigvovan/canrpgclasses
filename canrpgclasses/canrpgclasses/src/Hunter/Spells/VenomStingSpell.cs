using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    [SpellRegistration("canrpgclasses:venom_sting")]
    public class VenomStingSpell : Spell
    {
        public VenomStingSpell()
        {
            var b = Balance;
            DisplayName = "Venom Sting";
            IconName = "burning-dot";
            School = SpellSchool.PhysicalRanged;
            Tier = 2;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            RequiresBow = true;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.EmpowerNextShot,
                DamageSpellPowerCoefficient = b.F("coeff", 0.6f),
                StatusEffectId = "poison",
                StatusEffectDuration = b.F("poisonDuration", 8f),
                StatusEffectAmplifier = b.I("poisonAmp", 2),
                EmpowerWindowSeconds = b.F("window", 6f),
                Particles = new ParticleSpec { ColorA = 220, ColorR = 130, ColorG = 200, ColorB = 60, Gravity = -0.1f }
            });

            ConfigureCost(defResource: 20f, defCooldown: 6f); // focus
        }
    }
}
