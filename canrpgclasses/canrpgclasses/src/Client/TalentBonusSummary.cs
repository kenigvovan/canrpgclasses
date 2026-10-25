using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Talent window "build bonuses": totals derived stats from talents, tree mastery and gear affinities,
    /// mirroring <see cref="canrpgclasses.Core.EB.EBRpgStats"/>. Recomputed on a tick, not per frame.
    /// </summary>
    public static class TalentBonusSummary
    {
        // Must precede the tables below: static fields initialize in textual order.
        private static readonly string HunterRanged = StatKeys.SpellpowerFor(Core.Spells.SpellSchool.PhysicalRanged);
        private static readonly string PriestShadow = StatKeys.SpellpowerFor(Core.Spells.SpellSchool.Shadow);
        private static readonly string MageFire = StatKeys.SpellpowerFor(Core.Spells.SpellSchool.Fire);
        private static readonly string MageFrost = StatKeys.SpellpowerFor(Core.Spells.SpellSchool.Frost);
        private static readonly string MageArcane = StatKeys.SpellpowerFor(Core.Spells.SpellSchool.Arcane);
        private static readonly string ShamanNature = StatKeys.SpellpowerFor(Core.Spells.SpellSchool.Nature);

        // Talent -> summary stat and modifier sign; per-rank magnitude comes from BalanceConfig.
        private static readonly (string id, string stat, float sign)[] TalentStatTable =
        {
            ("rogue:nimble",           StatKeys.WalkSpeed,             1f),
            ("rogue:quiet_steps",      StatKeys.AnimalSeekingRange,   -1f),
            ("rogue:exposed_flesh",    StatKeys.MeleeWeaponsDamage,    1f),
            ("rogue:blade_master",     StatKeys.MeleeWeaponsDamage,    1f),
            ("rogue:hardy",            StatKeys.HungerRate,           -1f),
            ("rogue:fast_mending",   StatKeys.HealingEffectiveness,   1f),
            ("rogue:toughness",        StatKeys.MaxHealthExtraPoints,  1f),
            ("rogue:vitality",         "max_energy",            1f),
            ("paladin:devout_light",  StatKeys.SpellpowerHoly,       1f),
            ("paladin:light_infusion", StatKeys.SpellpowerHoly,       1f),
            ("paladin:zeal",           StatKeys.SpellpowerHoly,       1f),
            ("paladin:benediction",    StatKeys.HealingEffectiveness,   1f),
            ("paladin:zealot",       StatKeys.MeleeWeaponsDamage,    1f),
            ("paladin:vengeance",      StatKeys.MeleeWeaponsDamage,    1f),
            ("paladin:toughness",      StatKeys.MaxHealthExtraPoints,  1f),
            ("paladin:guardian",       StatKeys.MaxHealthExtraPoints,  1f),
            ("paladin:faithful_ward",  StatKeys.MagicResist,          1f),
            ("paladin:radiant_return",   "max_mana",              1f),
            ("rogue:nimble_fingers",          StatKeys.CooldownReduction,  1f),
            ("rogue:shadow_adept",   StatKeys.WalkSpeed,          1f),
            ("paladin:swift_light",       StatKeys.CooldownReduction,  1f),
            ("paladin:safe_ground",         StatKeys.MaxHealthExtraPoints, 1f),
            ("paladin:hallowed_wrath",  StatKeys.SpellpowerHoly,    1f),
            ("paladin:zealots_momentum",StatKeys.WalkSpeed,          1f),
            ("hunter:bow_expertise",   StatKeys.RangedWeaponsDamage,   1f),
            ("hunter:deadeye",         HunterRanged,                  1f), // approx: shown as ranged power
            ("hunter:light_footed",    StatKeys.WalkSpeed,            1f),
            ("hunter:hunters_hide",    StatKeys.MaxHealthExtraPoints,  1f),
            ("hunter:natural_recovery",StatKeys.HealingEffectiveness,   1f),
            ("warrior:weapon_expertise", StatKeys.MeleeWeaponsDamage,   1f),
            ("warrior:fleet_of_foot",    StatKeys.WalkSpeed,            1f),
            ("warrior:toughness",        StatKeys.MaxHealthExtraPoints, 1f),
            ("warrior:ironhide",        StatKeys.DamageReduction,      1f),
            ("warrior:rage_reservoir",   "max_rage",              1f),
            ("priest:dual_discipline",   StatKeys.SpellpowerHoly,       1f),
            ("priest:light_specialization",StatKeys.SpellpowerHoly,       1f),
            ("priest:devout_healing",  StatKeys.HealingPower,         1f),
            ("priest:nimble_mind",     "max_mana",              1f),
            ("priest:steady_will",       StatKeys.DamageReduction,      1f),
            ("priest:kindled_hope",       StatKeys.HealingEffectiveness, 1f),
            ("priest:reach_of_light",         StatKeys.CooldownReduction,    1f),
            ("priest:bulwark",              StatKeys.MaxHealthExtraPoints, 1f),
            ("priest:deeper_shadow",           PriestShadow,                  1f),
            ("priest:meditation",         StatKeys.ResourceRegen,        1f),
            ("mage:flame_mastery",           MageFire,                      1f),
            ("mage:frost_power",          MageFrost,                     1f),
            ("mage:mystic_focus",         MageArcane,                    1f),
            ("mage:insight",              "max_mana",              1f),
            ("mage:mystic_subtlety",      StatKeys.CooldownReduction,    1f),
            ("mage:mystic_meditation",    StatKeys.ResourceRegen,        1f),
            ("shaman:concussion",          ShamanNature,                  1f),
            ("shaman:storm_attunement",     "max_mana",              1f),
            ("shaman:elemental_warding",   StatKeys.MaxHealthExtraPoints, 1f),
            ("shaman:elemental_focus",     StatKeys.CooldownReduction,    1f),
            ("shaman:thundering_strikes",  StatKeys.MeleeWeaponsDamage,   1f),
            ("shaman:ancestral_knowledge", "max_mana",              1f),
            ("shaman:toughness",           StatKeys.MaxHealthExtraPoints, 1f),
            ("shaman:mental_quickness",    StatKeys.CooldownReduction,    1f),
            ("shaman:purification",        StatKeys.HealingPower,         1f),
            ("shaman:totemic_focus",       "max_mana",              1f),
            ("shaman:nature_warding",      StatKeys.MaxHealthExtraPoints, 1f),
            ("shaman:tidal_mastery",       StatKeys.CooldownReduction,    1f),
            ("shaman:water_meditation",    StatKeys.ResourceRegen,        1f),
        };

        private const string DamageGroup = "ui-sum-g-damage";

        // Display order by group: stat key, lang label, percent (x100) or flat.
        private static readonly (string group, (string stat, string langKey, bool pct)[] rows)[] SummaryGroups =
        {
            ("ui-sum-g-survival", new[]
            {
                (StatKeys.MaxHealthExtraPoints, "ui-sum-maxhp",     false),
                (StatKeys.DamageReduction,      "ui-sum-dr",        true),
                (StatKeys.MagicResist,          "ui-sum-magicres",  true),
                (StatKeys.HealingEffectiveness, "ui-sum-healing",   true),
            }),
            ("ui-sum-g-resources", new[]
            {
                ("max_energy",                  "ui-sum-maxenergy", false),
                ("max_mana",                    "ui-sum-maxmana",   false),
                ("max_focus",                   "ui-sum-maxfocus",  false),
                ("max_rage",                    "ui-sum-maxrage",   false),
                (StatKeys.ResourceRegen,        "ui-sum-regen",     true),
                (StatKeys.CooldownReduction,    "ui-sum-cdr",       true),
            }),
            (DamageGroup, new[]
            {
                (StatKeys.MeleeWeaponsDamage,   "ui-sum-melee",     true),
                (StatKeys.RangedWeaponsDamage,  "ui-sum-bow",       true),
                (HunterRanged,                  "ui-sum-ranged",    true),
                (StatKeys.SpellpowerHoly,       "ui-sum-holy",      true),
                (PriestShadow,                  "ui-sum-shadow",    true),
                (MageFire,                      "ui-sum-fire",      true),
                (MageFrost,                     "ui-sum-frost",     true),
                (MageArcane,                    "ui-sum-arcane",    true),
                (ShamanNature,                  "ui-sum-nature",    true),
            }),
            ("ui-sum-g-healing", new[]
            {
                (StatKeys.HealingPower,         "ui-sum-healpower", true),
            }),
            ("ui-sum-g-utility", new[]
            {
                (StatKeys.WalkSpeed,            "ui-sum-speed",     true),
                (StatKeys.HungerRate,           "ui-sum-hunger",    true),
                (StatKeys.AnimalSeekingRange,   "ui-sum-stealth",   true),
            }),
        };

        // A drop is the good direction for these.
        private static readonly HashSet<string> LowerIsBetter = new() { StatKeys.HungerRate, StatKeys.AnimalSeekingRange };

        private static readonly Dictionary<string, SpellSchool> StatSchool = new()
        {
            [StatKeys.MeleeWeaponsDamage] = SpellSchool.PhysicalMelee,
            [StatKeys.RangedWeaponsDamage] = SpellSchool.PhysicalRanged,
            [HunterRanged] = SpellSchool.PhysicalRanged,
            [StatKeys.SpellpowerHoly] = SpellSchool.Holy,
            [PriestShadow] = SpellSchool.Shadow,
            [MageFire] = SpellSchool.Fire,
            [MageFrost] = SpellSchool.Frost,
            [MageArcane] = SpellSchool.Arcane,
            [ShamanNature] = SpellSchool.Nature,
        };

        /// <summary>One summary line; the direction lets the window colour it without re-parsing.</summary>
        public readonly struct Row
        {
            public readonly string Label;
            public readonly string Value;
            public readonly int Sign;
            /// <summary>School tint for the label; null = default label colour.</summary>
            public readonly string? LabelHex;
            /// <summary>Resulting spell power multiplier, shown dimmed after the value.</summary>
            public readonly string Extra;
            public readonly bool IsHeader;

            public Row(string label, string value, int sign, string? labelHex = null, string extra = "", bool isHeader = false)
            {
                Label = label; Value = value; Sign = sign; LabelHex = labelHex; Extra = extra; IsHeader = isHeader;
            }
        }

        /// <summary>The summary block as label/value rows, already localised.</summary>
        public static List<Row> Build(ICoreClientAPI capi, Entity player, canrpgclassesModSystem mod,
            string classId, RpgClassDef? cls, int level)
        {
            var totals = new Dictionary<string, float>();
            void Add(string stat, float v) { totals.TryGetValue(stat, out var c); totals[stat] = c + v; }

            foreach (var (id, stat, sign) in TalentStatTable)
            {
                if (!id.StartsWith(classId + ":", StringComparison.Ordinal)) continue;
                int rank = TalentState.Rank(player, id);
                if (rank <= 0) continue;
                Add(stat, sign * BalanceConfig.Talent(id).F("perRank", 0f) * rank);
            }

            if (cls != null)
            {
                // Tree mastery: every point in a tree adds its bonus; not covered by TalentStatTable.
                foreach (var m in cls.TreeMasteries)
                {
                    int pts = TalentState.PointsInTree(player, mod.Talents, m.TreeIndex);
                    if (pts > 0) Add(m.Stat, pts * m.PerPoint);
                }

                var plr = capi.World?.Player;
                var weapon = plr?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Collectible;
                var armorInv = plr?.InventoryManager?.GetOwnInventory("character");
                foreach (var aff in cls.GearAffinities)
                    if (aff.Matches(weapon, armorInv))
                        foreach (var (stat, value) in aff.Modifiers)
                            Add(stat, value);

                // HP per level (see EBTalents.ApplyStatTalents) - same "level - 1" convention as spell power's levelMul.
                if (cls.HpPerLevel > 0f) Add(StatKeys.MaxHealthExtraPoints, cls.HpPerLevel * Math.Max(0, level - 1));
            }

            // Attributes pay into the same stats (level curves run through them).
            foreach (var def in Core.Attributes.RpgAttributes.All)
            {
                float points = def.EffectivePoints(player);
                if (Math.Abs(points) < 1e-4f) continue;
                foreach (var eff in def.EffectsFor(classId)) Add(eff.Stat, eff.Value(points));
            }

            var lines = new List<Row>();
            bool any = false;
            float levelMul = 1f + Math.Max(0, level - 1) * BalanceConfig.Global("spellPowerPerLevel", 0.05f);

            foreach (var (group, rows) in SummaryGroups)
            {
                var block = new List<Row>();
                if (group == DamageGroup)
                    block.Add(new Row(Lang.Get("canrpgclasses:ui-sum-levelmul"), "×" + levelMul.ToString("0.00"), 0));

                foreach (var (stat, langKey, pct) in rows)
                {
                    if (!totals.TryGetValue(stat, out var v) || Math.Abs(v) < 1e-4f) continue;
                    any = true;
                    string val = pct ? Signed((int)Math.Round(v * 100f)) + "%" : Signed((int)Math.Round(v));
                    int sign = Math.Sign(v) * (LowerIsBetter.Contains(stat) ? -1 : 1);
                    string? hex = StatSchool.TryGetValue(stat, out var school) ? SchoolPalette.Hex(school) : null;
                    float? mul = SpellPowerMul(stat, totals, levelMul);
                    block.Add(new Row(Lang.Get("canrpgclasses:" + langKey), val, sign, hex,
                        mul.HasValue ? "×" + mul.Value.ToString("0.00") : ""));
                }

                if (block.Count == 0) continue;
                lines.Add(new Row(Lang.Get("canrpgclasses:" + group), "", 0, isHeader: true));
                lines.AddRange(block);
            }
            if (!any) lines.Insert(0, new Row(Lang.Get("canrpgclasses:ui-sum-none"), "", 0));

            return lines;
        }

        /// <summary>Resulting spell power for a school stat, as in EBRpgStats.GetSpellPower; null for non-school stats.</summary>
        private static float? SpellPowerMul(string stat, Dictionary<string, float> totals, float levelMul)
        {
            float F(string s) => totals.TryGetValue(s, out var v) ? v : 0f;
            if (stat == StatKeys.MeleeWeaponsDamage) return levelMul * (1f + Math.Max(-0.9f, F(stat)));
            // Bow damage and ranged spell power stack for physical ranged spells; shown once, on the bow row.
            if (stat == StatKeys.RangedWeaponsDamage)
                return levelMul * (1f + Math.Max(-0.9f, F(stat))) * Math.Max(0.1f, 1f + F(HunterRanged));
            if (stat == HunterRanged) return null;
            return StatSchool.ContainsKey(stat) ? levelMul * Math.Max(0.1f, 1f + F(stat)) : null;
        }

        /// <summary>Class gear affinities with a live active marker, via the same <see cref="GearAffinity.Matches"/>
        /// the server uses.</summary>
        public static List<(string line, bool active, string tag)> GearAffinities(ICoreClientAPI capi, RpgClassDef? cls)
        {
            var result = new List<(string, bool, string)>();
            if (cls == null || cls.GearAffinities.Count == 0) return result;

            var plr = capi.World?.Player;
            var weapon = plr?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Collectible;
            var armorInv = plr?.InventoryManager?.GetOwnInventory("character");

            foreach (var aff in cls.GearAffinities)
                result.Add((aff.ResolvedDescription, aff.Matches(weapon, armorInv), ConditionTag(aff)));

            return result;
        }

        /// <summary>Short label of what the affinity checks: weapon, armor, or both.</summary>
        private static string ConditionTag(GearAffinity aff)
        {
            var parts = new List<string>();
            if ((aff.WeaponTools?.Length ?? 0) > 0 || !string.IsNullOrEmpty(aff.WeaponCodeContains))
                parts.Add(Lang.Get("canrpgclasses:ui-aff-tag-weapon"));
            if ((aff.ArmorCodeContains?.Length ?? 0) > 0)
                parts.Add(Lang.Get(aff.ArmorAbsent ? "canrpgclasses:ui-aff-tag-noarmor" : "canrpgclasses:ui-aff-tag-armor"));
            return parts.Count > 0 ? string.Join(" + ", parts) : Lang.Get("canrpgclasses:ui-aff-tag-always");
        }

        private static string Signed(int n) => SheetFormat.Signed(n);
    }
}
