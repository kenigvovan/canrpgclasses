using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    /// <summary>Its own id, so it balances independently of the paladin's Cleanse.</summary>
    [SpellRegistration("canrpgclasses:purify")]
    public class PurifySpell : Spell
    {
        public PurifySpell()
        {
            DisplayName = "Purify";
            IconName = "pouring-chalice";
            School = SpellSchool.Holy;
            Tier = 1;
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
