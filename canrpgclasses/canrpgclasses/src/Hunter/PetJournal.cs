using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using canrpgclasses.Core.Portals;

namespace canrpgclasses.Hunter
{
    /// <summary>The bestiary: every (look, colour) the hunter ever tamed, kept on the owner apart from the stable so a
    /// release never loses it. Milestones in <c>config/petjournal.json</c> pay out once each, in cosmetics, stable
    /// slots and tricks only. Tree <c>canrpgPetJournal</c>: looks{look: colour bitmask}, claimed{m#}, slots, tricks{}.</summary>
    public static class PetJournal
    {
        public const string Key = "canrpgPetJournal";

        private static JsonObject? config;

        public static JsonObject Config(ICoreAPI api)
        {
            if (config != null) return config;
            var asset = api.Assets.TryGet(new AssetLocation(canrpgclassesModSystem.ModId, "config/petjournal.json"));
            config = JsonObject.FromJson(asset?.ToText() ?? "{}");
            return config;
        }

        public static JsonObject[] Milestones(ICoreAPI api) => Config(api)["milestones"].AsArray() ?? System.Array.Empty<JsonObject>();

        /// <summary>Synced journal, or null before the first tame. Safe on the client.</summary>
        public static ITreeAttribute? Read(Entity owner) => owner.WatchedAttributes.GetTreeAttribute(Key);

        /// <summary>Server: the journal, created on first use and seeded from the stable (pets tamed before it existed).</summary>
        private static ITreeAttribute Tree(Entity owner)
        {
            var wa = owner.WatchedAttributes;
            if (wa.GetTreeAttribute(Key) is { } tree) return tree;
            tree = new TreeAttribute();
            wa[Key] = tree;
            for (int i = 0; i < PetStable.Count(owner); i++)
                if (PetStable.Slot(owner, i) is { } slot)
                    Mark(owner.World, tree, slot.GetString("species", PetSpecies.Default), slot.GetInt("color", 0));
            wa.MarkPathDirty(Key);
            return tree;
        }

        /// <summary>Looks that can be tamed from the wild; the starting wolf can't, so it isn't collected.</summary>
        public static bool Journaled(IWorldAccessor world, string look)
            => PetSpecies.Look(PetSpecies.TypeFor(world, look))?["tameCodes"].AsArray<string>()?.Length > 0;

        public static List<string> JournaledLooks(IWorldAccessor world)
            => PetSpecies.AllLooks(world).FindAll(l => Journaled(world, l));

        public static int Mask(ITreeAttribute? tree, string look) => tree?.GetTreeAttribute("looks")?.GetInt(look, 0) ?? 0;

        public static bool Has(ITreeAttribute? tree, string look, int color) => (Mask(tree, look) >> color & 1) == 1;

        private static bool Mark(IWorldAccessor world, ITreeAttribute tree, string look, int color)
        {
            if (!Journaled(world, look) || color < 0 || color > 30 || Has(tree, look, color)) return false;
            var looks = tree.GetOrAddTreeAttribute("looks");
            looks.SetInt(look, looks.GetInt(look, 0) | 1 << color);
            return true;
        }

        public static int Entries(ITreeAttribute? tree)
        {
            int n = 0;
            if (tree?.GetTreeAttribute("looks") is { } looks)
                foreach (var kv in looks) if (kv.Value is IntAttribute m) n += System.Numerics.BitOperations.PopCount((uint)m.value);
            return n;
        }

        public static int Total(IWorldAccessor world)
        {
            int n = 0;
            foreach (string look in JournaledLooks(world)) n += PetSpecies.ColorCount(world, look);
            return n;
        }

        public static int RareEntries(IWorldAccessor world, ITreeAttribute? tree)
        {
            int n = 0;
            foreach (string look in JournaledLooks(world))
                foreach (int c in PetSpecies.RareColors(world, look)) if (Has(tree, look, c)) n++;
            return n;
        }

        /// <summary>Every look of the family tamed at least once, in any colour.</summary>
        public static bool FamilyComplete(IWorldAccessor world, ITreeAttribute? tree, string family)
        {
            bool any = false;
            foreach (string look in JournaledLooks(world))
            {
                if (PetSpecies.FamilyOf(world, look) != family) continue;
                if (Mask(tree, look) == 0) return false;
                any = true;
            }
            return any;
        }

