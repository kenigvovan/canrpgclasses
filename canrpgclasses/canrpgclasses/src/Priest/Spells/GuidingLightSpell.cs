using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    /// <summary>A light that follows the priest, like a torch that needs no hand.</summary>
    [SpellRegistration("canrpgclasses:guiding_light")]
    public class GuidingLightSpell : Spell
    {
        public GuidingLightSpell()
        {
            var b = Balance;
            DisplayName = "Guiding Light";
            IconName = "sunbeams";
            School = SpellSchool.Holy;
            TriggersGlobalCooldown = false;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            int duration = (int)b.F("duration", 600f);
            DescArgs = new object[] { duration / 60 };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = UtilityEffectIds.MagicLight,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 10f, defCooldown: 5f);
        }
    }
}
