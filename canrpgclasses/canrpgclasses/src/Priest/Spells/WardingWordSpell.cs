using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    [SpellRegistration("canrpgclasses:warding_word")]
    public class WardingWordSpell : Spell
    {
        public WardingWordSpell()
        {
            var b = Balance;
            DisplayName = "Warding Word";
            IconName = "surrounded-shield";
            School = SpellSchool.Holy;
            Tier = 1;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct;

            float shieldDuration = b.F("shieldDuration", 15f);
            DescArgs = new object[] { (int)shieldDuration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Shield,
                ShieldSpellPowerCoefficient = b.F("shieldCoeff", 8f),
                ShieldDurationSeconds = shieldDuration,
                Particles = new ParticleSpec { ColorA = 200, ColorR = 235, ColorG = 225, ColorB = 160, Glow = true, VelocityY = 0.8f }
            });

            ConfigureCost(defResource: 30f, defCooldown: 12f); // mana
        }
    }
}
