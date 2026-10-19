using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Core.Resources
{
    /// <summary>Server ticker: regenerates the primary pool and decays idle combo points. Values live in <see cref="ResourceState"/>.</summary>
    public class EBResources : EntityBehavior
    {
        public const string Name = "canrpgresources";

        /// <summary>Seconds without a builder before combo points reset to 0.</summary>
        public static float ComboDecaySeconds => BalanceConfig.Global("comboDecaySeconds", 12f);

        /// <summary>Server-wide multiplier on all passive resource regen (0.5 = half speed).</summary>
        public static float RegenMultiplier => BalanceConfig.Global("resourceRegenMultiplier", 1f);

        private bool IsServer => entity.Api.Side == EnumAppSide.Server;

        // ~5 Hz: every pool write re-syncs to the client, so per-tick updates would just cost bandwidth.
        private const float TickInterval = 0.2f;
        private float accum;

        public EBResources(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        public override void OnGameTick(float dt)
        {
            if (!IsServer) return;

            accum += dt;
            if (accum < TickInterval) return;
            float elapsed = accum;
            accum = 0f;

            var cls = ResourceState.CurrentClass(entity);
            var pool = cls?.PrimaryResource;
            if (pool != null && pool.RegenPerSec != 0f)
            {
                // EffectiveMax scans every class talent, so compute it once and reuse it for clamp and Set.
                float max = ResourceState.EffectiveMax(entity, pool);
                float cur = ResourceState.Get(entity, pool);
                float regen = pool.RegenPerSec;
                float mul = entity.Stats.GetBlended(canrpgclasses.Core.StatKeys.ResourceRegen);
                if (mul > 0.01f) regen *= mul;
                regen *= RegenMultiplier;
                // Accumulated time, not dt: this body runs once per TickInterval.
                float next = System.Math.Clamp(cur + regen * elapsed, 0f, max);
                if (next != cur) ResourceState.Set(entity, pool, next, max);
            }

            if (cls?.UsesComboPoints == true)
            {
                int combo = ResourceState.Combo(entity);
                if (combo > 0 &&
                    entity.World.ElapsedMilliseconds - ResourceState.ComboLastMs(entity) > (long)(ComboDecaySeconds * 1000f))
                {
                    ResourceState.ResetCombo(entity);
                }
            }
        }
    }
}
