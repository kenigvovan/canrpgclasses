using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    [SpellRegistration("canrpgclasses:bloodletting")]
    public class BloodlettingSpell : Spell
    {
        public BloodlettingSpell()
        {
            var b = Balance;
            DisplayName = "Bloodletting";
            IconName = "mouth-watering";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "vampirism",
                StatusEffectDuration = b.F("duration", 10f),
                StatusEffectAmplifier = b.I("amp", 2),
                StatusEffectAmplifierCap = b.I("cap", 3)
            });

            ConfigureCost(defResource: 35f, defCooldown: 25f); // energy
        }
    }
}
