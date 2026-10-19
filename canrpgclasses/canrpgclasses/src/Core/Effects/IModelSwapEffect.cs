using System.Collections.Generic;
using Vintagestory.API.Common.Entities;

using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Core.Effects
{
    /// <summary>Effect holding the single model-swap slot. Holders must drop synchronously via
    /// <see cref="ModelSwapEffects.CancelActive"/>; waiting for expiry is a tick too late for the next swap.</summary>
    public interface IModelSwapEffect
    {
        /// <summary>Restores the model and stops the effect; the slot must be free on return.</summary>
        void CancelSwap();
    }

    public static class ModelSwapEffects
    {
        /// <summary>Cancels every active <see cref="IModelSwapEffect"/> on <paramref name="e"/> before another swap.</summary>
        public static void CancelActive(Entity? e)
        {
            var beh = e?.GetBehavior<EBEffects>();
            if (beh?.activeEffects == null) return;

            List<IModelSwapEffect>? holders = null;
            foreach (var kv in beh.activeEffects)
                if (kv.Value is IModelSwapEffect swap) (holders ??= new()).Add(swap);
            if (holders == null) return;

            // Clear the toggled aura too, or EBAuras re-applies the swap (Spirit Wolf) next tick.
            e!.WatchedAttributes.SetString(AttrKeys.ActiveAura, "");
            foreach (var h in holders) h.CancelSwap();
        }
    }
}
