using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.Core.Integration
{
    /// <summary>
    /// Swaps a player's rendered model by driving PlayerModelLib through its synced <c>"skinModel"</c> attribute,
    /// with no compile-time reference to that mod. The pre-swap code is saved under our own key and restored
    /// whenever the form ends, so nobody is left stuck as a sheep.
    /// </summary>
    public static class PlayerModelSwap
    {
        // PlayerModelLib's synced model-code attribute (PlayerSkinBehavior.GetPlayerModelAttributeValue, default "seraph").
        private const string ModelAttr = "skinModel";
        // Our saved pre-transmute model code (presence also marks "currently swapped by us").
        private const string OrigAttr = "canrpgPolyOrigModel";
        private const string DefaultModel = "seraph";

        /// <summary>Model code Transmute swaps to, registered by our own PlayerModelLib content asset. The domain
        /// prefix is required: PlayerModelLib registers config models under their asset's domain, so a bare
        /// "canrpgsheep" misses the registry and the swap silently no-ops. Only "seraph" escapes that rule.</summary>
        public const string TransmuteModelCode = "canrpgclasses:canrpgsheep";

        /// <summary>Swaps the player to <paramref name="modelCode"/>, remembering their current model to restore later.
        /// No-op if we've already swapped them (don't overwrite the saved original).</summary>
        public static void ToModel(EntityPlayer? player, string modelCode)
        {
            var wa = player?.WatchedAttributes;
            if (wa == null || wa.HasAttribute(OrigAttr)) return;
            wa.SetString(OrigAttr, wa.GetString(ModelAttr, DefaultModel));
            wa.SetString(ModelAttr, modelCode);
        }

        /// <summary>Re-asserts the model if it isn't already active, so a persistent form survives relog: OnStart
        /// doesn't run for a deserialized effect, a re-apply can land as OnStack, and PlayerModelLib may reset
        /// skinModel on join. Called each tick while the form lives; never clobbers the saved original.</summary>
        public static void EnsureModel(EntityPlayer? player, string modelCode)
        {
            var wa = player?.WatchedAttributes;
            if (wa == null || wa.GetString(ModelAttr, DefaultModel) == modelCode) return;
            if (!wa.HasAttribute(OrigAttr)) wa.SetString(OrigAttr, wa.GetString(ModelAttr, DefaultModel));
            wa.SetString(ModelAttr, modelCode);
        }

        /// <summary>The relog re-apply: a delayed callback knocks skinModel back to the saved base so the form's
        /// per-tick <see cref="EnsureModel"/> re-applies it as a real change. Needed because PlayerModelLib
        /// re-tessellates only on a change event, never from the join snapshot. Costs a brief flicker on join.</summary>
        public static void ScheduleReapplyOnJoin(Vintagestory.API.Server.ICoreServerAPI sapi, Vintagestory.API.Server.IServerPlayer player)
        {
            long delayMs = (long)Config.BalanceConfig.Global("modelReapplyOnJoinDelayMs", 2500f);
            sapi.Event.RegisterCallback(_ =>
            {
                var wa = player?.Entity?.WatchedAttributes;
                if (wa == null) return;
                string cur = wa.GetString(ModelAttr, DefaultModel);
                if (!wa.HasAttribute(OrigAttr))
                {
                    // No saved original means we have no swap active. skinModel is shared state - a non-default
                    // value is normally the player's own choice, and resetting it on join stripped their model
                    // and its traits. Never touch a foreign model; only un-stick an orphaned one of ours.
                    if (cur.StartsWith("canrpgclasses:")) wa.SetString(ModelAttr, DefaultModel);
                    return;
                }
                string orig = wa.GetString(OrigAttr, DefaultModel);
                if (cur == orig) return;
                wa.SetString(ModelAttr, orig); // the live form's next tick re-applies the swap as a real change
            }, (int)delayMs);
        }

        /// <summary>Restores the pre-swap model. Only writes the original back while the player still wears our
        /// swap or the base model: if a foreign model landed in skinModel meanwhile, that choice is newer than our
        /// snapshot and overwriting it caused the "reset to seraph after combat" reports. The saved original is
        /// consumed either way, so a stale one can never fire later.</summary>
        public static void Restore(EntityPlayer? player)
        {
            var wa = player?.WatchedAttributes;
            if (wa == null || !wa.HasAttribute(OrigAttr)) return;
            string orig = wa.GetString(OrigAttr, DefaultModel);
            wa.RemoveAttribute(OrigAttr);
            string cur = wa.GetString(ModelAttr, DefaultModel);
            if (cur.StartsWith("canrpgclasses:") || cur == DefaultModel)
                wa.SetString(ModelAttr, orig);
        }

        /// <summary>True if we currently have a model swap active on this player (used to un-stick on login).</summary>
        public static bool IsSwapped(Entity? e) => e?.WatchedAttributes?.HasAttribute(OrigAttr) ?? false;

        /// <summary>The player's own model, as opposed to whatever they are rendered as right now: while a form is
        /// active the live skinModel is the form. Anything keying off race must read this, or a shapeshifted
        /// player counts as a different one.</summary>
        public static string BaseModelOf(Entity? e)
        {
            var wa = e?.WatchedAttributes;
            if (wa == null) return DefaultModel;
            return wa.HasAttribute(OrigAttr) ? wa.GetString(OrigAttr, DefaultModel) : wa.GetString(ModelAttr, DefaultModel);
        }
    }
}
