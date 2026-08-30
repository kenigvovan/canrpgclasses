using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    [SpellRegistration("canrpgclasses:zealots_aura")]
    public class ZealotsAuraSpell : Spell
    {
        public ZealotsAuraSpell()
        {
            var b = Balance;
            DisplayName = "Zealot's Aura";
            IconName = "cloaked-figure-on-horseback";
            School = SpellSchool.Holy;
            Tier = 2;
            Range = b.F("radius", 10f);

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.ToggleAura,
                StatusEffectId = AuraEffectIds.Haste, // value from config, applied infinite by EBAuras
                AuraUpkeepPerSecond = b.F("upkeep", 0f)
            });

            ConfigureCost(defResource: 0f, defCooldown: 0f);
        }
    }
}
