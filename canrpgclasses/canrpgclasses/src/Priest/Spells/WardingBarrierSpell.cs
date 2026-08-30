using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    [SpellRegistration("canrpgclasses:warding_barrier")]
    public class WardingBarrierSpell : Spell
    {
        public WardingBarrierSpell()
        {
            var b = Balance;
            DisplayName = "Warding Barrier";
            IconName = "shield-echoes";
            School = SpellSchool.Holy;
            Tier = 5;
            Range = b.F("range", 8f);

            Target.Type = TargetType.Area;
            Target.Affinity = TargetAffinity.Ally;
            Target.AreaIncludeCaster = true;
            Deliver.Type = DeliveryType.Direct;

            int shieldDuration = (int)b.F("shieldDuration", 10f);
            DescArgs = new object[] { shieldDuration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Shield,
                ShieldSpellPowerCoefficient = b.F("shieldCoeff", 6f),
                ShieldDurationSeconds = shieldDuration,
                Particles = new ParticleSpec { ColorA = 200, ColorR = 235, ColorG = 225, ColorB = 160, Glow = true, VelocityY = 0.8f }
            });

            ConfigureCost(defResource: 60f, defCooldown: 120f); // mana
        }
    }
}
