using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    /// <summary>A player target really has their model swapped to a sheep; a mob is only frozen with a puff of
    /// particles.</summary>
    [SpellRegistration("canrpgclasses:transmute")]
    public class TransmuteSpell : Spell
    {
        public TransmuteSpell()
        {
            var b = Balance;
            DisplayName = "Transmute";
            IconName = "sheep";
            School = SpellSchool.Arcane;
            Tier = 4;
            Range = 24;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 1.9f);

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            int duration = (int)b.F("duration", 8f);
            DescArgs = new object[] { duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Transmute,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.ForSchool(SpellSchool.Arcane)
            });

            ConfigureCost(defResource: 25f, defCooldown: 30f); // mana
        }
    }
}
