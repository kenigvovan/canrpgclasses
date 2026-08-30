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
    }
}
