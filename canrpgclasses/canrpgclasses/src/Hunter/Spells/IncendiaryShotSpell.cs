using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    [SpellRegistration("canrpgclasses:incendiary_shot")]
    public class IncendiaryShotSpell : Spell
    {
        public IncendiaryShotSpell()
        {
            var b = Balance;
            DisplayName = "Incendiary Shot";
            IconName = "fire-dash";
            School = SpellSchool.Fire;
            Tier = 3;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            RequiresBow = true;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.EmpowerNextShot,
                DamageSpellPowerCoefficient = b.F("coeff", 0.6f),
                ShotIgnite = true,
                EmpowerWindowSeconds = b.F("window", 6f),
                Particles = new ParticleSpec { ColorA = 220, ColorR = 240, ColorG = 130, ColorB = 30, Glow = true, VelocityY = 0.3f }
            });

            ConfigureCost(defResource: 25f, defCooldown: 10f); // focus
        }
    }
}
