using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    /// <summary>The extra arrows are real vanilla arrows spawned on release; the bonus damage lands on the
    /// first one to hit.</summary>
    [SpellRegistration("canrpgclasses:split_shot")]
    public class SplitShotSpell : Spell
    {
        public SplitShotSpell()
        {
            var b = Balance;
            DisplayName = "Split Shot";
            IconName = "sword-array";
            School = SpellSchool.PhysicalRanged;
            Tier = 2;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            RequiresBow = true;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.EmpowerNextShot,
                DamageSpellPowerCoefficient = b.F("coeff", 0.6f),
                ShotExtraArrows = b.I("extraArrows", 4),
                EmpowerWindowSeconds = b.F("window", 6f),
                Particles = ParticleSpec.ForSchool(SpellSchool.PhysicalRanged)
            });

            ConfigureCost(defResource: 35f, defCooldown: 10f); // focus
        }
    }
}
