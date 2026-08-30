using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    [SpellRegistration("canrpgclasses:cleanse")]
    public class CleanseSpell : Spell
    {
        public CleanseSpell()
        {
            DisplayName = "Cleanse";
            IconName = "swirl-string";
            School = SpellSchool.Holy;
            Tier = 2;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Cleanse,
                CleanseMax = 0, // remove all negative effects
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 20f, defCooldown: 8f); // mana
        }
    }
}
