using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    [SpellRegistration("canrpgclasses:counterstrike")]
    public class CounterstrikeSpell : Spell
    {
        public CounterstrikeSpell()
        {
            var b = Balance;
            DisplayName = "Counterstrike";
            IconName = "body-balance";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "thorns",
                StatusEffectDuration = b.F("duration", 8f),
                StatusEffectAmplifier = b.I("amp", 2),
                StatusEffectAmplifierCap = b.I("cap", 3)
            });

            ConfigureCost(defResource: 25f, defCooldown: 22f); // energy
        }
    }
}
