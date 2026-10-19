using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Tints entities in Shadow Guise via <see cref="Entity.RenderColor"/>. Re-applied on a tick because the
    /// hurt-flash overwrites RenderColor.
    /// </summary>
    public class ShadowformTint
    {
        private readonly ICoreClientAPI capi;
        private long listenerId;
        private HashSet<long> tinted = new();

        // Dark violet as packed ARGB; channels below 255 multiply the model darker.
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
