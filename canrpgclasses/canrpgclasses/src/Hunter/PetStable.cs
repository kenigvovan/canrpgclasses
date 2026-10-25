using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Hunter
{
    /// <summary>The hunter's tamed pets, stored on the owner since pet entities never persist. Tree
    /// <c>canrpgPetStable</c>: int v, int active, int count, and slots s0..sN of {species (the look), name, color,
    /// rareColor, cosmetics}.</summary>
    public static class PetStable
    {
        public const string Key = "canrpgPetStable";

        /// <summary>Base slots plus those earned from bestiary milestones, capped.</summary>
        public static int MaxSlots(Entity owner)
            => System.Math.Min((int)BalanceConfig.Global("petStableSlots", 3f) + PetJournal.SlotBonus(owner),
                (int)BalanceConfig.Global("petStableSlotsMax", 8f));

        /// <summary>The stable, created on first use with the starting wolf (carrying over the old single pet name).</summary>
        public static ITreeAttribute Tree(Entity owner)
        {
            var wa = owner.WatchedAttributes;
            if (wa.GetTreeAttribute(Key) is { } tree)
            {
                if (tree.GetInt("v", 1) < Version) Migrate(owner, tree);
                return tree;
            }

            tree = new TreeAttribute();
            tree.SetInt("v", Version);
            tree.SetInt("active", 0);
            tree.SetInt("count", 1);
            var slot = new TreeAttribute();
            slot.SetString("species", PetSpecies.Default);
            slot.SetString("name", wa.GetString(HunterPetSystem.PetNameKey, ""));
            tree["s0"] = slot;
            wa[Key] = tree;
            wa.MarkPathDirty(Key);
            return tree;
        }

        // 2: slots hold a look (bear-polar), not a species (bear).
        private const int Version = 2;

        /// <summary>Server only: the client must not rewrite a synced tree it doesn't own.</summary>
        private static void Migrate(Entity owner, ITreeAttribute tree)
        {
            if (owner.World.Side != EnumAppSide.Server) return;
            int n = tree.GetInt("count", 0);
            for (int i = 0; i < n; i++)
            {
                if (tree.GetTreeAttribute("s" + i) is not { } slot) continue;
                string old = slot.GetString("species", PetSpecies.Default);
                int color = slot.GetInt("color", 0);
                // The old fox drew its rare arctic coat as colour 1; that coat is a look of its own now.
                string look = old == "fox" && color == 1 ? "fox-arctic-male" : PetSpecies.Migrate(old);
                if (color >= PetSpecies.ColorCount(owner.World, look)) color = 0;
                slot.SetString("species", look);
                slot.SetInt("color", color);
                slot.SetInt("rareColor", PetSpecies.IsRareColor(owner.World, look, color) ? color : -1);
            }
            tree.SetInt("v", Version);
            owner.WatchedAttributes.MarkPathDirty(Key);
        }

        public static int Count(Entity owner) => Tree(owner).GetInt("count", 0);
        public static int ActiveIndex(Entity owner) => Tree(owner).GetInt("active", 0);
        public static ITreeAttribute? Slot(Entity owner, int i) => Tree(owner).GetTreeAttribute("s" + i);
        public static ITreeAttribute? Active(Entity owner) => Slot(owner, ActiveIndex(owner));

        public static string ActiveSpecies(Entity owner) => Active(owner)?.GetString("species", PetSpecies.Default) ?? PetSpecies.Default;
        public static string ActiveName(Entity owner) => Active(owner)?.GetString("name", "") ?? "";
        public static int ActiveColor(Entity owner) => Active(owner)?.GetInt("color", 0) ?? 0;
        /// <summary>The active pet's worn cosmetics: place → item code. Null if it wears none.</summary>
        public static ITreeAttribute? ActiveCosmetics(Entity owner) => Active(owner)?.GetTreeAttribute("cosmetics");

        /// <summary>Puts an item code on a place of the active pet, or clears the place with null.</summary>
        public static void SetActiveCosmetic(Entity owner, string place, string? itemCode)
        {
            var slot = Active(owner);
            if (slot == null) return;
            var worn = slot.GetOrAddTreeAttribute("cosmetics");
            if (itemCode == null) worn.RemoveAttribute(place);
            else worn.SetString(place, itemCode);
            Dirty(owner);
        }

        /// <summary>Steps a slot to its species' next colour, wrapping round. Rare colours are skipped, except the one
        /// this pet was tamed with.</summary>
        public static void CycleColor(Entity owner, int i)
        {
            var slot = Slot(owner, i);
            if (slot == null) return;
            string species = slot.GetString("species", PetSpecies.Default);
            int n = System.Math.Max(1, PetSpecies.ColorCount(owner.World, species));
            int own = slot.GetInt("rareColor", -1);
            int color = slot.GetInt("color", 0);
            for (int step = 0; step < n; step++)
            {
                color = (color + 1) % n;
                if (color == own || !PetSpecies.IsRareColor(owner.World, species, color)) break;
            }
            slot.SetInt("color", color);
            Dirty(owner);
        }

        public static bool IsRare(ITreeAttribute? slot) => slot != null && slot.GetInt("rareColor", -1) >= 0;

        public static bool IsFull(Entity owner) => Count(owner) >= MaxSlots(owner);

        /// <summary>Adds a freshly tamed pet and makes it active; -1 when the stable is full.</summary>
        public static int Add(Entity owner, string species, int color = 0)
        {
            var tree = Tree(owner);
            int n = tree.GetInt("count", 0);
            if (n >= MaxSlots(owner)) return -1;
            var slot = new TreeAttribute();
            slot.SetString("species", species);
            slot.SetString("name", "");
            slot.SetInt("color", color);
            // A rare roll stays this pet's for good, even after switching to common colours.
            slot.SetInt("rareColor", PetSpecies.IsRareColor(owner.World, species, color) ? color : -1);
            tree["s" + n] = slot;
            tree.SetInt("count", n + 1);
            SetActive(owner, n);
            return n;
        }

        public static void SetActive(Entity owner, int i)
        {
            var tree = Tree(owner);
            if (i < 0 || i >= tree.GetInt("count", 0)) return;
            tree.SetInt("active", i);
            Dirty(owner);
        }

        /// <summary>Removes a slot and closes the gap; the last remaining pet can't be released.</summary>
        public static bool Release(Entity owner, int i)
        {
            var tree = Tree(owner);
            int n = tree.GetInt("count", 0);
            if (n <= 1 || i < 0 || i >= n) return false;
            for (int k = i; k < n - 1; k++) tree["s" + k] = tree.GetTreeAttribute("s" + (k + 1));
            tree.RemoveAttribute("s" + (n - 1));
            tree.SetInt("count", n - 1);
            int active = tree.GetInt("active", 0);
            if (active > i || active >= n - 1) tree.SetInt("active", System.Math.Max(0, active - 1));
            Dirty(owner);
            return true;
        }

        public static void SetActiveName(Entity owner, string name)
        {
            Active(owner)?.SetString("name", name);
            Dirty(owner);
        }

        private static void Dirty(Entity owner)
        {
            owner.WatchedAttributes.MarkPathDirty(Key);
            // Mirrored for the pet panel, which reads the single name key.
            owner.WatchedAttributes.SetString(HunterPetSystem.PetNameKey, ActiveName(owner));
            owner.WatchedAttributes.MarkPathDirty(HunterPetSystem.PetNameKey);
        }
    }
}
