using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    /// <summary>Slow fall on the mage or an ally: fall speed is capped and there's no fall damage.</summary>
    [SpellRegistration("canrpgclasses:feather_fall")]
    public class FeatherFallSpell : Spell
    {
        public FeatherFallSpell()
        {
            var b = Balance;
            DisplayName = "Feather Fall";
            IconName = "two-feathers";
            School = SpellSchool.Arcane;
            Range = 20;
            TriggersGlobalCooldown = false;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct;

            int duration = (int)b.F("duration", 20f);
            DescArgs = new object[] { duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = UtilityEffectIds.SlowFall,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.ForSchool(SpellSchool.Arcane)
            });

            ConfigureCost(defResource: 15f, defCooldown: 20f);
        }
    }
}
