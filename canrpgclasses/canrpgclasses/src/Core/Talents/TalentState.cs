using System.Collections.Generic;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.Core.Talents
{
    /// <summary>Read-only talent state and spend rules; server and client UI share <see cref="CanSpend"/>.</summary>
    public static class TalentState
    {
        public const string RanksKey = Core.AttrKeys.TalentRanks;
        public const string ClassKey = Core.AttrKeys.CurrentClass;
        public const string DefaultClass = "rogue";
        /// <summary>Points per tier gate. At 4, the tier-4 capstone costs 17 of the max 24 points, leaving room
        /// to splash a second tree.</summary>
        public const int PointsPerTier = 4;

        public static string CurrentClass(Entity e) =>
            e?.WatchedAttributes?.GetString(ClassKey, DefaultClass) ?? DefaultClass;

        public static int Rank(Entity e, string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            var tree = e?.WatchedAttributes?.GetTreeAttribute(RanksKey);
            return tree?.GetInt(id, 0) ?? 0;
        }

        public static int SpentPoints(Entity e)
        {
            var tree = e?.WatchedAttributes?.GetTreeAttribute(RanksKey);
            if (tree == null) return 0;
            int sum = 0;
            foreach (var kv in tree) sum += tree.GetInt(kv.Key, 0);
            return sum;
        }

        public static int AvailablePoints(Entity e, int totalPoints) => totalPoints - SpentPoints(e);

        /// <summary>Active spell ids granted by the player's current-class talents that have at least one rank.</summary>
        public static IEnumerable<string> GrantedSpells(Entity e, TalentRegistry registry)
        {
            string cls = CurrentClass(e);
            foreach (var t in registry.ForClass(cls))
            {
                if (!string.IsNullOrEmpty(t.GrantsSpellId) && Rank(e, t.Id) > 0)
                    yield return t.GrantsSpellId!;
            }
        }

        public static int PointsInTree(Entity e, TalentRegistry registry, int treeIndex)
        {
            string cls = CurrentClass(e);
            int sum = 0;
            foreach (var t in registry.ForClassTree(cls, treeIndex)) sum += Rank(e, t.Id);
            return sum;
        }

        /// <summary>Whether one more point can be put into the talent right now. Out reason for UI tooltips.</summary>
        public static bool CanSpend(Entity e, Talent talent, TalentRegistry registry, int totalPoints, out string reason)
        {
            reason = "";
            if (talent == null) { reason = "unknown"; return false; }
            if (talent.ClassId != CurrentClass(e)) { reason = "wrong-class"; return false; }
            if (Rank(e, talent.Id) >= talent.MaxRank) { reason = "maxed"; return false; }
            if (AvailablePoints(e, totalPoints) <= 0) { reason = "no-points"; return false; }
            if (PointsInTree(e, registry, talent.TreeIndex) < talent.Tier * PointsPerTier) { reason = "tier-locked"; return false; }
            if (!string.IsNullOrEmpty(talent.RequiresTalent))
            {
                var req = registry.Get(talent.RequiresTalent);
                if (req != null && Rank(e, req.Id) < req.MaxRank) { reason = "prereq"; return false; }
            }
            if (!Core.Attributes.AttributeStats.Meets(e, talent.RequiresAttributes, out string missing))
            {
                reason = "attribute:" + missing;
                return false;
            }
            return true;
        }
    }
}
