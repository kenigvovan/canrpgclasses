using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    /// <summary>Both the damage tick and the leech are snapshotted at cast.</summary>
    [SpellRegistration("canrpgclasses:consuming_plague")]
    public class ConsumingPlagueSpell : Spell
    {
        public ConsumingPlagueSpell()
        {
            var b = Balance;
            DisplayName = "Consuming Plague";
            IconName = "carrion";
            School = SpellSchool.Shadow;
            Tier = 3;
            Range = 24;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            // The spender: the DoT snapshots shadow spell power with the orbs still counted, then consumes them -
            // locking their boost into the plague's whole duration. Cast at max orbs for the big hit.
            ConsumesCasterEffectId = PriestEffectIds.ShadowOrbs;

            int duration = (int)b.F("duration", 9f);
            DescArgs = new object[] { duration, (int)System.Math.Round(b.F("selfHealFraction", 0.5f) * 100f) };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = PriestEffectIds.ConsumingPlague,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.ForSchool(SpellSchool.Shadow)
            });

            ConfigureCost(defResource: 40f, defCooldown: 15f); // mana
        }
    }
}
