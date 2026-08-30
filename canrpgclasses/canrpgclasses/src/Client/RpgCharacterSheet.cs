using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using canrpgclasses.Core;
using canrpgclasses.Core.HarmonyPatches;
using canrpgclasses.Core.Progression;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using EffectsHud = effectshud.src.effectshud;
using Vintagestory.API.Client;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Contributes the RPG stats (class/level/XP/talent points + derived stats) to the character dialog tab that
    /// effectshud owns, through its <see cref="effectshud.src.ICharacterSheetSection"/> extension point - which
    /// keeps the dependency one-way. All values are already client-side.
    /// </summary>
    public class RpgCharacterSheet : ModSystem, effectshud.src.ICharacterSheetSection
    {
        public double Order => 0; // stats above the effect list

        // Enum.GetValues allocates a fresh array per call; the sheet loops schools 3× per 500ms refresh - cache once.
        private static readonly SpellSchool[] Schools = Enum.GetValues<SpellSchool>();

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            EffectsHud.RegisterCharacterSheetSection(this);
            EffectsHud.SetCharacterTabTitle(() => Lang.Get("canrpgclasses:ui-rpgtab"));
        }

        public void Append(StringBuilder sb, Entity player)
        {
            var mod = canrpgclassesModSystem.ClientInstance;

            var prog = player.GetBehavior<EBProgression>();
            int level = prog?.Level ?? 1;
            int total = prog?.TotalTalentPoints ?? 0;
            int avail = TalentState.AvailablePoints(player, total);
            string classId = TalentState.CurrentClass(player);
            string className = mod?.Classes.Get(classId)?.DisplayName ?? classId;
            long xpInto = prog?.XpIntoLevel ?? 0;
            long xpNext = prog?.XpForNextLevel ?? 0;

            sb.AppendLine(Lang.Get("canrpgclasses:sheet-summary", className, level, xpInto, xpNext));
            sb.AppendLine(Lang.Get("canrpgclasses:sheet-points", avail, total));
            sb.AppendLine();

            sb.AppendLine(Lang.Get("canrpgclasses:sheet-stats-header"));

            float baseResist = StunPatches.InnateMagicResist(player) + player.ReductionStat(StatKeys.MagicResist);
            sb.AppendLine(Lang.Get("canrpgclasses:sheet-resist-base", Pct(baseResist)));
            AppendPerSchool(sb, player, "canrpgclasses:sheet-resist-school", d => d.ResistStat);

            float pen = player.ReductionStat(StatKeys.MagicPen);
            if (pen > 0.0005f) sb.AppendLine(Lang.Get("canrpgclasses:sheet-pen", Pct(pen)));
            // Per-school penetration (e.g. priest's shadow piercing), shown only where a talent grants it.
            AppendPerSchool(sb, player, "canrpgclasses:sheet-pen-school", d => d.PenStat);

            float dr = player.ReductionStat(StatKeys.DamageReduction);
            if (dr > 0.0005f) sb.AppendLine(Lang.Get("canrpgclasses:sheet-dr", Pct(dr)));

            float cdr = player.ReductionStat(StatKeys.CooldownReduction);
            if (cdr > 0.0005f) sb.AppendLine(Lang.Get("canrpgclasses:sheet-cdr", Pct(cdr)));

            float regen = player.Stats.GetBlended(StatKeys.ResourceRegen);
            if (Math.Abs(regen - 1f) > 0.005f) sb.AppendLine(Lang.Get("canrpgclasses:sheet-regen", Mult(regen)));

            float healing = player.Stats.GetBlended(StatKeys.HealingPower);
            if (Math.Abs(healing - 1f) > 0.005f) sb.AppendLine(Lang.Get("canrpgclasses:sheet-healing", Mult(healing)));

            // Per-school spell-power multipliers, only where a talent/gear bonus is present (base = 1.0).
            var spLines = new List<string>();
            foreach (SpellSchool s in Schools)
            {
                if (DamageSchools.For(s).Physical) continue;
                float sp = player.Stats.GetBlended(StatKeys.SpellpowerFor(s));
                if (Math.Abs(sp - 1f) > 0.005f)
                    spLines.Add(Lang.Get("canrpgclasses:sheet-spellpower", SchoolName(s), Mult(sp)));
            }
            if (spLines.Count > 0)
            {
                sb.AppendLine(Lang.Get("canrpgclasses:sheet-spellpower-header"));
                foreach (var l in spLines) sb.AppendLine(l);
            }
        }

        /// <summary>Appends a "  + School: X%" line for each magical school whose 1.0-based reduction stat (chosen by
        /// <paramref name="stat"/>) is non-zero. Shared by the resist and penetration blocks.</summary>
        private static void AppendPerSchool(StringBuilder sb, Entity player, string langKey, System.Func<DamageSchools.Def, string> stat)
        {
            foreach (SpellSchool s in Schools)
            {
                var def = DamageSchools.For(s);
                if (def.Physical) continue;
                float v = player.ReductionStat(stat(def));
                if (v > 0.0005f) sb.AppendLine(Lang.Get(langKey, SchoolName(s), Pct(v)));
            }
        }

        private static string Pct(float v) => (v * 100f).ToString("0");
        private static string Mult(float v) => v.ToString("0.##");

        private static string SchoolName(SpellSchool s)
        {
            string key = "canrpgclasses:school-" + s.ToString().ToLowerInvariant();
            return Lang.HasTranslation(key, true, false) ? Lang.Get(key) : s.ToString();
        }
    }
}
