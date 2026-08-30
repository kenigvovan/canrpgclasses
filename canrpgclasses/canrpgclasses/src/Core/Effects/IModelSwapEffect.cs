using System.Collections.Generic;
using Vintagestory.API.Common.Entities;

using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Core.Effects
{
    /// <summary>
    /// An effect that holds a player model swap while it runs. There is only one slot, so anything about to take
    /// it calls <see cref="ModelSwapEffects.CancelActive"/> first and each holder drops its swap synchronously -
    /// waiting for expiry is a tick too late, after the new swap has already no-opped.
    /// </summary>
    public interface IModelSwapEffect
    {
        /// <summary>Ends this effect's model swap right now: restore the model and stop the effect. Must leave the
        /// model-swap slot free by the time it returns.</summary>
        void CancelSwap();
    }

    public static class ModelSwapEffects
    {
        /// <summary>Frees the model-swap slot on <paramref name="e"/> before another swap takes it: cancels every
        /// active <see cref="IModelSwapEffect"/> and clears the toggled aura that would otherwise re-apply it on the
        /// next EBAuras tick. No-op for anyone not currently model-swapped (the normal case).</summary>
        public static void CancelActive(Entity? e)
        {
            var beh = e?.GetBehavior<EBEffects>();
            if (beh?.activeEffects == null) return;

            List<IModelSwapEffect>? holders = null;
            foreach (var kv in beh.activeEffects)
                if (kv.Value is IModelSwapEffect swap) (holders ??= new()).Add(swap);
            if (holders == null) return;

            // A model-swap effect kept alive by a toggled aura (Spirit Wolf) comes back within a tick unless the
            // toggle itself is switched off.
            e!.WatchedAttributes.SetString(AttrKeys.ActiveAura, "");
            foreach (var h in holders) h.CancelSwap();
        }
    }
}
