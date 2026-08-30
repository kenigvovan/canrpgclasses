using System.Reflection;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using canrpgclasses.Core.Items;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Core.HarmonyPatches
{
    /// <summary>
    /// Client-side: right-clicking an item with a bound spell casts it instead of the item's normal use. The
    /// binding rides in the stack attributes, so no behavior is attached to the item, and the cast goes down the
    /// same request path as the hotbar - the server still authorizes it.
    /// </summary>
    public static class SpellItemUsePatches
    {
        private static FieldInfo? prevMouseRightField;

        // Prefix of TryBeginUseActiveSlotItem(BlockSelection, EntitySelection, EnumHandInteract useType, ref EnumHandHandling handling).
        public static bool Prefix(object __instance, EnumHandInteract useType, ref EnumHandHandling handling, ref bool __result)
        {
            if (useType != EnumHandInteract.HeldItemInteract) return true; // only right-click "use", not attack

            var capi = canrpgclassesModSystem.ClientApi;
            if (capi == null) return true;

            // Only on the initial press (prevMouseRight == false), so holding the button doesn't spam casts.
            prevMouseRightField ??= __instance.GetType().GetField("prevMouseRight");
            if (prevMouseRightField?.GetValue(__instance) is bool prev && prev) return true;

            var stack = capi.World?.Player?.InventoryManager?.ActiveHotbarSlot?.Itemstack;
            var player = capi.World?.Player?.Entity;
            // Null or pool-restricted (e.g. a rogue-only dagger held by another class) → normal interaction
            // instead of firing a cast the server would just reject.
            string? spellId = player != null ? BehaviorSpellContainer.GetUsableBoundSpell(stack, TalentState.CurrentClass(player)) : null;
            if (string.IsNullOrEmpty(spellId)) return true;

            canrpgclassesModSystem.ClientInstance?.Hotbar?.Cast(spellId!);
            handling = EnumHandHandling.PreventDefault;
            __result = true;
            return false; // skip the original method
        }

        // Postfix of CollectibleObject.GetHeldItemInfo: append the bound spell to the item's tooltip so the player
        // can see what a right-click will cast. Reads straight off the stack attributes (any item, no behavior).
        public static void Postfix_GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc)
        {
            var player = canrpgclassesModSystem.ClientApi?.World?.Player?.Entity;
            // Null or pool-restricted → don't advertise a cast the holder's class can't actually use (e.g. a
            // rogue-only dagger's default spell shown to a paladin) - the server would silently reject it.
            string? id = player != null ? BehaviorSpellContainer.GetUsableBoundSpell(inSlot?.Itemstack, TalentState.CurrentClass(player)) : null;
            if (string.IsNullOrEmpty(id)) return;

            string label = id!;
            var mod = canrpgclassesModSystem.ClientInstance;
            if (mod != null && mod.Spells.TryGet(id!, out var spell)) label = spell.DisplayName;
            dsc.AppendLine(Lang.Get("canrpgclasses:item-bound-spell", label));
        }
    }
}
