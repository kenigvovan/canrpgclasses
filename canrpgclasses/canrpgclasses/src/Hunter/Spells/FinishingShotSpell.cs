using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    /// <summary>The combo points go at cast and the bonus is snapshotted onto the shot.</summary>
    [SpellRegistration("canrpgclasses:finishing_shot")]
    public class FinishingShotSpell : Spell
    {
        public FinishingShotSpell()
        {
            var b = Balance;
            DisplayName = "Finishing Shot";
            IconName = "pierced-body";
            School = SpellSchool.PhysicalRanged;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            RequiresBow = true;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.EmpowerNextShot,
                DamageSpellPowerCoefficient = b.F("coeff", 1.5f),
                DamagePerComboPoint = b.F("perCombo", 1.2f),
                Knockback = b.F("knockback", 0.5f),
                EmpowerWindowSeconds = b.F("window", 6f),
                Particles = ParticleSpec.ForSchool(SpellSchool.PhysicalRanged)
            });

            ComboFinisher = true;
            ConfigureCost(defResource: 30f, defCooldown: 8f); // focus
        }
    }
}
