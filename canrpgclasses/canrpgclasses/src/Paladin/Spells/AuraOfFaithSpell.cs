using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    [SpellRegistration("canrpgclasses:aura_of_faith")]
    public class AuraOfFaithSpell : Spell
    {
        public AuraOfFaithSpell()
        {
            var b = Balance;
            DisplayName = "Aura of Faith";
            IconName = "aura";
            School = SpellSchool.Holy;
            Tier = 2;
            Range = b.F("radius", 10f);

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.ToggleAura,
                StatusEffectId = AuraEffectIds.Regen, // hp per tick from config, applied infinite by EBAuras
                AuraUpkeepPerSecond = b.F("upkeep", 0f)
            });

            ConfigureCost(defResource: 0f, defCooldown: 0f);
        }
    }
}
