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

    /// <summary>A playable class. Talents reference it by <c>ClassId</c> and a tree by index.</summary>
    public abstract class RpgClassDef
    {
        /// <summary>Default tree count, not a limit: callers read the per-class <see cref="TreeCount"/>.</summary>
        public const int DefaultTreeCount = 3;

        // Protected so a JSON-defined subclass can set its id without the attribute.
        public string Id { get; protected set; } = "";
        public string[] TreeNames { get; protected set; } = new string[DefaultTreeCount];

        /// <summary>This class's number of talent trees, i.e. the valid range of a talent's
        /// <see cref="canrpgclasses.Core.Talents.Talent.TreeIndex"/>.</summary>
        public int TreeCount => TreeNames.Length;

        /// <summary>Icon stem under <c>canrpgclasses:textures/icons/</c> (.svg/.png) for the class badge. Defaults to the id.</summary>
        public string IconName { get; protected set; } = "";

        /// <summary>Primary resource that <c>Cost.Resource</c> spends. Null = cooldown-only class.</summary>
        public ResourcePoolDef? PrimaryResource { get; protected set; }

        public bool UsesComboPoints { get; protected set; }

        /// <summary>Spells available from level 1; the rest come from talents.</summary>
        public string[] BaseSpells { get; protected set; } = Array.Empty<string>();

        /// <summary>Class-wide gear passives, evaluated by <see cref="canrpgclasses.Core.EB.EBGearAffinity"/>.</summary>
        public System.Collections.Generic.List<GearAffinity> GearAffinities { get; protected set; } = new();

        /// <summary>Each point in a tree grants <see cref="TreeMastery.PerPoint"/> of a stat.</summary>
        public System.Collections.Generic.List<TreeMastery> TreeMasteries { get; protected set; } = new();

        /// <summary>Flat stats granted just for being this class.</summary>
        public System.Collections.Generic.List<(string Stat, float Value)> BaseStats { get; protected set; } = new();

        /// <summary>Extra <c>maxhealthExtraPoints</c> per level above 1.</summary>
        public float HpPerLevel { get; protected set; }

        /// <summary>Any stat per level above 1, e.g. attribute points.</summary>
        public System.Collections.Generic.List<(string Stat, float PerLevel)> StatsPerLevel { get; protected set; } = new();

        /// <summary>Per-attribute worth for this class; overrides the class entry in <c>attributes.json</c>.</summary>
        public System.Collections.Generic.List<(string Attribute, float Multiplier)> AttributeAffinity { get; protected set; } = new();

        private string? _displayName;
        /// <summary>Localized class name: lang key <c>canrpgclasses:class-&lt;id&gt;</c> wins; else code default; else humanized id.</summary>
        public string DisplayName
        {
            get => LangText.Get("class-" + Id, _displayName) ?? LangText.Humanize(Id);
            protected set => _displayName = value;
        }

        private string? _role;
        /// <summary>Short role tag for the class picker, lang key <c>canrpgclasses:class-&lt;id&gt;-role</c>.</summary>
        public string Role
        {
            get => LangText.Get("class-" + Id + "-role", _role) ?? "";
            protected set => _role = value;
        }

        private string? _description;
        /// <summary>Class-picker description, lang key <c>canrpgclasses:class-&lt;id&gt;-desc</c>.</summary>
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
