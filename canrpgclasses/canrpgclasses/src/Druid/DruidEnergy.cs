using Vintagestory.API.Common;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Druid
{
    /// <summary>
    /// The druid's Cat-form Energy economy (mirrors <see cref="DruidRage"/>). Unlike Rage it regenerates
    /// passively; EBResources only regenerates the primary pool, so the regen is driven here by a fast tick.
    /// </summary>
    public static class DruidEnergy
    {
        public const string ClassId = "druid";
        public const string PoolId = "energy";

        private const float RegenTickSeconds = 0.2f; // fine-grained so the bar refills smoothly, not in 1s steps

        /// <summary>Starts full so a shift into Cat can open immediately.</summary>
        public static readonly ResourcePoolDef EnergyPool = new ResourcePoolDef
        {
            Id = PoolId, Max = 100f, RegenPerSec = 0f, StartFull = true,
            ColorR = 0.95f, ColorG = 0.82f, ColorB = 0.20f // energy yellow
        };

        private static bool registered;
        private static long regenTickId;

        public static void Init()
        {
            if (registered) return;
            registered = true;
            ResourceState.RegisterSecondaryPool(EnergyPool);
            regenTickId = canrpgclassesModSystem.ServerApi?.Event.RegisterGameTickListener(RegenTick, (int)(RegenTickSeconds * 1000f)) ?? 0;
        }

        public static void Stop()
        {
            var api = canrpgclassesModSystem.ServerApi;
            if (api != null && regenTickId != 0) api.Event.UnregisterGameTickListener(regenTickId);
            regenTickId = 0;
            registered = false;
        }

        // Passive regen for every druid (harmless out of Cat - only Cat abilities spend it; shifting into Cat then
        // finds the pool topped up, the intended feral opener). Capped at the pool max (talent bonuses included).
        private static void RegenTick(float dt)
        {
            var api = canrpgclassesModSystem.ServerApi;
            if (api?.World == null) return;
            float perSec = BalanceConfig.Global("druidEnergyRegenPerSec", 10f) * canrpgclasses.Core.Resources.EBResources.RegenMultiplier;
            float add = perSec * dt;
            if (add <= 0f) return;
            foreach (var pl in api.World.AllOnlinePlayers)
            {
                var e = pl?.Entity;
                if (e == null || TalentState.CurrentClass(e) != ClassId) continue;
                float max = ResourceState.EffectiveMax(e, EnergyPool);
                float cur = ResourceState.Get(e, EnergyPool);
                if (cur >= max) continue;
                ResourceState.Set(e, EnergyPool, cur + add, max);
            }
        }
    }
}
