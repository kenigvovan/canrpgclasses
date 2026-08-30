using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    /// <summary>The bonus needs a full draw begun after this was pressed - no pre-drawing the bow for a free
    /// hit, and a snap shot fires plain.</summary>
    [SpellRegistration("canrpgclasses:careful_shot")]
    public class CarefulShotSpell : Spell
    {
        public CarefulShotSpell()
        {
            var b = Balance;
            DisplayName = "Careful Shot";
            IconName = "deadly-strike";
            School = SpellSchool.PhysicalRanged;
            Tier = 2;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            RequiresBow = true;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.EmpowerNextShot,
                DamageSpellPowerCoefficient = b.F("coeff", 4f),
                Knockback = b.F("knockback", 0.5f),
                EmpowerWindowSeconds = b.F("window", 6f),
                ShotRequiresDrawFraction = b.F("drawFraction", 1.2f), // held past full - a deliberate over-aim
                Particles = ParticleSpec.ForSchool(SpellSchool.PhysicalRanged)
            });

            ConfigureCost(defResource: 30f, defCooldown: 8f); // focus
        }
    }
}
