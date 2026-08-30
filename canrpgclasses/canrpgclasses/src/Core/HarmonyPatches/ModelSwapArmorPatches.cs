using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.Core.HarmonyPatches
{
    /// <summary>
    /// Hides worn armour while a player is in a model-swap form: a client-side prefix on PlayerModelLib's
    /// <c>ProcessSlot</c> (resolved by name, no compile-time reference). Skipping the slot also keeps the form's
    /// own body elements, which ProcessSlot would otherwise delete where the armour covers them.
    /// </summary>
    public static class ModelSwapArmorPatches
    {
        private static FieldInfo? playerField;

        /// <summary>Caches the reflected <c>PlayerEntity</c> field so the per-slot prefix doesn't re-resolve it.
        /// Called once with the resolved WearablesTesselatorBehavior type.</summary>
        public static void Init(Type tesselatorType)
        {
            playerField = HarmonyLib.AccessTools.Field(tesselatorType, "PlayerEntity");
        }

        public static bool Prefix_ProcessSlot(object __instance)
        {
            var pe = playerField?.GetValue(__instance) as EntityPlayer;
            string skin = pe?.WatchedAttributes?.GetString("skinModel", "") ?? "";
            // false = skip the original ProcessSlot → this wearable isn't attached to the swapped model.
            return !skin.StartsWith("canrpgclasses:", StringComparison.Ordinal);
        }
    }
}
