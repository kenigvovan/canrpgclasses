using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    [SpellRegistration("canrpgclasses:final_stand")]
    public class FinalStandSpell : Spell
    {
        public FinalStandSpell()
        {
            var b = Balance;
            DisplayName = "Final Stand";
            IconName = "ribcage";
            School = SpellSchool.PhysicalMelee;
            Tier = 3;
            RequiresShield = true;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Shield,
                ShieldSpellPowerCoefficient = b.F("shieldCoeff", 6f),
                ShieldDurationSeconds = b.F("shieldDuration", 10f),
                Particles = new ParticleSpec { ColorA = 200, ColorR = 220, ColorG = 200, ColorB = 160, Glow = true, VelocityY = 0.8f }
            });

            ConfigureCost(defResource: 25f, defCooldown: 90f); // rage
        }
    }
}
