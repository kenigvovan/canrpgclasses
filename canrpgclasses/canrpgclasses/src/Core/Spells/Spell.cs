using System.Collections.Generic;
using System.Reflection;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Core.Spells
{
    /// <summary>
    /// Base class for every spell. Concrete spells subclass this, mark themselves with
    /// <see cref="SpellRegistrationAttribute"/> and configure the data fields in their constructor.
    /// </summary>
    public abstract class Spell
    {
        /// <summary>Stable id, e.g. <c>canrpgclasses:quickblades</c>. Filled from the registration attribute.</summary>
        public string Id { get; protected set; } = "";

        private string? _displayName;
        private string? _iconName;

        /// <summary>Localized name (HUD/tooltip): lang key <c>canrpgclasses:spell-&lt;local&gt;</c> wins; else code default; else humanized.</summary>
        public string DisplayName
        {
            get => canrpgclasses.Core.LangText.Get("spell-" + LocalId, _displayName) ?? canrpgclasses.Core.LangText.Humanize(LocalId);
            protected set => _displayName = value;
        }

        /// <summary>Icon file stem under <c>canrpgclasses:textures/icons/</c> (.svg or .png). Null → letter placeholder.</summary>
        public string? IconName
        {
            get => _iconName;
            protected set => _iconName = value;
        }

        private string? _description;

        /// <summary>Values for the description template's placeholders. Taken from the same config numbers the
        /// spell itself uses, so the tooltip can't drift from the effect. Empty = show the text verbatim.</summary>
        protected object[]? DescArgs;

        /// <summary>Localized tooltip description: lang key <c>canrpgclasses:spelldesc-&lt;local&gt;</c> wins; else code default.
        /// When <see cref="DescArgs"/> is set, the resolved text is treated as a <c>string.Format</c> template.</summary>
        public string Description
        {
            get
            {
                var t = canrpgclasses.Core.LangText.Get("spelldesc-" + LocalId, _description);
                if (string.IsNullOrEmpty(t)) return "";
                return DescArgs != null && DescArgs.Length > 0 ? string.Format(t!, DescArgs) : t!;
            }
            protected set => _description = value;
        }

        public string Domain
        {
            get { int i = Id.IndexOf(':'); return i > 0 ? Id.Substring(0, i) : "canrpgclasses"; }
        }

        public string LocalId
        {
            get { int i = Id.IndexOf(':'); return i >= 0 ? Id.Substring(i + 1) : Id; }
        }

        public string CooldownKey => string.IsNullOrEmpty(Cost.Cooldown.Group) ? Id : Cost.Cooldown.Group!;

        /// <summary>One shared cooldown entry per caster, stored in <c>EBSpellCooldowns</c> like any other, so it
        /// syncs and renders on the hotbar.</summary>
        public const string GlobalCooldownKey = "canrpgclasses:gcd";

        /// <summary>False for abilities that must stay usable off-GCD: emergency defensives, form-exit toggles,
        /// anything its own cooldown already gates harder.</summary>
        public bool TriggersGlobalCooldown { get; protected set; } = true;

        public SpellSchool School { get; protected set; } = SpellSchool.PhysicalMelee;
        public SpellType Type { get; protected set; } = SpellType.Active;
        public int Tier { get; protected set; } = 1;
        public float Range { get; protected set; }

        /// <summary>Body animation played on the caster. Cosmetic - it neither gates nor times the impacts. The
        /// engine swaps in the matching <c>-fp</c> variant in first person.</summary>
        public string? AnimationCode { get; protected set; }

        public CastMode CastMode { get; protected set; } = CastMode.Instant;
        public float CastDuration { get; protected set; }

        /// <summary>Periodic-fire channel: fire the impacts this many times over <see cref="CastDuration"/> for one
        /// up-front cost. 0 = the plain zone channel - fire once and keep the effect alive for the duration.</summary>
        public int ChannelTicks { get; protected set; }

        /// <summary>Draw a continuous ray from caster to the channel's aimed target while it runs. State-synced,
        /// so it follows both bodies and dies with the channel.</summary>
        public bool BeamFx { get; protected set; }

        /// <summary>Castable under a travel-form action lock. Set on the spell that toggles the form OFF, so
        /// entering a form never traps you in it.</summary>
        public bool BypassesFormLock { get; protected set; }

        public SpellTarget Target { get; protected set; } = new SpellTarget();
        public SpellDelivery Deliver { get; protected set; } = new SpellDelivery();
        public List<SpellImpact> Impacts { get; protected set; } = new List<SpellImpact>();
        public SpellCost Cost { get; protected set; } = new SpellCost();

        /// <summary>A builder adds combo points on a hit; a finisher spends all of them and scales with the count.
        /// Never both.</summary>
        public bool ComboBuilder { get; protected set; }
        public bool ComboFinisher { get; protected set; }
        public int ComboPointsGenerated { get; protected set; } = 1;

        /// <summary>Caster stat that multiplies this spell's damage - how a talent sharpens one specific spell
        /// without the executor ever knowing spell ids. Combo finishers default to <c>finisherDamage</c>.</summary>
        public string? DamageMultiplierStat { get; protected set; }

        /// <summary>Caster stat that multiplies this spell's resource cost, read when the cast is committed.
        /// Unset = the flat <see cref="SpellCost.Resource"/>.</summary>
        public string? ResourceCostMultiplierStat { get; protected set; }

        /// <summary>Effect stripped from the caster once the cast finishes. Consumed after every projectile has
        /// snapshotted the caster's power, so the whole burst still benefits from the stacks.</summary>
        public string? ConsumesCasterEffectId { get; protected set; }

        /// <summary>Gate: castable only out of combat. Stealth is the approach tool, Slip Away the escape.</summary>
        public bool RequiresOutOfCombat { get; protected set; }

        /// <summary>Gate: needs a bow in the active hand - the shot abilities empower the next real bow shot.</summary>
        public bool RequiresBow { get; protected set; }

        /// <summary>Gate: needs a shield (a VS item carrying a "shield" attribute block) in either hand.</summary>
        public bool RequiresShield { get; protected set; }

        /// <summary>Gate: the target must already carry one of these effects. Checked against the entity the spell
        /// would resolve, so a failed check costs nothing.</summary>
        public string[]? RequiresTargetEffectIds { get; protected set; }

        /// <summary>Gate: needs the named toggle-form aura active - a cat ability requires cat form. Holds the
        /// form-toggle spell's id.</summary>
        public string? RequiresForm { get; protected set; }

        /// <summary>Set on a form-toggle whose form blocks normal casting. The form's effect asserts the lock every
        /// tick, but only the active-aura string survives a relog reliably - so the gate reads the aura too.</summary>
        public bool AuraLocksActions { get; protected set; }

        /// <summary>Cost in a pool that isn't the class primary - Bear abilities spend Rage. Resolved by id from
        /// <c>ResourceState.SecondaryPool</c>. Null = none.</summary>
        public string? SecondaryResourceId { get; protected set; }
        public float SecondaryResourceCost { get; protected set; }

        protected void ConfigureSecondaryCost(string poolId, float amount)
        {
            SecondaryResourceId = poolId;
            SecondaryResourceCost = amount;
        }

        /// <summary>Seconds of stealth window a successful cast opens. The cooldown is meant to be shorter than the
        /// invisibility, so re-stealthing out of combat is seamless; attacking inside it restores the full
        /// lockout. 0 = no window.</summary>
        public float OpensStealthWindowSeconds { get; protected set; }

        private BalanceConfig.Entry? _balance;

        /// <summary>This spell's <see cref="BalanceConfig"/> numbers. Cached per instance - the registries build
        /// fresh instances after every config change.</summary>
        protected BalanceConfig.Entry Balance => _balance ??= BalanceConfig.Spell(LocalId);

        protected void ConfigureCost(float defResource, float defCooldown)
        {
            Cost.Resource = Balance.F("resource", defResource);
            Cost.Cooldown.Duration = Balance.F("cooldown", defCooldown);
        }

        protected Spell()
        {
            var attr = GetType().GetCustomAttribute<SpellRegistrationAttribute>();
            if (attr != null)
            {
                Id = attr.SpellId;
            }
        }
    }
}
