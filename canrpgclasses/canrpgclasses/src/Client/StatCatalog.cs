using System;
using System.Collections.Generic;
using System.Reflection;
using canrpgclasses.Core;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Editor dropdown stat keys, read from <see cref="StatKeys"/> by reflection. Per-spell keys have no fixed
    /// set, hence the free-text option.
    /// </summary>
    public static class StatCatalog
    {
        /// <summary>Dropdown entry standing for "type the key myself".</summary>
        public const string Custom = " custom";

        private static List<(string Key, string Label)>? cached;

        public static IReadOnlyList<(string Key, string Label)> Entries => cached ??= Build();

        /// <summary>Drops the cache after an attribute config (re)load, so the dropdown lists what exists now.</summary>
        public static void Invalidate() => cached = null;

        private static List<(string, string)> Build()
        {
            var list = new List<(string, string)>();
            var seen = new HashSet<string>();

            foreach (var f in typeof(StatKeys).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.FieldType != typeof(string)) continue;
                if (f.GetValue(null) is not string key || key.Length == 0) continue;
                if (!seen.Add(key)) continue;
                list.Add((key, $"{Humanize(f.Name)}  ({key})"));
            }

            // Spell power keys are generated per school.
            foreach (SpellSchool school in Enum.GetValues(typeof(SpellSchool)))
            {
                string key = StatKeys.SpellpowerFor(school);
                if (seen.Add(key)) list.Add((key, $"Spell power: {school}  ({key})"));
            }

            // Attributes are plain stat keys too.
            foreach (var attr in Core.Attributes.RpgAttributes.All)
                if (seen.Add(attr.Id)) list.Add((attr.Id, $"{attr.DisplayName}  ({attr.Id})"));

            list.Sort((a, b) => string.Compare(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        /// <summary>Readable stat name outside the dropdown; a <c>stat-&lt;key&gt;</c> lang entry wins.</summary>
        public static string Label(string key)
        {
            string lang = "canrpgclasses:stat-" + key;
            if (Vintagestory.API.Config.Lang.HasTranslation(lang, true, false))
                return Vintagestory.API.Config.Lang.Get(lang);

            foreach (var (k, label) in Entries)
                if (k == key)
                {
                    int at = label.IndexOf("  (", StringComparison.Ordinal);
                    return at > 0 ? label.Substring(0, at) : label;
                }
            return key;
        }

        /// <summary>"MaxHealthExtraPoints" -> "Max health extra points".</summary>
        private static string Humanize(string name)
        {
            var sb = new System.Text.StringBuilder(name.Length + 8);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && char.IsUpper(c)) { sb.Append(' '); sb.Append(char.ToLowerInvariant(c)); }
                else sb.Append(i == 0 ? char.ToUpperInvariant(c) : c);
            }
            return sb.ToString();
        }
    }
}
