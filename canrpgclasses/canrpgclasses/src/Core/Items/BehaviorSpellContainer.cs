using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace canrpgclasses.Core.Items
{
    /// <summary>
    /// Marks a collectible as spell-capable, with an optional default spell from JSON; the real binding is
    /// per-stack. The static helpers work on items without this behavior too, which is what lets the
    /// right-click cast patch and the tooltip apply to anything.
    /// </summary>
    public class BehaviorSpellContainer : CollectibleBehavior
    {
        public const string Name = "canrpgspellcontainer";
        public const string AttrTree = "spellContainer";

        /// <summary>Default spell a fresh stack of this item type is bound to (first id), from JSON properties.</summary>
        internal string[] defaultSpellIds = Array.Empty<string>();

        /// <summary>Class restriction from JSON: only that class may use whatever spell is bound here, so a
        /// class-flavoured weapon doesn't leak its spell onto whoever picks it up.</summary>
        internal string? pool;

        public BehaviorSpellContainer(CollectibleObject collObj) : base(collObj) { }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            defaultSpellIds = properties["spellIds"].AsArray<string>(Array.Empty<string>());
            pool = properties["pool"].AsString(null);
        }

        /// <summary>The spell id bound to this stack: the per-stack attribute, else the item-type default, else null.
        /// Static so it works on any item - items without this behavior simply have no default to fall back to.</summary>
        public static string? GetBoundSpell(ItemStack? stack)
        {
            if (stack == null) return null;

            string? fromStack = First(stack.Attributes?.GetTreeAttribute(AttrTree)?.GetString("spellIds"));
            if (fromStack != null) return fromStack;

            var beh = stack.Collectible?.GetCollectibleBehavior(typeof(BehaviorSpellContainer), true) as BehaviorSpellContainer;
            return beh != null && beh.defaultSpellIds.Length > 0 ? beh.defaultSpellIds[0] : null;
        }

        /// <summary>Writes the bound spell on the stack. Null/empty clears the override (reverting to the item
        /// default, if any). One spell per item.</summary>
        public static void SetBoundSpell(ItemStack stack, string? spellId)
        {
            if (stack?.Attributes == null) return;
            if (string.IsNullOrEmpty(spellId)) { stack.Attributes.RemoveAttribute(AttrTree); return; }
            stack.Attributes.GetOrAddTreeAttribute(AttrTree).SetString("spellIds", spellId);
        }

        /// <summary>Whether this class may use the spell bound to the stack. An item with no pool restriction
        /// works for anyone; a restricted one only for its own class, default binding or rebind alike.</summary>
        public static bool PoolAllows(ItemStack? stack, string classId)
        {
            var beh = stack?.Collectible?.GetCollectibleBehavior(typeof(BehaviorSpellContainer), true) as BehaviorSpellContainer;
            string? pool = beh?.pool;
            if (string.IsNullOrEmpty(pool)) return true;

            int c = pool!.IndexOf(':');
            string poolClass = c >= 0 ? pool.Substring(c + 1) : pool;
            return string.Equals(poolClass, classId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The bound spell, but only if this class may use it. Call this rather than pairing
        /// <see cref="GetBoundSpell"/> with <see cref="PoolAllows"/> by hand, so a new call site can't quietly
        /// skip the restriction.</summary>
        public static string? GetUsableBoundSpell(ItemStack? stack, string classId)
        {
            string? bound = GetBoundSpell(stack);
            return !string.IsNullOrEmpty(bound) && PoolAllows(stack, classId) ? bound : null;
        }

        private static string? First(string? csv)
        {
            if (string.IsNullOrEmpty(csv)) return null;
            foreach (var part in csv!.Split(',')) { var s = part.Trim(); if (s.Length > 0) return s; }
            return null;
        }
    }
}
