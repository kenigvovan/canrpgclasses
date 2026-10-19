using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Talents;
using Vintagestory.API.Common;

namespace canrpgclasses.Core.Content
{
    /// <summary>A JSON talent: grants a spell and/or sets stats per rank. Hook talents still need C#.</summary>
    public class ContentTalent : Talent
    {
        private readonly (string Stat, float PerRank)[] stats;
        private readonly string statKey;

        /// <summary>Per-rank stats, exposed so the tree editor can read them back instead of blanking them on save.</summary>
        public IReadOnlyList<(string Stat, float PerRank)> Stats => stats;

        public ContentTalent(string id, TalentModel m, ContentReport report)
        {
            Id = id;
            ClassId = m.@class ?? "";
            TreeIndex = m.tree;
            Tier = m.tier;
            Column = m.column;
            MaxRank = m.maxRank > 0 ? m.maxRank : 1;
            RequiresTalent = m.requires;
            if (m.requiresAttributes is { Count: > 0 }) RequiresAttributes = m.requiresAttributes;
            GrantsSpellId = m.grantsSpell;
            IconName = m.icon;

            if (!string.IsNullOrEmpty(m.name)) DisplayName = m.name!;
            if (!string.IsNullOrEmpty(m.description)) Description = m.description!;

            if (m.descArgs is { Length: > 0 })
            {
                var args = new object[m.descArgs.Length];
                for (int i = 0; i < args.Length; i++) args[i] = m.descArgs[i];
                DescArgs = args;
            }

            var list = new List<(string, float)>();
            if (m.stats != null)
                foreach (var s in m.stats)
                {
                    if (string.IsNullOrEmpty(s.stat)) { report.Warn(id, "a stat entry has no 'stat' name"); continue; }
                    list.Add((s.stat!, s.perRank));
                }
            stats = list.ToArray();

            // One modifier key per talent, so ranks replace rather than stack, and a respec clears cleanly.
            statKey = "canrpgcontent_" + id.Replace(':', '_');
        }

        public override void ApplyStats(EntityAgent entity, int rank)
        {
            foreach (var (stat, perRank) in stats)
            {
                if (rank > 0) entity.Stats.Set(stat, statKey, perRank * rank);
                else entity.Stats.Remove(stat, statKey);
            }
        }
    }
}
