using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using canrpgclasses.Core;

namespace canrpgclasses.Core.Talents
{
    /// <summary>One talent in a class tree. Stat talents override <see cref="ApplyStats"/>; hook talents are read
    /// by the combat hooks themselves.</summary>
    public abstract class Talent
    {
        // Protected so a JSON-defined subclass can set its id without the attribute.
        public string Id { get; protected set; } = "";
        public string ClassId { get; protected set; } = "rogue";
        public int TreeIndex { get; protected set; }          // 0..RpgClassDef.TreeCount-1 within the class
        public int Tier { get; protected set; }               // row, 0-based (gated by points spent in the tree)
        public int Column { get; protected set; }             // column within the tier
        public int MaxRank { get; protected set; } = 1;
        public string? RequiresTalent { get; protected set; } // prerequisite talent id (must be maxed)

        /// <summary>Attribute points needed to spend here, checked by <see cref="TalentState.CanSpend"/>.</summary>
        public IReadOnlyDictionary<string, float>? RequiresAttributes { get; protected set; }

        /// <summary>If set, taking this talent (rank ≥ 1) grants the player this active spell in their hotbar.</summary>
        public string? GrantsSpellId { get; protected set; }

        /// <summary>Icon stem under <c>canrpgclasses:textures/icons/</c> (.svg/.png). If null, the UI falls back
        /// to the granted spell's icon (when <see cref="GrantsSpellId"/> is set), else a text abbreviation.</summary>
        public string? IconName { get; protected set; }

        private string? _displayName;
        private string? _description;

        /// <summary>Localized name: lang key <c>canrpgclasses:talent-&lt;id&gt;</c> wins; else the code default; else humanized id.</summary>
        public string DisplayName
        {
            get => LangText.Get("talent-" + LangKey, _displayName) ?? LangText.Humanize(Id);
            protected set => _displayName = value;
        }

        /// <summary>Values for the description template's placeholders, taken from the same config numbers the
        /// talent applies, so the tooltip can't drift from the effect.</summary>
        protected object[]? DescArgs;

        /// <summary>Localized description: lang key <c>canrpgclasses:talentdesc-&lt;id&gt;</c> wins; else the code default.
        /// When <see cref="DescArgs"/> is set, the resolved text is treated as a <c>string.Format</c> template.</summary>
        public string Description
        {
            get
            {
                var t = LangText.Get("talentdesc-" + LangKey, _description);
                if (string.IsNullOrEmpty(t)) return "";
                return DescArgs != null && DescArgs.Length > 0 ? string.Format(t!, DescArgs) : t!;
            }
            protected set => _description = value;
        }

        private string LangKey => Id.Replace(':', '-');

        protected Talent()
        {
            var attr = GetType().GetCustomAttribute<TalentRegistrationAttribute>();
            if (attr != null) Id = attr.TalentId;
        }

        /// <summary>Stat talents override this; called with the current rank (0 clears the modifier).</summary>
        public virtual void ApplyStats(EntityAgent entity, int rank) { }

        /// <summary>Raises a pool's max; read live by <see cref="canrpgclasses.Core.Resources.ResourceState.EffectiveMax"/>.</summary>
        public virtual float MaxResourceBonus(string poolId, int rank) => 0f;

        /// <summary>Runs on every hit before mitigation, so it must bail fast at rank 0.</summary>
        public virtual DamageModifier? DamageModifier => null;

        /// <summary>Runs on every landed swing, so it must bail fast at rank 0.</summary>
        public virtual MeleeHitHook? OnMeleeHit => null;
    }
}
