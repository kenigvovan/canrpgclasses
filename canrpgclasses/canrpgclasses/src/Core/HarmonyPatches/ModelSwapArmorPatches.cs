using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.Core.HarmonyPatches
{
    /// <summary>Hides worn armour in a model-swap form via a client prefix on PlayerModelLib's <c>ProcessSlot</c>
    /// (resolved by name). Skipping the slot also stops it deleting the form's body elements under the armour.</summary>
    public static class ModelSwapArmorPatches
    {
        private static FieldInfo? playerField;

        /// <summary>Caches the reflected <c>PlayerEntity</c> field of WearablesTesselatorBehavior.</summary>
        public static void Init(Type tesselatorType)
        {
            playerField = HarmonyLib.AccessTools.Field(tesselatorType, "PlayerEntity");
        }

        public static bool Prefix_ProcessSlot(object __instance)
        {
            var pe = playerField?.GetValue(__instance) as EntityPlayer;
            string skin = pe?.WatchedAttributes?.GetString("skinModel", "") ?? "";
            return !skin.StartsWith("canrpgclasses:", StringComparison.Ordinal);
        }
    }
}
