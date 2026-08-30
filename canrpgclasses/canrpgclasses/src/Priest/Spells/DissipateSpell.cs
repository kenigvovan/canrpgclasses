using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    [SpellRegistration("canrpgclasses:dissipate")]
    public class DissipateSpell : Spell
    {
        public DissipateSpell()
        {
            var b = Balance;
            DisplayName = "Dissipate";
            IconName = "swirl-string";
            School = SpellSchool.Shadow;
            Tier = 4;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            int duration = (int)b.F("duration", 6f);
            float manaGain = b.F("manaGain", 40f);
            DescArgs = new object[]
            {
                (int)System.Math.Round(b.F("damageReduction", 0.40f) * 100f),
                duration,
                (int)manaGain
            };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = PriestEffectIds.Dissipate,
                StatusEffectDuration = duration
            });
            // GainResource pours into the caster's primary pool, which for the priest is mana.
            Impacts.Add(new SpellImpact { Action = ImpactAction.GainResource, ResourceGainAmount = manaGain });

            ConfigureCost(defResource: 0f, defCooldown: 90f);
        }
    }
}
