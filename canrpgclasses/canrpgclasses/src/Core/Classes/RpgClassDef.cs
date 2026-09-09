using System;
using System.Reflection;
using canrpgclasses.Core.Resources;

namespace canrpgclasses.Core.Classes
{
    /// <summary>Marks an <see cref="RpgClassDef"/> subclass for discovery by the class registry.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class RpgClassRegistrationAttribute : Attribute
    {
        public string ClassId { get; }
        public RpgClassRegistrationAttribute(string classId) { ClassId = classId; }
    }

    /// <summary>
    /// A playable class: id, display name and its talent-tree names. Talents reference a class by <c>ClassId</c>
    /// and a tree by index.
    /// </summary>
    public abstract class RpgClassDef
    {
        /// <summary>Trees a class gets when it doesn't say otherwise - the three specs every shipped class uses.
        /// Not a limit: a class may declare any number of <see cref="TreeNames"/>, and everything that walks the
        /// trees reads the per-class <see cref="TreeCount"/>.</summary>
        public const int DefaultTreeCount = 3;

        // protected, so a data-driven subclass (see Core.Content) can set its id from JSON instead of an attribute.
        public string Id { get; protected set; } = "";
        public string[] TreeNames { get; protected set; } = new string[DefaultTreeCount];

        /// <summary>This class's number of talent trees, i.e. the valid range of a talent's
        /// <see cref="canrpgclasses.Core.Talents.Talent.TreeIndex"/>.</summary>
        public int TreeCount => TreeNames.Length;

        /// <summary>Icon stem under <c>canrpgclasses:textures/icons/</c> (.svg/.png) for the class badge. Defaults to the id.</summary>
        public string IconName { get; protected set; } = "";

        /// <summary>This class's primary resource pool (energy/rage/mana). A spell's <c>Cost.Resource</c> is
        /// spent from it. Null = the class has no resource (cooldown-only).</summary>
        public ResourcePoolDef? PrimaryResource { get; protected set; }

        /// <summary>Whether this class uses combo points (builder/finisher mechanic). Rogue: true.</summary>
        public bool UsesComboPoints { get; protected set; }

        /// <summary>Spells available from level 1. Everything else is unlocked by talents.</summary>
        public string[] BaseSpells { get; protected set; } = Array.Empty<string>();

        /// <summary>Intrinsic, class-wide gear buffs/debuffs applied while matching equipment is worn/wielded
        /// (no talent needed). Evaluated by <see cref="canrpgclasses.Core.EB.EBGearAffinity"/>.</summary>
        public System.Collections.Generic.List<GearAffinity> GearAffinities { get; protected set; } = new();

        /// <summary>Spec scaling: each point in a tree grants <see cref="TreeMastery.PerPoint"/> of a stat. A tree
        /// can scale several stats and a stat can be fed by several trees, so specced players are stronger in
        /// their tree's role. Numbers come from config.</summary>
        public System.Collections.Generic.List<TreeMastery> TreeMasteries { get; protected set; } = new();

        /// <summary>Stat bonuses granted just for being this class, independent of talents and gear - what lets a
        /// paladin start tougher than a mage. Numbers come from config.</summary>
        public System.Collections.Generic.List<(string Stat, float Value)> BaseStats { get; protected set; } = new();

        /// <summary>Extra <c>maxhealthExtraPoints</c> per level above 1, re-applied on every level-up.</summary>
        public float HpPerLevel { get; protected set; }

        /// <summary>Any stat granted per level above 1 - the general form of <see cref="HpPerLevel"/>. This is how
        /// a class hands out attribute points as it levels; re-applied on every level-up.</summary>
        public System.Collections.Generic.List<(string Stat, float PerLevel)> StatsPerLevel { get; protected set; } = new();

        /// <summary>Multiplier on what one point of an attribute pays this class ("intelligence is worth 0.2× to a
        /// warrior"). Overrides the class entry in <c>attributes.json</c> for the attributes it names.</summary>
        public System.Collections.Generic.List<(string Attribute, float Multiplier)> AttributeAffinity { get; protected set; } = new();

        private string? _displayName;
        /// <summary>Localized class name: lang key <c>canrpgclasses:class-&lt;id&gt;</c> wins; else code default; else humanized id.</summary>
        public string DisplayName
        {
            get => LangText.Get("class-" + Id, _displayName) ?? LangText.Humanize(Id);
            protected set => _displayName = value;
        }

        private string? _role;
        /// <summary>Localized short role/playstyle tag shown in the class picker (e.g. "Ranged burst caster"): lang key
        /// <c>canrpgclasses:class-&lt;id&gt;-role</c>. Empty when unset.</summary>
        public string Role
        {
            get => LangText.Get("class-" + Id + "-role", _role) ?? "";
            protected set => _role = value;
        }

        private string? _description;
        /// <summary>Localized one-paragraph class description shown in the class-picker preview: lang key
        /// <c>canrpgclasses:class-&lt;id&gt;-desc</c>. Empty when unset.</summary>
        public string Description
        {
            get => LangText.Get("class-" + Id + "-desc", _description) ?? "";
            protected set => _description = value;
        }

        protected RpgClassDef()
        {
            var attr = GetType().GetCustomAttribute<RpgClassRegistrationAttribute>();
            if (attr != null) Id = attr.ClassId;
        }

        /// <summary>Localized tree name: lang key <c>canrpgclasses:class-&lt;id&gt;-tree&lt;index&gt;</c> wins; else code default.</summary>
        public string TreeName(int index)
        {
            string? fallback = (index >= 0 && index < TreeNames.Length) ? TreeNames[index] : null;
            return LangText.Get($"class-{Id}-tree{index}", fallback) ?? $"Tree {index + 1}";
        }
    }

    /// <summary>One "each point in tree X grants +PerPoint of Stat" rule. See <see cref="RpgClassDef.TreeMasteries"/>.</summary>
    public struct TreeMastery
    {
        public int TreeIndex;
        public string Stat;
        public float PerPoint;
        public TreeMastery(int treeIndex, string stat, float perPoint) { TreeIndex = treeIndex; Stat = stat; PerPoint = perPoint; }
    }
}