        public static int SlotBonus(Entity owner) => Read(owner)?.GetInt("slots", 0) ?? 0;

        public static bool TrickUnlocked(Entity owner, string trick)
        {
            var defaults = Config(owner.Api)["defaultTricks"].AsArray<string>();
            if (defaults != null && System.Array.IndexOf(defaults, trick) >= 0) return true;
            return Read(owner)?.GetTreeAttribute("tricks")?.GetBool(trick, false) ?? false;
        }

        public static bool Claimed(ITreeAttribute? tree, int i) => tree?.GetTreeAttribute("claimed")?.GetBool("m" + i, false) ?? false;

        public static bool Reached(IWorldAccessor world, ITreeAttribute? tree, JsonObject m)
        {
            int entries = m["entries"].AsInt(0), rare = m["rare"].AsInt(0);
            string? family = m["family"].AsString(null);
            if (entries <= 0 && rare <= 0 && family == null) return false;
            return Entries(tree) >= entries && RareEntries(world, tree) >= rare
                && (family == null || FamilyComplete(world, tree, family));
        }

        /// <summary>Server: writes a fresh tame into the journal and pays any milestone it reaches.</summary>
        public static void Record(EntityAgent owner, string look, int color)
        {
            if (owner.World.Side != EnumAppSide.Server) return;
            var tree = Tree(owner);
            if (!Mark(owner.World, tree, look, color)) return;
            PortalSpells.Tell(owner, Lang.Get("canrpgclasses:msg-journal-new", Entries(tree), Total(owner.World)));
            CheckMilestones(owner, tree);
            owner.WatchedAttributes.MarkPathDirty(Key);
        }

        private static void CheckMilestones(EntityAgent owner, ITreeAttribute tree)
        {
            var milestones = Milestones(owner.Api);
            for (int i = 0; i < milestones.Length; i++)
            {
                if (Claimed(tree, i) || !Reached(owner.World, tree, milestones[i])) continue;
                tree.GetOrAddTreeAttribute("claimed").SetBool("m" + i, true);
                foreach (string reward in Grant(owner, tree, milestones[i]))
                    PortalSpells.Tell(owner, Lang.Get("canrpgclasses:msg-journal-milestone", reward));
            }
        }

        private static List<string> Grant(EntityAgent owner, ITreeAttribute tree, JsonObject m)
        {
            var rewards = new List<string>();
            int slots = m["slots"].AsInt(0);
            if (slots > 0)
            {
                tree.SetInt("slots", tree.GetInt("slots", 0) + slots);
                rewards.Add(Lang.Get("canrpgclasses:journal-reward-slots", slots));
            }
            foreach (string trick in m["tricks"].AsArray<string>() ?? System.Array.Empty<string>())
            {
                tree.GetOrAddTreeAttribute("tricks").SetBool(trick, true);
                rewards.Add(Lang.Get("canrpgclasses:journal-reward-trick", Lang.Get("canrpgclasses:ui-pet-trick-" + trick)));
            }
            if (m["item"].AsString(null) is { } code && GiveItem(owner, code) is { } name)
                rewards.Add(name);
            return rewards;
        }

        /// <summary>Into the inventory, or at the feet when it's full. Returns the item's name, null if unknown.</summary>
        private static string? GiveItem(EntityAgent owner, string code)
        {
            var item = owner.World.GetItem(new AssetLocation(code));
            if (item == null) { owner.Api.Logger.Warning("[canrpgclasses] pet journal reward '{0}' is not an item", code); return null; }
            var stack = new ItemStack(item);
            string name = stack.GetName();
            if (!((owner as EntityPlayer)?.Player?.InventoryManager?.TryGiveItemstack(stack, true) ?? false))
                owner.World.SpawnItemEntity(stack, owner.Pos.XYZ);
            return name;
        }

        /// <summary>The first milestone still ahead that counts entries, for the journal header; null when none is left.</summary>
        public static JsonObject? NextEntryMilestone(ICoreAPI api, ITreeAttribute? tree, out int index)
        {
            var milestones = Milestones(api);
            for (index = 0; index < milestones.Length; index++)
                if (!Claimed(tree, index) && milestones[index]["entries"].AsInt(0) > 0) return milestones[index];
            index = -1;
            return null;
        }
    }
}
