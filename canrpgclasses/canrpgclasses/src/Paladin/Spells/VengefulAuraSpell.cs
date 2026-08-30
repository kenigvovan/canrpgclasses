using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    [SpellRegistration("canrpgclasses:vengeful_aura")]
    public class VengefulAuraSpell : Spell
    {
        public VengefulAuraSpell()
        {
            var b = Balance;
            DisplayName = "Vengeful Aura";
            IconName = "enrage";
            School = SpellSchool.Holy;
            Tier = 2;
            Range = b.F("radius", 10f);

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.ToggleAura,
                StatusEffectId = AuraEffectIds.Might, // value from config, applied infinite by EBAuras
                AuraUpkeepPerSecond = b.F("upkeep", 0f)
            });

            ConfigureCost(defResource: 0f, defCooldown: 0f);
        }
    }
}
