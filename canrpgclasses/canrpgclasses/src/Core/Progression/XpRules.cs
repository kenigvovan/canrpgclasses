using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Core.Progression
{
    /// <summary>Kill XP: maxhealth * <c>xpPerHealth</c>, times a per-entity multiplier from <c>config/xp.json</c>
    /// and a level-difference falloff for mobs that carry a level. Server only.</summary>
    public static class XpRules
    {
        private const string AssetFile = "xp.json";

        /// <summary>Server's own table on top of the shipped one, written with the shipped values on first start.</summary>
        private const string OverridesFile = "canrpgclasses-xp.json";

        // canrpgmobs keeps its identity in this watched tree; read by name so this mod needs no reference to it.
        private const string MobTree = "canrpgmobs";
        private const string MobLevelKey = "lvl";

        private static volatile List<(AssetLocation Pattern, float Multiplier)> multipliers = new();

        /// <summary>Levels a mob may sit below the killer before its XP starts to fall.</summary>
        public static int LevelGrace => (int)BalanceConfig.Global("xpLevelGrace", 2f);

        /// <summary>XP lost per level beyond the grace, as a fraction of the full amount.</summary>
        public static float LevelFalloffPerLevel => BalanceConfig.Global("xpLevelFalloffPerLevel", 0.1f);

        /// <summary>The floor of that falloff: a trivial mob is never worth less than this fraction.</summary>
        public static float LevelMinFactor => BalanceConfig.Global("xpLevelMinFactor", 0.1f);

        public static void Load(ICoreAPI api)
        {
            var merged = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            var asset = api.Assets.TryGet(new AssetLocation(canrpgclassesModSystem.ModId, "config/" + AssetFile));
            if (asset != null)
            {
                try { Merge(merged, asset.ToObject<XpFileModel>()); }
                catch (Exception e) { api.Logger.Warning("[canrpgclasses] bad xp config {0}: {1}", asset.Location, e.Message); }
            }

            if (api.Side == EnumAppSide.Server)
            {
                try
                {
                    var overrides = api.LoadModConfig<XpFileModel>(OverridesFile);

                    // Seed with the shipped values so admins edit a file that already has the shape. A broken file
                    // throws into the catch and is never overwritten.
                    if (overrides == null)
                    {
                        overrides = new XpFileModel
                        {
                            _comment = "XP multipliers per entity code, on top of maxhealth * xpPerHealth. Wildcards work "
                                     + "(game:locust-*); the longest matching pattern wins; unmatched mobs are worth 1. "
                                     + "An entry here replaces the shipped one of the same pattern. Read at startup and "
                                     + "on content reload.",
                            multipliers = new Dictionary<string, float>(merged)
                        };
                        api.StoreModConfig(overrides, OverridesFile);
                    }

                    Merge(merged, overrides);
                }
                catch (Exception e) { api.Logger.Warning("[canrpgclasses] bad {0}: {1}", OverridesFile, e.Message); }
            }

            var next = new List<(AssetLocation, float)>();
            foreach (var kv in merged) next.Add((new AssetLocation(kv.Key), Math.Max(0f, kv.Value)));
            multipliers = next;

            api.Logger.Notification("[canrpgclasses] xp config: {0} multiplier pattern(s)", next.Count);
        }

        private static void Merge(Dictionary<string, float> into, XpFileModel? file)
        {
            if (file?.multipliers == null) return;
            foreach (var kv in file.multipliers)
                if (!string.IsNullOrWhiteSpace(kv.Key)) into[kv.Key.Trim()] = kv.Value;
        }

        /// <summary>The XP one kill of <paramref name="dead"/> gives a killer of <paramref name="killerLevel"/>.</summary>
        public static long XpFor(Entity dead, int killerLevel)
        {
            float maxHp = dead.WatchedAttributes.GetTreeAttribute("health")?.GetFloat("maxhealth", 1f) ?? 1f;
            float xp = maxHp * EBProgression.XpPerHealth * Multiplier(dead);

            int? mobLevel = MobLevel(dead);
            if (mobLevel.HasValue) xp *= LevelFactor(killerLevel, mobLevel.Value);

            return (long)xp;
        }

        /// <summary>Multiplier of the longest matching pattern, regardless of file order; 1 when none matches.</summary>
        public static float Multiplier(Entity entity)
        {
            var code = entity.Code;
            if (code == null) return 1f;

            float result = 1f;
            int best = -1;
            foreach (var (pattern, multiplier) in multipliers)
            {
                int length = pattern.Domain.Length + pattern.Path.Length;
                if (length <= best || !WildcardUtil.Match(pattern, code)) continue;

                best = length;
                result = multiplier;
            }
            return result;
        }

        /// <summary>The mob's level, when something gave it one.</summary>
        public static int? MobLevel(Entity entity)
        {
            var tree = entity.WatchedAttributes.GetTreeAttribute(MobTree);
            if (tree == null || !tree.HasAttribute(MobLevelKey)) return null;
            return tree.GetInt(MobLevelKey);
        }

        /// <summary>1 within <see cref="LevelGrace"/>, then falls per level to <see cref="LevelMinFactor"/>. Stronger mobs
        /// get no bonus here; their extra health already pays more.</summary>
        public static float LevelFactor(int killerLevel, int mobLevel)
        {
            int below = killerLevel - mobLevel - Math.Max(0, LevelGrace);
            if (below <= 0) return 1f;

            float min = Math.Clamp(LevelMinFactor, 0f, 1f);
            return Math.Max(min, 1f - below * Math.Max(0f, LevelFalloffPerLevel));
        }

        private class XpFileModel
        {
            public string? _comment { get; set; }
            public Dictionary<string, float>? multipliers { get; set; }
        }
    }
}
