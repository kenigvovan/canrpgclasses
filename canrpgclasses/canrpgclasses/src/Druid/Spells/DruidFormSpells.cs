using canrpgclasses.Core.Spells;

namespace canrpgclasses.Druid.Spells
{
    /// <summary>Base for a druid shapeshift toggle: a free, self-only <see cref="ImpactAction.ToggleAura"/> that
    /// enters/leaves a form (mutually exclusive via the single ActiveAura slot, like the warrior's Stances). Bypasses
    /// the form-lock so you can always shift back out; a short shared cooldown stops form-flickering.</summary>
    public abstract class DruidFormSpell : Spell
    {
        protected DruidFormSpell(string effectId, string display, string icon, bool locksActions = true)
        {
            DisplayName = display;
            IconName = icon;
            School = SpellSchool.Nature;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;
            BypassesFormLock = true; // must be able to toggle the form itself off
            AuraLocksActions = locksActions; // relog-safe negative gate derives the lock from ActiveAura + this flag

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.ToggleAura,
                StatusEffectId = effectId,
                AuraSelfOnly = true,        // a personal form, never a party aura
                AuraUpkeepPerSecond = 0f
            });

            Cost.Cooldown.Group = "canrpgclasses:druid_form"; // one shared cooldown across all forms
            ConfigureCost(defResource: 15f, defCooldown: 1.5f); // shifting costs mana (mana regens in every form, so exiting is never trapped)
        }
    }

    [SpellRegistration("canrpgclasses:feline_form")]
    public class FelineFormSpell : DruidFormSpell
    {
        public FelineFormSpell() : base(DruidEffectIds.FelineForm, "Feline Form", "hyena-head") { }
    }

    [SpellRegistration("canrpgclasses:ursine_form")]
    public class UrsineFormSpell : DruidFormSpell
    {
        public UrsineFormSpell() : base(DruidEffectIds.UrsineForm, "Ursine Form", "bear-head") { }
    }
}
