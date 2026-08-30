using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Resources;

namespace canrpgclasses.Core.Content
{
    /// <summary>A playable class defined in JSON. <see cref="RpgClassDef"/> is already pure data, so this is a
    /// straight copy; gear affinities are the one part left to C# add-ons, since matching gear needs more than a
    /// list of numbers.</summary>
    public class ContentClassDef : RpgClassDef
    {
        public ContentClassDef(string id, ClassModel m, ContentReport report)
        {
            Id = id;
            if (!string.IsNullOrEmpty(m.name)) DisplayName = m.name!;
            if (!string.IsNullOrEmpty(m.role)) Role = m.role!;
            if (!string.IsNullOrEmpty(m.description)) Description = m.description!;
            IconName = string.IsNullOrEmpty(m.icon) ? id : m.icon!;

            TreeNames = m.trees is { Count: > 0 }
                ? m.trees.ToArray()
                : new[] { "Tree 1", "Tree 2", "Tree 3" };

            if (m.resource != null)
            {
                if (string.IsNullOrEmpty(m.resource.id))
                {
                    report.Warn(id, "resource has no 'id' - the class will have no resource pool");
                }
                else
                {
                    var pool = new ResourcePoolDef
                    {
                        Id = m.resource.id!,
                        Max = m.resource.max,
                        RegenPerSec = m.resource.regenPerSec,
                        StartFull = m.resource.startFull
                    };
                    if (m.resource.color is { Length: >= 3 })
                    {
                        pool.ColorR = m.resource.color[0];
                        pool.ColorG = m.resource.color[1];
                        pool.ColorB = m.resource.color[2];
                    }
                    PrimaryResource = pool;
                }
            }

            UsesComboPoints = m.usesComboPoints;
            HpPerLevel = m.hpPerLevel;
            if (m.baseSpells != null) BaseSpells = m.baseSpells.ToArray();

            if (m.baseStats != null)
                foreach (var s in m.baseStats)
                {
                    if (string.IsNullOrEmpty(s.stat)) { report.Warn(id, "a baseStats entry has no 'stat' name"); continue; }
                    BaseStats.Add((s.stat!, s.value));
                }

            if (m.treeMasteries != null)
                foreach (var t in m.treeMasteries)
                {
                    if (string.IsNullOrEmpty(t.stat)) { report.Warn(id, "a treeMasteries entry has no 'stat' name"); continue; }
                    if (t.tree < 0 || t.tree >= TreeCount)
                    {
                        report.Warn(id, $"treeMasteries points at tree {t.tree}, but this class has {TreeCount}");
                        continue;
                    }
                    TreeMasteries.Add(new TreeMastery(t.tree, t.stat!, t.perPoint));
                }
        }
    }
}
