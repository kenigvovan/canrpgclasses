using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    /// <summary>Mobs flee via their own AI; a feared player instead loses control and stumbles.</summary>
    [SpellRegistration("canrpgclasses:terrifying_scream")]
    public class TerrifyingScreamSpell : Spell
    {
        public TerrifyingScreamSpell()
        {
            var b = Balance;
            DisplayName = "Terrifying Scream";
            IconName = "screaming";
            School = SpellSchool.Shadow;
            Tier = 4;
            Range = b.F("range", 6f);

            Target.Type = TargetType.Area;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            int duration = (int)b.F("duration", 5f);
            DescArgs = new object[] { duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Fear,
                StatusEffectDuration = duration,
                NovaFx = true, // a shadow shell bursts out on each feared enemy
                Particles = ParticleSpec.ForSchool(SpellSchool.Shadow)
            });

            ConfigureCost(defResource: 30f, defCooldown: 45f); // mana
        }
    }
}
