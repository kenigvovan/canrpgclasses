using Vintagestory.API.Client;
using canrpgclasses.Core.Visuals;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Client half of the effect-visuals split (see <see cref="EffectVisuals"/>): reads the synced per-entity
    /// bitmask off nearby entities and spawns their state particles locally. The server publishes only the mask,
    /// never particles.
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
            // Charge orbs need a fast tick: they're re-placed each call and the small per-tick step is what makes
            // them read as balls FLYING around the body rather than hopping (a 250ms step would visibly jump).
            orbitListenerId = capi.Event.RegisterGameTickListener(OnOrbitTick, 60);
        }

        private void OnTick(float dt)
        {
            var self = capi.World?.Player?.Entity;
            if (self == null) return;

            // (Channel beams are drawn by BeamRenderer - a solid per-frame ribbon mesh, not particles.)
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
