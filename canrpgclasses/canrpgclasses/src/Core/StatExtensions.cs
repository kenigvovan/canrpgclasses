using Vintagestory.API.Common.Entities;

namespace canrpgclasses.Core
{
    public static class StatExtensions
    {
        /// <summary>Reads a 1.0-based stat as a 0-based fraction. GetBlended returns 1.0 for an unset stat, so the
        /// implicit 1.0 comes off - otherwise an entity with no such modifier would read as a permanent full cut.
        /// Floored at 0; each caller clamps to its own cap.</summary>
        public static float ReductionStat(this Entity e, string stat)
            => System.Math.Max(0f, e.Stats.GetBlended(stat) - 1f);

        /// <summary>What one source contributes to a stat, or 0 if the stat or the source isn't set. EntityStats
        /// has no lookup for this, hence the scan.</summary>
        public static float StatSource(this Entity e, string stat, string source)
            => TryStatSource(e, stat, source, out float v) ? v : 0f;

        /// <summary>Whether a source is present on a stat at all, telling "set to 0" apart from "not set".</summary>
        public static bool TryStatSource(this Entity e, string stat, string source, out float value)
        {
            foreach (var kv in e.Stats)
                if (kv.Key == stat)
                {
                    if (kv.Value.ValuesByKey.TryGetValue(source, out var s)) { value = s.Value; return true; }
                    break;
                }
            value = 0f;
            return false;
        }

        /// <summary>Sets a stat source only when the value changes: every <c>EntityStats.Set</c> re-serializes the
        /// whole stat tree and wakes its listeners, so a pass writing dozens of stats is worth this scan.</summary>
        public static void SetStatIfChanged(this Entity e, string stat, string source, float value)
        {
            if (TryStatSource(e, stat, source, out float cur) && cur == value) return;
            e.Stats.Set(stat, source, value);
        }

        /// <summary>Removes a stat source only if it is there, for the same reason as <see cref="SetStatIfChanged"/>.</summary>
        public static void RemoveStatIfPresent(this Entity e, string stat, string source)
        {
            if (TryStatSource(e, stat, source, out _)) e.Stats.Remove(stat, source);
        }
    }
}
