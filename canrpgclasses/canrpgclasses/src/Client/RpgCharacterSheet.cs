using System;
using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using canrpgclasses.Core;
using canrpgclasses.Core.HarmonyPatches;
using canrpgclasses.Core.Progression;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Client
{
    /// <summary>How a value reads: a plain label, a bonus, a penalty, or a stat sitting at its baseline.</summary>
    public enum SheetTone { Label, Good, Bad, Neutral, Inactive }

    /// <summary>One line of the sheet. <see cref="LabelHex"/> overrides the label colour (schools use their own).</summary>
    public readonly struct SheetRow
    {
        public readonly string Label;
        public readonly string Value;
        public readonly SheetTone Tone;
        public readonly int Indent;
        public readonly string? LabelHex;

        public SheetRow(string label, string value, SheetTone tone, int indent = 0, string? labelHex = null)
        {
            Label = label; Value = value; Tone = tone; Indent = indent; LabelHex = labelHex;
        }
    }

    public sealed class SheetSection
    {
        public string Title = "";
        public readonly List<SheetRow> Rows = new();
    }

    public readonly struct SheetHeader
    {
        public readonly string ClassName;
        public readonly int Level;
        public readonly int AvailPoints;
        public readonly int TotalPoints;
        public readonly long XpInto;
        public readonly long XpNext;

        public SheetHeader(string className, int level, int avail, int total, long xpInto, long xpNext)
        {
            ClassName = className; Level = level; AvailPoints = avail; TotalPoints = total;
            XpInto = xpInto; XpNext = xpNext;
        }

        public bool MaxLevel => XpNext <= 0;
    }

    /// <summary>The character sheet as data - class, level, attributes, derived stats - with no formatting of its
    /// own: <see cref="RpgSheetVtml"/> marks it up and <see cref="RpgSheetTab"/> draws it.</summary>
    public static class RpgCharacterSheet
    {
        /// <summary>Sections, in display order. Fixed set, so the tab can build its elements once.</summary>
        public const int SectionCount = 4;

        // Enum.GetValues allocates a fresh array per call; the sheet loops schools twice per refresh - cache once.
        private static readonly SpellSchool[] Schools = Enum.GetValues<SpellSchool>();

        public static SheetHeader BuildHeader(Entity player)
        {
            var mod = canrpgclassesModSystem.ClientInstance;
            var prog = player.GetBehavior<EBProgression>();

            int total = prog?.TotalTalentPoints ?? 0;
            string classId = TalentState.CurrentClass(player);

            return new SheetHeader(
                mod?.Classes.Get(classId)?.DisplayName ?? classId,
                prog?.Level ?? 1,
                TalentState.AvailablePoints(player, total),
                total,
                prog?.XpIntoLevel ?? 0,
                prog?.XpForNextLevel ?? 0);
        }

        /// <summary>Fills a reusable buffer of <see cref="SectionCount"/> sections - the tab keeps one around, so a
        /// refresh twice a second doesn't allocate four lists every time.</summary>
        public static void BuildSections(Entity player, List<SheetSection> into)
        {
            while (into.Count < SectionCount) into.Add(new SheetSection());
            foreach (var s in into) s.Rows.Clear();

            string classId = TalentState.CurrentClass(player);

            BuildAttributes(player, classId, into[0]);
            BuildDefense(player, into[1]);
            BuildOffense(player, into[2]);
            BuildUtility(player, into[3]);
        }

        private static void BuildAttributes(Entity player, string classId, SheetSection sec)
        {
            sec.Title = Lang.Get("canrpgclasses:sheet-sec-attributes");

            foreach (var def in Core.Attributes.RpgAttributes.All)
            {
                float points = def.Points(player);
                float effective = def.EffectivePoints(player);
                // Past a softcap the payouts run off a smaller number than the sheet shows - say so, or the bonus
                // simply reads as wrong.
                string shown = Math.Abs(effective - points) < 0.005f
                    ? Mult(points)
                    : Mult(points) + Lang.Get("canrpgclasses:sheet-attribute-softcap", Mult(effective));

                sec.Rows.Add(new SheetRow(def.DisplayName, shown,
                    points > 0.005f ? SheetTone.Neutral : SheetTone.Inactive, 0, VtmlText.Accent));

                // What the points actually pay for - an attribute nobody can read the effect of is just a number.
                foreach (var (label, value, flat) in Payouts(def, classId, effective))
                    sec.Rows.Add(new SheetRow(label,
                        flat ? Signed(value) : Signed(value * 100f) + "%",
                        value >= 0f ? SheetTone.Good : SheetTone.Bad, 1));
            }
        }

        private static void BuildDefense(Entity player, SheetSection sec)
        {
            sec.Title = Lang.Get("canrpgclasses:sheet-sec-defense");

            float baseResist = StunPatches.InnateMagicResist(player) + player.ReductionStat(StatKeys.MagicResist);
            sec.Rows.Add(new SheetRow(Lang.Get("canrpgclasses:sheet-resist-base"), Pct(baseResist) + "%",
                ToneForFraction(baseResist)));

            AddPerSchool(player, sec, d => d.ResistStat, indent: 1);

            float dr = player.ReductionStat(StatKeys.DamageReduction);
            sec.Rows.Add(new SheetRow(Lang.Get("canrpgclasses:sheet-dr"), Pct(dr) + "%", ToneForFraction(dr)));
        }

        private static void BuildOffense(Entity player, SheetSection sec)
        {
            sec.Title = Lang.Get("canrpgclasses:sheet-sec-offense");

            float pen = player.ReductionStat(StatKeys.MagicPen);
            sec.Rows.Add(new SheetRow(Lang.Get("canrpgclasses:sheet-pen"), Pct(pen) + "%", ToneForFraction(pen)));

            // Per-school penetration (e.g. priest's shadow piercing) only where a talent grants it: unlike resists
            // and spell power, it is the rare case and a row of seven zeroes would say nothing.
            foreach (SpellSchool s in Schools)
            {
                var def = DamageSchools.For(s);
                if (def.Physical) continue;
                float v = player.ReductionStat(def.PenStat);
                if (v <= 0.0005f) continue;
                sec.Rows.Add(new SheetRow(Lang.Get("canrpgclasses:sheet-pen-school", SchoolName(s)),
                    Pct(v) + "%", SheetTone.Good, 1, SchoolPalette.Hex(s)));
            }

            sec.Rows.Add(new SheetRow(Lang.Get("canrpgclasses:sheet-spellpower-header"), "", SheetTone.Label));
            foreach (SpellSchool s in Schools)
            {
                if (DamageSchools.For(s).Physical) continue;
                float sp = player.Stats.GetBlended(StatKeys.SpellpowerFor(s));
                sec.Rows.Add(new SheetRow(SchoolName(s), "x" + Mult(sp), ToneFor(sp, 1f), 1, SchoolPalette.Hex(s)));
            }
        }

        private static void BuildUtility(Entity player, SheetSection sec)
        {
            sec.Title = Lang.Get("canrpgclasses:sheet-sec-utility");

            float cdr = player.ReductionStat(StatKeys.CooldownReduction);
            sec.Rows.Add(new SheetRow(Lang.Get("canrpgclasses:sheet-cdr"), Pct(cdr) + "%", ToneForFraction(cdr)));

            float regen = player.Stats.GetBlended(StatKeys.ResourceRegen);
            sec.Rows.Add(new SheetRow(Lang.Get("canrpgclasses:sheet-regen"), "x" + Mult(regen), ToneFor(regen, 1f)));

            float healing = player.Stats.GetBlended(StatKeys.HealingPower);
            sec.Rows.Add(new SheetRow(Lang.Get("canrpgclasses:sheet-healing"), "x" + Mult(healing), ToneFor(healing, 1f)));
        }

        /// <summary>A row per magical school for a 1.0-based reduction stat, zeroes included: the sheet scrolls, and
        /// a school missing from the list reads as "unknown" rather than "none".</summary>
        private static void AddPerSchool(Entity player, SheetSection sec,
                                         Func<DamageSchools.Def, string> stat, int indent)
        {
            foreach (SpellSchool s in Schools)
            {
                var def = DamageSchools.For(s);
                if (def.Physical) continue;
                float v = player.ReductionStat(stat(def));
                sec.Rows.Add(new SheetRow(SchoolName(s), Pct(v) + "%",
                    ToneForFraction(v), indent, SchoolPalette.Hex(s)));
            }
        }

        /// <summary>One entry per payout, except that a whole school family (spell power, resists, penetration)
        /// collapses into one while its schools pay the same - which is the usual case.</summary>
        private static IEnumerable<(string Label, float Value, bool Flat)> Payouts(
            Core.Attributes.AttributeDef def, string classId, float points)
        {
            var families = new List<(string Family, List<(string Stat, float Value, bool Flat)> Members)>();
            foreach (var eff in def.EffectsFor(classId))
            {
                float v = eff.Value(points);
                if (Math.Abs(v) < 0.0005f) continue;

                string family = Family(eff.Stat);
                int at = families.FindIndex(f => f.Family == family);
                if (at < 0) { families.Add((family, new List<(string, float, bool)>())); at = families.Count - 1; }
                families[at].Members.Add((eff.Stat, v, eff.Flat));
            }

            foreach (var (family, members) in families)
            {
                bool collapse = family != members[0].Stat
                    && members.TrueForAll(m => Math.Abs(m.Value - members[0].Value) < 0.0005f);
                if (collapse)
                {
                    yield return (Lang.Get("canrpgclasses:sheet-attr-" + family), members[0].Value, members[0].Flat);
                    continue;
                }
                foreach (var m in members) yield return (StatCatalog.Label(m.Stat), m.Value, m.Flat);
            }
        }

        private static string Family(string stat)
            => stat.StartsWith("spellpower_", StringComparison.Ordinal) ? "spellpower"
             : stat.StartsWith("canrpgResist_", StringComparison.Ordinal) ? "resist"
             : stat.StartsWith("canrpgPen_", StringComparison.Ordinal) ? "pen"
             : stat;

        private static string Signed(float v) => SheetFormat.Signed(v);
        private static string Pct(float v) => SheetFormat.Pct(v);
        private static string Mult(float v) => SheetFormat.Mult(v);

        /// <summary>The tone a 1.0-based multiplier reads in: above its baseline it's a bonus, below it a penalty,
        /// at it nothing at all.</summary>
        private static SheetTone ToneFor(float value, float baseline)
            => value > baseline + 0.005f ? SheetTone.Good
             : value < baseline - 0.005f ? SheetTone.Bad
             : SheetTone.Inactive;

        /// <summary>The same for a 0-based fraction, where anything above zero is a bonus.</summary>
        private static SheetTone ToneForFraction(float value)
            => value > 0.0005f ? SheetTone.Good : SheetTone.Inactive;

        private static string SchoolName(SpellSchool s)
        {
            string key = "canrpgclasses:school-" + s.ToString().ToLowerInvariant();
            return Lang.HasTranslation(key, true, false) ? Lang.Get(key) : s.ToString();
        }
    }
}
