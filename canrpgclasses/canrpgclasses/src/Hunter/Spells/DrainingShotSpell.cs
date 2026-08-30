using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    [SpellRegistration("canrpgclasses:draining_shot")]
    public class DrainingShotSpell : Spell
    {
        public DrainingShotSpell()
        {
            var b = Balance;
            DisplayName = "Draining Shot";
            IconName = "drop";
            School = SpellSchool.PhysicalRanged;
            Tier = 3;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            RequiresBow = true;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.EmpowerNextShot,
                DamageSpellPowerCoefficient = b.F("coeff", 0.5f),
                ShotResourceDrain = b.F("drain", 25f),
                EmpowerWindowSeconds = b.F("window", 6f),
                Particles = new ParticleSpec { ColorA = 220, ColorR = 60, ColorG = 120, ColorB = 220, Gravity = -0.05f }
            });

            ConfigureCost(defResource: 20f, defCooldown: 10f); // focus
        }
    }
}
