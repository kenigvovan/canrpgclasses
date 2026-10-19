using System.Reflection;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using canrpgclasses.Core.Items;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Core.HarmonyPatches
{
    /// <summary>Client: right-click on an item with a bound spell (stack attribute) casts it through the hotbar
    /// request path instead of the normal use.</summary>
    public static class SpellItemUsePatches
    {
        private static FieldInfo? prevMouseRightField;

        // Prefix of TryBeginUseActiveSlotItem(BlockSelection, EntitySelection, EnumHandInteract useType, ref EnumHandHandling handling).
        public static bool Prefix(object __instance, EnumHandInteract useType, ref EnumHandHandling handling, ref bool __result)
        {
            if (useType != EnumHandInteract.HeldItemInteract) return true;

            var capi = canrpgclassesModSystem.ClientApi;
            if (capi == null) return true;

            // Initial press only, so holding the button doesn't spam casts.
            prevMouseRightField ??= __instance.GetType().GetField("prevMouseRight");
            if (prevMouseRightField?.GetValue(__instance) is bool prev && prev) return true;

            var stack = capi.World?.Player?.InventoryManager?.ActiveHotbarSlot?.Itemstack;
            var player = capi.World?.Player?.Entity;
            // Pool-restricted spells fall through to normal use rather than a cast the server would reject.
            string? spellId = player != null ? BehaviorSpellContainer.GetUsableBoundSpell(stack, TalentState.CurrentClass(player)) : null;
            if (string.IsNullOrEmpty(spellId)) return true;

            canrpgclassesModSystem.ClientInstance?.Hotbar?.Cast(spellId!);
            handling = EnumHandHandling.PreventDefault;
            __result = true;
            return false;
        }

        // Appends the bound spell to the tooltip, unless the holder's class can't use it.
        public static void Postfix_GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc)
        {
            var player = canrpgclassesModSystem.ClientApi?.World?.Player?.Entity;
            string? id = player != null ? BehaviorSpellContainer.GetUsableBoundSpell(inSlot?.Itemstack, TalentState.CurrentClass(player)) : null;
            if (string.IsNullOrEmpty(id)) return;

            string label = id!;
            var mod = canrpgclassesModSystem.ClientInstance;
            if (mod != null && mod.Spells.TryGet(id!, out var spell)) label = spell.DisplayName;
            dsc.AppendLine(Lang.Get("canrpgclasses:item-bound-spell", label));
        }
    }
}
