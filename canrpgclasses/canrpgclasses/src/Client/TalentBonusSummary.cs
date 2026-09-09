using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Client
{
    /// <summary>
    /// The talent window's "build bonuses" lines: totals each derived stat from talent ranks, tree mastery and
    /// gear affinities, mirroring <see cref="canrpgclasses.Core.EB.EBRpgStats"/>. Pure arithmetic over live
    /// client state, split out of the window so it can recompute on a tick rather than every frame.
    /// </summary>
    public static class TalentBonusSummary
    {
        // Declared before the tables below - static fields initialize in textual order, and the tables
        // reference these in their initializers.
        private static readonly string HunterRanged = StatKeys.SpellpowerFor(Core.Spells.SpellSchool.PhysicalRanged);
        private static readonly string PriestShadow = StatKeys.SpellpowerFor(Core.Spells.SpellSchool.Shadow);
        private static readonly string MageFire = StatKeys.SpellpowerFor(Core.Spells.SpellSchool.Fire);
        private static readonly string MageFrost = StatKeys.SpellpowerFor(Core.Spells.SpellSchool.Frost);
        private static readonly string MageArcane = StatKeys.SpellpowerFor(Core.Spells.SpellSchool.Arcane);
        private static readonly string ShamanNature = StatKeys.SpellpowerFor(Core.Spells.SpellSchool.Nature);

        // Which numeric talent feeds which summary stat, and the sign of its applied modifier (config holds the
        // magnitude). The per-rank value itself comes from BalanceConfig - single source of truth.
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
            ("hunter:beast_kinship",      "max_focus",             1f),
            ("hunter:light_footed",    StatKeys.WalkSpeed,            1f),
            ("hunter:hunters_hide",    StatKeys.MaxHealthExtraPoints,  1f),
            ("hunter:kindred_spirit",     StatKeys.MaxHealthExtraPoints,  1f),
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

        // Display order: stat key → lang label, and whether it's a percent (×100) or a flat number.
        private static readonly (string stat, string langKey, bool pct)[] SummaryRows =
        {
            (StatKeys.MeleeWeaponsDamage,   "ui-sum-melee",     true),
            (StatKeys.RangedWeaponsDamage,  "ui-sum-bow",       true),
            (StatKeys.SpellpowerHoly,      "ui-sum-holy",      true),
            (PriestShadow,           "ui-sum-shadow",    true),
            (MageFire,               "ui-sum-fire",      true),
            (MageFrost,              "ui-sum-frost",     true),
            (MageArcane,             "ui-sum-arcane",    true),
            (ShamanNature,           "ui-sum-nature",    true),
            (HunterRanged,           "ui-sum-ranged",    true),
            (StatKeys.HealingPower,        "ui-sum-healpower", true),
            (StatKeys.HealingEffectiveness,  "ui-sum-healing",   true),
            (StatKeys.WalkSpeed,            "ui-sum-speed",     true),
            (StatKeys.MaxHealthExtraPoints, "ui-sum-maxhp",     false),
            ("max_energy",           "ui-sum-maxenergy", false),
            ("max_mana",             "ui-sum-maxmana",   false),
            ("max_focus",            "ui-sum-maxfocus",  false),
            ("max_rage",             "ui-sum-maxrage",   false),
            (StatKeys.DamageReduction,      "ui-sum-dr",        true),
            (StatKeys.MagicResist,          "ui-sum-magicres",  true),
            (StatKeys.ResourceRegen,        "ui-sum-regen",     true),
            (StatKeys.HungerRate,           "ui-sum-hunger",    true),
            (StatKeys.AnimalSeekingRange,   "ui-sum-stealth",   true),
            (StatKeys.CooldownReduction,    "ui-sum-cdr",       true),
        };

        /// <summary>One line of the summary: what it is, the formatted value, and which way it moves the character
        /// (so the window can colour it without re-parsing the number back out of the string).</summary>
        public readonly struct Row
        {
            public readonly string Label;
            public readonly string Value;
            public readonly int Sign;

            public Row(string label, string value, int sign) { Label = label; Value = value; Sign = sign; }
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
                // Tree mastery: every point spent in a tree adds its bonus regardless of which specific talent it
                // went into - not covered by TalentStatTable, so without this the summary undercounts the real stat.
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

            // Attributes pay into the same stats, and the level curves now run through them - without this the
            // summary reads far too low.
            foreach (var def in Core.Attributes.RpgAttributes.All)
            {
                float points = def.EffectivePoints(player);
                if (Math.Abs(points) < 1e-4f) continue;
                foreach (var eff in def.EffectsFor(classId)) Add(eff.Stat, eff.Value(points));
            }

            var lines = new List<Row>();

            foreach (var (stat, langKey, pct) in SummaryRows)
            {
                if (!totals.TryGetValue(stat, out var v) || Math.Abs(v) < 1e-4f) continue;
                string val = pct ? Signed((int)Math.Round(v * 100f)) + "%" : Signed((int)Math.Round(v));
                lines.Add(new Row(Lang.Get("canrpgclasses:" + langKey), val, Math.Sign(v)));
            }
            if (lines.Count == 0) lines.Add(new Row(Lang.Get("canrpgclasses:ui-sum-none"), "", 0));

            // Effective spell power = levelMul × (1 + relevant stat fraction), as in EBRpgStats.GetSpellPower.
            float levelMul = 1f + Math.Max(0, level - 1) * BalanceConfig.Global("spellPowerPerLevel", 0.05f);
            totals.TryGetValue(StatKeys.MeleeWeaponsDamage, out var meleeFrac);
            totals.TryGetValue(StatKeys.SpellpowerHoly, out var holyFrac);
            float meleeSp = levelMul * (1f + Math.Max(-0.9f, meleeFrac));
            lines.Add(new Row(Lang.Get("canrpgclasses:ui-sum-sp-melee"), "×" + meleeSp.ToString("0.00"), 0));
            if (holyFrac > 1e-4f || classId == "paladin")
            {
                float holySp = levelMul * (1f + Math.Max(-0.9f, holyFrac));
                lines.Add(new Row(Lang.Get("canrpgclasses:ui-sum-sp-holy"), "×" + holySp.ToString("0.00"), 0));
            }

            return lines;
        }

        /// <summary>The class's intrinsic gear affinities with a live active/inactive marker, computed client-side
        /// from the held weapon + worn armor (the same <see cref="GearAffinity.Matches"/> the server uses).</summary>
        public static List<(string line, bool active)> GearAffinities(ICoreClientAPI capi, RpgClassDef? cls)
        {
            var result = new List<(string, bool)>();
            if (cls == null || cls.GearAffinities.Count == 0) return result;

            var plr = capi.World?.Player;
            var weapon = plr?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Collectible;
            var armorInv = plr?.InventoryManager?.GetOwnInventory("character");

            foreach (var aff in cls.GearAffinities)
                result.Add((aff.ResolvedDescription, aff.Matches(weapon, armorInv)));

            return result;
        }

        private static string Signed(int n) => SheetFormat.Signed(n);
    }
}
