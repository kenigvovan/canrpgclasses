using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    /// <summary>The combo point lands when the arrow hits - a miss builds nothing.</summary>
    [SpellRegistration("canrpgclasses:measured_shot")]
    public class MeasuredShotSpell : Spell
    {
        public MeasuredShotSpell()
        {
            var b = Balance;
            DisplayName = "Measured Shot";
            IconName = "air-zigzag";
            School = SpellSchool.PhysicalRanged;
            Tier = 1;

            Target.Type = TargetType.Caster; // arms the next real bow shot instead of firing now
            Deliver.Type = DeliveryType.Direct;
            RequiresBow = true;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.EmpowerNextShot,
                DamageSpellPowerCoefficient = b.F("coeff", 1.5f),
                Knockback = b.F("knockback", 0.2f),
                EmpowerWindowSeconds = b.F("window", 6f),
                Particles = ParticleSpec.ForSchool(SpellSchool.PhysicalRanged)
            });

            ComboBuilder = true;
            ConfigureCost(defResource: 12f, defCooldown: 1.5f); // roughly break-even with focus regen when spammed
        }
    }
}
