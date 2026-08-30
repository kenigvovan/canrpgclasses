using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    [SpellRegistration("canrpgclasses:quickblades")]
    public class QuickbladesSpell : Spell
    {
        public QuickbladesSpell()
        {
            var b = Balance;
            IconName = "sacrificial-dagger";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;
            Range = 0;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            // The trigger that spends a stack, handled by EBSpellCaster.DidAttack. It rides a dedicated effect id
            // rather than the shared strengthmelee, so the per-hit consume can't drain other strength buffs.
            Deliver.StashEffectId = QuickbladesEffectIds.Quickblades;
            Deliver.StashTriggers.Add(new SpellTrigger { Type = TriggerType.MeleeImpact, TargetOverride = TargetSelector.Caster });

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = QuickbladesEffectIds.Quickblades,
                StatusEffectDuration = b.F("duration", 10f),
                StatusEffectAmplifier = b.I("amp", 1),
                StatusEffectAmplifierCap = b.I("cap", 9),
                StatusEffectApplyMode = StatusApplyMode.Add
            });

            Cost.Exhaust = b.F("exhaust", 0.2f);
            ConfigureCost(defResource: 25f, defCooldown: 15f); // energy
        }
    }
}
