using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:freezing_burst")]
    public class FreezingBurstSpell : Spell
    {
        public FreezingBurstSpell()
        {
            var b = Balance;
            DisplayName = "Freezing Burst";
            IconName = "snowflake-1";
            School = SpellSchool.Frost;
            Tier = 2;
            Range = b.F("range", 6f);

            Target.Type = TargetType.Area;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            int duration = (int)b.F("duration", 4f);
            DescArgs = new object[] { duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Root,
                StatusEffectDuration = duration,
                NovaFx = true, // a shell of ice bursts on each rooted enemy
                Particles = ParticleSpec.ForSchool(SpellSchool.Frost)
            });

            ConfigureCost(defResource: 30f, defCooldown: 20f); // mana
        }
    }
}
