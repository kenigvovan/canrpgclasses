using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    /// <summary>Walk on water and breathe under it, on the shaman or an ally. Sneak to dive.</summary>
    [SpellRegistration("canrpgclasses:water_walking")]
    public class WaterWalkingSpell : Spell
    {
        public WaterWalkingSpell()
        {
            var b = Balance;
            DisplayName = "Water Walking";
            IconName = "wave-crest";
            School = SpellSchool.Nature;
            Range = 20;
            TriggersGlobalCooldown = false;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct;

            int duration = (int)b.F("duration", 600f);
            DescArgs = new object[] { duration / 60 };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = UtilityEffectIds.WaterWalk,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });

            ConfigureCost(defResource: 15f, defCooldown: 5f);
        }
    }
}
