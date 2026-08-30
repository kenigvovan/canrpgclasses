using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    [SpellRegistration("canrpgclasses:suppress_pain")]
    public class SuppressPainSpell : Spell
    {
        public SuppressPainSpell()
        {
            var b = Balance;
            DisplayName = "Suppress Pain";
            IconName = "shield-opposition";
            School = SpellSchool.Holy;
            Tier = 3;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct;

            int duration = (int)b.F("duration", 8f);
            DescArgs = new object[] { (int)System.Math.Round(b.F("damageReduction", 0.40f) * 100f), duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = PriestEffectIds.SuppressPain,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 30f, defCooldown: 60f); // mana
        }
    }
}
