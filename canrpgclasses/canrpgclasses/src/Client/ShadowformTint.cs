using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Client-only: tints any entity in Shadow Guise dark violet via <see cref="Entity.RenderColor"/>. Runs as a
    /// tick, not a one-shot on toggle: the engine's hurt-flash rewrites RenderColor, so the tint must be
    /// re-asserted.
    /// </summary>
    public class ShadowformTint
    {
        private readonly ICoreClientAPI capi;
        private long listenerId;
        private HashSet<long> tinted = new();

        // Dark violet, packed as the ARGB int the entity shader decodes (A<<24 | R<<16 | G<<8 | B). Below-255
        // channels multiply the model darker; the low red/green + higher blue reads as shadow.
        private const int Tint = (255 << 24) | (95 << 16) | (45 << 8) | 135;
        private const string ShadowformAura = "canrpgclasses:shadow_guise";

        public ShadowformTint(ICoreClientAPI capi)
        {
            this.capi = capi;
            listenerId = capi.Event.RegisterGameTickListener(OnTick, 150);
        }

        private void OnTick(float dt)
        {
            var self = capi.World?.Player?.Entity;
            if (self == null) return;

            var current = new HashSet<long>();
            foreach (var e in capi.World.GetEntitiesAround(self.Pos.XYZ, 64f, 64f,
                         en => en.WatchedAttributes?.GetString("canrpgActiveAura", "") == ShadowformAura))
            {
                e.RenderColor = Tint;
                current.Add(e.EntityId);
            }

            foreach (var id in tinted)
                if (!current.Contains(id) && capi.World.GetEntityById(id) is { } e)
                    e.RenderColor = -1;

            tinted = current;
        }

        public void Dispose()
        {
            foreach (var id in tinted)
                if (capi.World?.GetEntityById(id) is { } e) e.RenderColor = -1;
            capi.Event.UnregisterGameTickListener(listenerId);
        }
    }
}
