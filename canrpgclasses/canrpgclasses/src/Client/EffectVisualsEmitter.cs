using Vintagestory.API.Client;
using canrpgclasses.Core.Visuals;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Client half of <see cref="EffectVisuals"/>: reads the synced bitmask off nearby entities and spawns
    /// state particles locally.
    /// </summary>
    public class EffectVisualsEmitter
    {
        private const float Range = 48f;

        private readonly ICoreClientAPI capi;
        private readonly long listenerId;
        private readonly long orbitListenerId;

        public EffectVisualsEmitter(ICoreClientAPI capi)
        {
            this.capi = capi;
            listenerId = capi.Event.RegisterGameTickListener(OnTick, 250);
            // Fast tick so charge orbs orbit smoothly instead of hopping.
            orbitListenerId = capi.Event.RegisterGameTickListener(OnOrbitTick, 60);
        }

        private void OnTick(float dt)
        {
            var self = capi.World?.Player?.Entity;
            if (self == null) return;

            foreach (var e in capi.World.GetEntitiesAround(self.Pos.XYZ, Range, Range,
                         en => en.Alive && (en.WatchedAttributes?.GetLong(EffectVisuals.MaskAttr, 0) ?? 0) != 0))
            {
                EffectVisuals.EmitMask(capi.World, e, e.WatchedAttributes.GetLong(EffectVisuals.MaskAttr, 0));
            }
        }

        private void OnOrbitTick(float dt)
        {
            var self = capi.World?.Player?.Entity;
            if (self == null) return;

            foreach (var e in capi.World.GetEntitiesAround(self.Pos.XYZ, Range, Range,
                         en => en.Alive && en.WatchedAttributes != null
                               && en.WatchedAttributes.GetLong(EffectVisuals.CountsAttr, 0) != 0))
            {
                EffectVisuals.EmitOrbits(capi.World, e, e.WatchedAttributes.GetLong(EffectVisuals.CountsAttr, 0));
            }
        }

        public void Dispose()
        {
            capi.Event.UnregisterGameTickListener(listenerId);
            capi.Event.UnregisterGameTickListener(orbitListenerId);
        }
    }
}
