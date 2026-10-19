using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.Core.Integration
{
    /// <summary>Swaps the rendered model via PlayerModelLib's synced <c>"skinModel"</c> attribute (no compile-time
    /// reference). The original code is saved under our own key and restored when the form ends.</summary>
    public static class PlayerModelSwap
    {
        // PlayerModelLib's synced model-code attribute (PlayerSkinBehavior.GetPlayerModelAttributeValue, default "seraph").
        private const string ModelAttr = "skinModel";
        // Our saved pre-transmute model code (presence also marks "currently swapped by us").
        private const string OrigAttr = "canrpgPolyOrigModel";
        private const string DefaultModel = "seraph";

        /// <summary>Transmute model. The domain prefix is required: PlayerModelLib registers config models under
        /// their asset domain, so a bare code silently no-ops.</summary>
        public const string TransmuteModelCode = "canrpgclasses:canrpgsheep";

        /// <summary>No-op if already swapped, so the saved original is never overwritten.</summary>
        public static void ToModel(EntityPlayer? player, string modelCode)
        {
            var wa = player?.WatchedAttributes;
            if (wa == null || wa.HasAttribute(OrigAttr)) return;
            wa.SetString(OrigAttr, wa.GetString(ModelAttr, DefaultModel));
            wa.SetString(ModelAttr, modelCode);
        }

        /// <summary>Per-tick re-assert so a form survives relog (OnStart doesn't run for a deserialized effect, and
        /// PlayerModelLib may reset skinModel on join).</summary>
        public static void EnsureModel(EntityPlayer? player, string modelCode)
        {
            var wa = player?.WatchedAttributes;
            if (wa == null || wa.GetString(ModelAttr, DefaultModel) == modelCode) return;
            if (!wa.HasAttribute(OrigAttr)) wa.SetString(OrigAttr, wa.GetString(ModelAttr, DefaultModel));
            wa.SetString(ModelAttr, modelCode);
        }

        /// <summary>PlayerModelLib only re-tessellates on a change event, so after join skinModel is reset to the base
        /// and <see cref="EnsureModel"/> re-applies the form. Brief flicker.</summary>
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
                    // No swap of ours: never touch a foreign model, only un-stick an orphaned one of ours.
                    if (cur.StartsWith("canrpgclasses:")) wa.SetString(ModelAttr, DefaultModel);
                    return;
                }
                string orig = wa.GetString(OrigAttr, DefaultModel);
                if (cur == orig) return;
                wa.SetString(ModelAttr, orig); // the live form's next tick re-applies the swap as a real change
            }, (int)delayMs);
        }

        /// <summary>Restores the original only if the player still wears our swap or the base model; a foreign model
        /// set meanwhile is newer. The saved original is consumed either way.</summary>
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

        /// <summary>The player's own model, not the active form. Anything keying off race must read this.</summary>
        public static string BaseModelOf(Entity? e)
        {
            var wa = e?.WatchedAttributes;
            if (wa == null) return DefaultModel;
            return wa.HasAttribute(OrigAttr) ? wa.GetString(OrigAttr, DefaultModel) : wa.GetString(ModelAttr, DefaultModel);
        }
    }
}
