using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace canrpgclasses.Hunter
{
    /// <summary>Pet looks are the variants of the hunterpet entity. Numbers come only from the look's family
    /// (<c>petFamily</c>: family, role, special, minTameLevel, hpMul, dmgMul, tricks); the look itself carries
    /// <c>petLook</c> (tameCodes, rareColors, hint). Adding a look is data only.</summary>
    public static class PetSpecies
    {
        public const string Default = "wolf";
        private const string Prefix = HunterPetSystem.PetEntityCode + "-";

        public static AssetLocation CodeFor(string look) => new(canrpgclassesModSystem.ModId, Prefix + look);

        public static EntityProperties? TypeFor(IWorldAccessor world, string look) => world.GetEntityType(CodeFor(look));

        /// <summary>Look of a pet entity, or null for anything that isn't one.</summary>
        public static string? Of(Entity? e) => LookOf(e?.Code);

        private static string? LookOf(AssetLocation? code)
        {
            if (code == null || code.Domain != canrpgclassesModSystem.ModId || !code.Path.StartsWith(Prefix)) return null;
            return code.Path.Substring(Prefix.Length);
        }

        public static JsonObject? Family(EntityProperties? type) => type?.Attributes?["petFamily"];
        public static JsonObject? Family(Entity pet) => Family(pet.Properties);
        public static JsonObject? Look(EntityProperties? type) => type?.Attributes?["petLook"];

        public static float HpMul(Entity pet) => Family(pet)?["hpMul"].AsFloat(1f) ?? 1f;
        public static float DmgMul(Entity pet) => Family(pet)?["dmgMul"].AsFloat(1f) ?? 1f;
        public static string Special(Entity pet) => Family(pet)?["special"].AsString("") ?? "";

        public static string FamilyOf(IWorldAccessor world, string look)
            => Family(TypeFor(world, look))?["family"].AsString(Default) ?? Default;

        public static int MinTameLevel(IWorldAccessor world, string look)
            => Family(TypeFor(world, look))?["minTameLevel"].AsInt(1) ?? 1;

        public static string Hint(IWorldAccessor world, string look)
            => Look(TypeFor(world, look))?["hint"].AsString("") ?? "";

        /// <summary>Every pet look, in the entity's variant order.</summary>
        public static List<string> AllLooks(IWorldAccessor world)
        {
            var list = new List<string>();
            foreach (var type in world.EntityTypes)
                if (LookOf(type.Code) is { } look) list.Add(look);
            return list;
        }

        // Wild code pattern → look, built once per set of entity types: ForWild runs on every creature spawn.
        private static List<EntityProperties>? cachedTypes;
        private static List<(AssetLocation Pattern, string Look)> wildPatterns = new();

        /// <summary>The look a wild creature tames into (first look whose tameCodes match), or null.</summary>
        public static string? ForWild(IWorldAccessor world, Entity wild)
        {
            if (wild?.Code == null || Of(wild) != null) return null;
            if (!ReferenceEquals(cachedTypes, world.EntityTypes)) BuildPatterns(world);
            foreach (var (pattern, look) in wildPatterns)
                if (WildcardUtil.Match(pattern, wild.Code)) return look;
            return null;
        }

        private static void BuildPatterns(IWorldAccessor world)
        {
            var patterns = new List<(AssetLocation, string)>();
            foreach (var type in world.EntityTypes)
            {
                if (LookOf(type.Code) is not { } look) continue;
                var codes = Look(type)?["tameCodes"].AsArray<string>();
                if (codes == null) continue;
                foreach (var c in codes) patterns.Add((new AssetLocation(c), look));
            }
            wildPatterns = patterns;
            cachedTypes = world.EntityTypes;
        }

        /// <summary>How many colours the look has: its base texture plus the alternates.</summary>
        public static int ColorCount(IWorldAccessor world, string look)
            => (TypeFor(world, look)?.Client?.TexturesAlternatesCount ?? 0) + 1;

        public static int[] RareColors(IWorldAccessor world, string look)
            => Look(TypeFor(world, look))?["rareColors"].AsArray<int>() ?? System.Array.Empty<int>();

        public static bool IsRareColor(IWorldAccessor world, string look, int color)
            => System.Array.IndexOf(RareColors(world, look), color) >= 0;

        /// <summary>Colours this pet may switch between: the common ones plus its own rare one, if it has one.</summary>
        public static int SelectableColors(IWorldAccessor world, string look, int ownRare)
        {
            int n = ColorCount(world, look), count = 0;
            for (int i = 0; i < n; i++) if (i == ownRare || !IsRareColor(world, look, i)) count++;
            return count;
        }

        /// <summary>A coat for a wild creature of this look: a rare one on a lucky roll, otherwise any common one.</summary>
        public static int RollColor(IWorldAccessor world, string look, float rareChance)
        {
            int n = ColorCount(world, look);
            var rare = new List<int>();
            var common = new List<int>();
            for (int i = 0; i < n; i++) (IsRareColor(world, look, i) ? rare : common).Add(i);
            if (rare.Count > 0 && world.Rand.NextDouble() < rareChance) return rare[world.Rand.Next(rare.Count)];
            return common.Count > 0 ? common[world.Rand.Next(common.Count)] : 0;
        }

        /// <summary>Pre-collection stables stored the species alone; these are the looks they were drawn as.</summary>
        public static string Migrate(string look) => look switch
        {
            "bear" => "bear-brown",
            "hyena" => "hyena-spotted-male",
            "fox" => "fox-red-male",
            _ => look
        };
    }
}
