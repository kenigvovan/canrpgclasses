using System;
using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using Vintagestory.API.Common;

namespace canrpgclasses.Core.Attributes
{
    /// <summary>Turns attribute points into the stats they pay for. Runs at the end of the stat pass in
    /// <see cref="canrpgclasses.Core.EB.EBTalents"/>, so class grant, talents, gear and the admin bonus read as one
    /// total. Every payout is written on every pass (0 where a class has none), so nothing lingers.</summary>
    public static class AttributeStats
    {
        public static void Apply(EntityAgent agent, string classId, int level)
        {
            foreach (var (stat, source) in RpgAttributes.StalePairs) agent.RemoveStatIfPresent(stat, source);

            var defs = RpgAttributes.All;
            if (defs.Count == 0) return;

            // Class grant first: the payouts below read the attribute's full total, talents included.
            int levels = Math.Max(0, level - 1);
            var grant = RpgAttributes.ForClass(classId);
            foreach (var def in defs)
                agent.SetStatIfChanged(def.Id, RpgAttributes.ClassSource, grant.Granted(def.Id, levels));

            foreach (var def in defs)
            {
                float points = def.EffectivePoints(agent);
                string source = RpgAttributes.PayoutSource(def.Id);
                foreach (var eff in def.EffectsFor(classId))
                    agent.SetStatIfChanged(eff.Stat, source, eff.Value(points));
            }
        }

        /// <summary>Fingerprint of every attribute total, so the "stats" listener can tell whether a stat change
        /// moved an attribute. A hash, not a sum: +2 strength and -2 dexterity must not cancel out.</summary>
        public static int Signature(Entity e)
        {
            int h = 17;
            foreach (var def in RpgAttributes.All) h = h * 31 + def.Points(e).GetHashCode();
            return h;
        }

        /// <summary>Attribute gate for talents and spells. <paramref name="missing"/> names the first unmet
        /// requirement, already localized, for the message the player sees.</summary>
        public static bool Meets(Entity e, IReadOnlyDictionary<string, float>? required, out string missing)
        {
            missing = "";
            if (required == null || required.Count == 0) return true;

            foreach (var kv in required)
            {
                var def = RpgAttributes.Get(kv.Key);
                if (def == null) continue; // an unknown attribute gates nothing; the loader already warned
                if (def.Points(e) + 0.0005f < kv.Value)
                {
                    missing = def.DisplayName + " " + kv.Value.ToString("0.##");
                    return false;
                }
            }
            return true;
        }
    }
}
