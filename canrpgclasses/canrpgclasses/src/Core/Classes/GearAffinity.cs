using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace canrpgclasses.Core.Classes
{
    /// <summary>
    /// A class-wide passive that applies <see cref="Modifiers"/> while the equipped gear matches. All set
    /// conditions must hold; an unset one is ignored.
    /// </summary>
    public class GearAffinity
    {
        /// <summary>Unique key (per class) - used as the stat-modifier code so it can be set/cleared on its own.</summary>
        public string Key = "";
        public string Description = "";

        /// <summary>Values for the description template, from the same config numbers the modifiers use.</summary>
        public object[]? DescArgs;

        /// <summary>The localized description with <see cref="DescArgs"/> filled in - use this for display.</summary>
        public string ResolvedDescription
        {
            get
            {
                var t = canrpgclasses.Core.LangText.Get("affinity-" + Key, Description);
                if (string.IsNullOrEmpty(t)) return "";
                return DescArgs != null && DescArgs.Length > 0 ? string.Format(t!, DescArgs) : t!;
            }
        }

        /// <summary>Active-hand item must be one of these tool types.</summary>
        public EnumTool[]? WeaponTools;
        /// <summary>Active-hand item code (path) must contain this substring (case-insensitive).</summary>
        public string? WeaponCodeContains;

        /// <summary>Armor condition: a worn piece whose code contains any of these substrings.</summary>
        public string[]? ArmorCodeContains;
        /// <summary>Inverts the armor condition: the affinity applies only while NO worn piece matches.</summary>
        public bool ArmorAbsent;

        /// <summary>Stat modifiers applied while matching (stat key → blend value).</summary>
        public List<(string Stat, float Value)> Modifiers = new();

        /// <summary>Affinities every classed player gets on top of their own class list, like metal armor giving
        /// magic resistance. Rebuilt from config on each load, so the numbers stay live.</summary>
        public static readonly List<GearAffinity> Global = new();

        /// <summary>(Re)builds <see cref="Global"/> from the current balance config. Call after BalanceConfig.Load.</summary>
        public static void RebuildGlobal()
        {
            Global.Clear();
            string[] heavy = { "plate", "chain", "scale", "brigandine", "lamellar" };
            float ward = canrpgclasses.Core.Config.BalanceConfig.Global("armorMagicResist", 0.08f);
            Global.Add(new GearAffinity
            {
                Key = "armor_ward",
                Description = "+{0}% magic resistance in metal armor",
                DescArgs = new object[] { (int)Math.Round(ward * 100f) },
                ArmorCodeContains = heavy,
                Modifiers = { (canrpgclasses.Core.StatKeys.MagicResist, ward) }
            });
        }

        /// <summary>Whether the given equipped gear satisfies all set conditions. Side-agnostic - used both by
        /// the server (to apply stats) and the client (to show which affinities are active).</summary>
        public bool Matches(CollectibleObject? weapon, IInventory? armorInv)
        {
            if (WeaponTools != null && WeaponTools.Length > 0)
            {
                if (weapon?.Tool == null || !WeaponTools.Contains(weapon.Tool.Value)) return false;
            }
            if (!string.IsNullOrEmpty(WeaponCodeContains))
            {
                string? path = weapon?.Code?.Path;
                if (path == null || path.IndexOf(WeaponCodeContains!, StringComparison.OrdinalIgnoreCase) < 0) return false;
            }
            if (ArmorCodeContains != null && ArmorCodeContains.Length > 0)
            {
                bool worn = ArmorWorn(armorInv, ArmorCodeContains);
                if (ArmorAbsent ? worn : !worn) return false;
            }
            return true;
        }

        private static bool ArmorWorn(IInventory? armorInv, string[] parts)
        {
            if (armorInv == null) return false;
            foreach (var slot in armorInv)
            {
                string? path = slot?.Itemstack?.Collectible?.Code?.Path;
                if (path == null) continue;
                foreach (var p in parts)
                    if (path.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }
    }
}
