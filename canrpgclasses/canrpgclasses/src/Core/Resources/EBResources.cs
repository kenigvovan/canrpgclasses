using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Core.Resources
{
    /// <summary>
    /// Server-side ticker for the class resource: advances the current class's primary pool by its
    /// <see cref="ResourcePoolDef.RegenPerSec"/> and drops combo points after a spell of inactivity, so they
    /// don't carry forever out of combat. The values themselves live in <see cref="ResourceState"/>.
    /// </summary>
    public class EBResources : EntityBehavior
    {
        public const string Name = "canrpgresources";

        /// <summary>Seconds without a builder before combo points reset to 0.</summary>
        public static float ComboDecaySeconds => BalanceConfig.Global("comboDecaySeconds", 12f);

        /// <summary>Server-wide multiplier on every passive resource regen - one knob an admin can turn without
        /// re-tuning each class's own rate. 0.5 = half speed.</summary>
        public static float RegenMultiplier => BalanceConfig.Global("resourceRegenMultiplier", 1f);

        private bool IsServer => entity.Api.Side == EnumAppSide.Server;

        // ~5 times a second rather than on every server tick. Each write to the pool re-syncs it to the client
        // and the max-scan below is pure recompute, so ticking 6x less is free on the eye and cheap on the wire.
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
                // Talents scale the regen; unset blends to 1.0.
                float regen = pool.RegenPerSec;
                float mul = entity.Stats.GetBlended(canrpgclasses.Core.StatKeys.ResourceRegen);
                if (mul > 0.01f) regen *= mul;
                // The pace knob comes last, so it scales the talent-boosted rate too.
                regen *= RegenMultiplier;
                // Against the accumulated time, not the frame's dt - dt would fill the pool ~6x slower than
                // RegenPerSec promises, since this body runs once per TickInterval.
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
