using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using canrpgclasses.Core;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Builds the colour-coded tooltip body for a spell, shared by the spellbook, class picker and talent window.
    /// Numbers are estimates at the local player's current spell power. <see cref="BuildLines"/> is pure -
    /// <see cref="Gui.GuiElementSpellTooltip"/> does the drawing.
    /// </summary>
    public static class SpellTooltip
    {
        // Palette as Cairo RGBA (0..1), which is what the vanilla GUI wants.
        public static readonly double[] Name = EditorStyle.Gold;
        public static readonly double[] Dmg = { 1.00, 0.55, 0.40, 1 };  // warm red
        public static readonly double[] Heal = { 0.45, 1.00, 0.50, 1 }; // green
        public static readonly double[] Ctrl = { 0.60, 0.80, 1.00, 1 }; // blue (stun/shield)
        public static readonly double[] Meta = { 0.70, 0.70, 0.75, 1 }; // dim grey
        public static readonly double[] Body = { 0.90, 0.90, 0.90, 1 }; // flavour text

        public readonly struct Line
        {
            public readonly string Text;
            public readonly double[] Color;
            /// <summary>Flavour text - the only line that should be word-wrapped rather than shown as-is.</summary>
            public readonly bool Wrap;

            public Line(string text, double[] color, bool wrap = false) { Text = text; Color = color; Wrap = wrap; }
        }

        public static List<Line> BuildLines(Entity? player, Spell spell, bool withName = true)
        {
            var lines = new List<Line>();
            void Add(string text, double[] color, bool wrap = false)
            {
                if (!string.IsNullOrEmpty(text)) lines.Add(new Line(text, color, wrap));
            }

            if (withName) Add(spell.DisplayName, Name);
            if (!string.IsNullOrEmpty(spell.Description)) Add(spell.Description, Body, true);

            float sp = player?.GetBehavior<EBRpgStats>()?.GetSpellPower(spell.School) ?? 1f;

            foreach (var imp in spell.Impacts)
            {
                switch (imp.Action)
                {
                    case ImpactAction.Damage:
                        Add(imp.DamagePerComboPoint > 0
                            ? Lang.Get("canrpgclasses:ui-tt-damage-combo", R(sp * imp.DamageSpellPowerCoefficient), R(sp * imp.DamagePerComboPoint))
                            : Lang.Get("canrpgclasses:ui-tt-damage", R(sp * imp.DamageSpellPowerCoefficient)), Dmg);
                        // Death Blow-style: the extra damage against a hurt target, shown at its max (target near 0 HP).
                        if (imp.DamageMissingHealthCoefficient > 0f)
                            Add(Lang.Get("canrpgclasses:ui-tt-death_blow", R(sp * imp.DamageMissingHealthCoefficient)), Dmg);
                        break;
                    case ImpactAction.DamageZone:
                        if (imp.DamageSpellPowerCoefficient > 0)
                            Add(imp.ZoneIsTrap
                                ? Lang.Get("canrpgclasses:ui-tt-trap", R(sp * imp.DamageSpellPowerCoefficient))
                                : Lang.Get("canrpgclasses:ui-tt-zone", R(sp * imp.DamageSpellPowerCoefficient), F(imp.ZoneDurationSeconds)), Dmg);
                        AddBuff(lines, imp.ZoneStatusEffectId, imp.ZoneStatusTier, imp.ZoneStatusSeconds);
                        break;
                    case ImpactAction.Heal:
                        Add(Lang.Get("canrpgclasses:ui-tt-heal", R(sp * imp.HealSpellPowerCoefficient)), Heal);
                        break;
                    case ImpactAction.Stun:
                        Add(imp.DurationPerComboPoint > 0
                            ? Lang.Get("canrpgclasses:ui-tt-stun-combo", F(imp.StatusEffectDuration), F(imp.DurationPerComboPoint))
                            : Lang.Get("canrpgclasses:ui-tt-stun", F(imp.StatusEffectDuration)), Ctrl);
                        break;
                    case ImpactAction.Shield:
                        Add(Lang.Get("canrpgclasses:ui-tt-absorb", R(sp * imp.ShieldSpellPowerCoefficient), F(imp.ShieldDurationSeconds)), Ctrl);
                        break;
                    case ImpactAction.EmpowerNextMelee:
                        Add(imp.DamagePerComboPoint > 0
                            ? Lang.Get("canrpgclasses:ui-tt-empower-combo", R(sp * imp.DamageSpellPowerCoefficient), R(sp * imp.DamagePerComboPoint))
                            : Lang.Get("canrpgclasses:ui-tt-empower", R(sp * imp.DamageSpellPowerCoefficient)), Dmg);
                        // Death Blow finisher rides an empowered swing: show its missing-health bonus at its max.
                        if (imp.DamageMissingHealthCoefficient > 0f)
                            Add(Lang.Get("canrpgclasses:ui-tt-death_blow", R(sp * imp.DamageMissingHealthCoefficient)), Dmg);
                        break;
                    case ImpactAction.EmpowerNextShot:
                        if (imp.DamageSpellPowerCoefficient > 0 || imp.DamagePerComboPoint > 0)
                            Add(imp.DamagePerComboPoint > 0
                                ? Lang.Get("canrpgclasses:ui-tt-shot-combo", R(sp * imp.DamageSpellPowerCoefficient), R(sp * imp.DamagePerComboPoint))
                                : Lang.Get("canrpgclasses:ui-tt-shot", R(sp * imp.DamageSpellPowerCoefficient)), Dmg);
                        if (imp.ShotExtraArrows > 0)
                            Add(Lang.Get("canrpgclasses:ui-tt-shot-extra", imp.ShotExtraArrows), Dmg);
                        if (imp.ShotHealCutPercent > 0)
                            Add(Lang.Get("canrpgclasses:ui-tt-shot-healcut", R(imp.ShotHealCutPercent * 100f), F(imp.ShotHealCutSeconds)), Ctrl);
                        if (imp.ShotResourceDrain > 0)
                            Add(Lang.Get("canrpgclasses:ui-tt-shot-drain", R(imp.ShotResourceDrain)), Ctrl);
                        if (imp.ShotIgnite)
                            Add(Lang.Get("canrpgclasses:ui-tt-shot-ignite"), Ctrl);
                        if (!string.IsNullOrEmpty(imp.StatusEffectId))
                            AddBuff(lines, imp.StatusEffectId, imp.StatusEffectAmplifier, imp.StatusEffectDuration);
                        break;
                    case ImpactAction.Reveal:
                        Add(Lang.Get("canrpgclasses:ui-tt-reveal", R(spell.Range > 0 ? spell.Range : 12f)), Ctrl);
                        break;
                    case ImpactAction.PetTarget:
                        // Two flavours share this action: Tend Beast heals (scales with spell power) and Beast Fury
                        // buffs pet damage (DamageSpellPowerCoefficient is a flat +% for a window).
                        if (imp.HealSpellPowerCoefficient > 0)
                            Add(Lang.Get("canrpgclasses:ui-tt-pet-heal", R(sp * imp.HealSpellPowerCoefficient)), Heal);
                        if (imp.DamageSpellPowerCoefficient > 0)
                            Add(Lang.Get("canrpgclasses:ui-tt-pet-wrath",
                                R(imp.DamageSpellPowerCoefficient * 100f), F(imp.StatusEffectDuration)), Dmg);
                        break;
                    case ImpactAction.StatusEffect:
                        AddBuff(lines, imp.StatusEffectId, imp.StatusEffectAmplifier, imp.StatusEffectDuration);
                        break;
                    case ImpactAction.Evasion:
                        Add(Lang.Get("canrpgclasses:ui-tt-evasion",
                            R(imp.EvasionChance * 100f), F(imp.StatusEffectDuration)), Ctrl);
                        break;
                    case ImpactAction.GainResource:
                        // Stances carry a GainResource impact with no fixed amount (the stance-switch bonus is a
                        // global, shown in their description) - only spells with an explicit amount draw a line.
                        if (imp.ResourceGainAmount > 0f)
                            Add(Lang.Get("canrpgclasses:ui-tt-gainrage", (int)imp.ResourceGainAmount), Meta);
                        break;
                    case ImpactAction.Aggro:
                        Add(Lang.Get("canrpgclasses:ui-tt-taunt", R(imp.AggroRange)), Ctrl);
                        break;
                }
            }

            if (spell.ComboBuilder) Add(Lang.Get("canrpgclasses:ui-tt-builder", spell.ComboPointsGenerated), Meta);
            else if (spell.ComboFinisher) Add(Lang.Get("canrpgclasses:ui-tt-finisher"), Meta);

            var parts = new List<string>();
            if (spell.Cost.Resource > 0) parts.Add(Lang.Get("canrpgclasses:ui-tt-cost", (int)spell.Cost.Resource));
            // Cooldown / cast shown at the viewing player's effective value (mirrors EBSpellCaster): base minus the
            // global cooldownReduction + a per-spell cooldownReduction_<spell> / castReduction_<spell>, capped at
            // 80%, so talents like Swift Light visibly lower the number after they're taken.
            if (spell.Cost.Cooldown.Duration > 0)
            {
                float cdr = player != null
                    ? System.Math.Clamp(player.ReductionStat(StatKeys.CooldownReduction) + player.ReductionStat(StatKeys.CooldownReductionFor(spell.LocalId)), 0f, 0.8f)
                    : 0f;
                parts.Add(Lang.Get("canrpgclasses:ui-tt-cd", F(spell.Cost.Cooldown.Duration * (1f - cdr))));
            }
            if (spell.CastDuration > 0)
            {
                float cr = player != null ? System.Math.Clamp(player.ReductionStat(StatKeys.CastReductionFor(spell.LocalId)), 0f, 0.8f) : 0f;
                parts.Add(Lang.Get("canrpgclasses:ui-tt-cast", F(spell.CastDuration * (1f - cr))));
            }
            if (spell.Range > 0) parts.Add(Lang.Get("canrpgclasses:ui-tt-range", R(spell.Range)));
            if (parts.Count > 0) Add(string.Join("   ·   ", parts), Meta);

            // Attribute gate, or the only hint the player gets is the refusal in chat when the cast fails.
            if (spell.RequiresAttributes != null)
                foreach (var kv in spell.RequiresAttributes)
                {
                    var attr = Core.Attributes.RpgAttributes.Get(kv.Key);
                    if (attr == null) continue;
                    bool met = player == null || attr.Points(player) + 0.0005f >= kv.Value;
                    Add(Lang.Get("canrpgclasses:ui-requires-attribute", attr.DisplayName, F(kv.Value)),
                        met ? Meta : Dmg);
                }

            return lines;
        }

        private static int R(float f) => (int)System.Math.Round(f);
        private static string F(float f) => f.ToString("0.#");

        /// <summary>One buff/debuff line for an effect id + tier + duration: "+50% move speed for 8s" when the
        /// effect has a known per-tier magnitude, else "Invisibility for 4s". Unknown ids add nothing.</summary>
        private static void AddBuff(List<Line> lines, string? id, int tier, float seconds)
        {
            var (label, perTier, debuff) = BuffInfo(id);
            if (string.IsNullOrEmpty(label)) return;
            string dur = F(seconds);
            string line = perTier != 0f
                ? Lang.Get("canrpgclasses:ui-tt-buff-pct",
                    (perTier * tier >= 0f ? "+" : "") + R(perTier * tier * 100f), label, dur)
                : Lang.Get("canrpgclasses:ui-tt-buff", label, dur);
            lines.Add(new Line(line, debuff ? Ctrl : Heal));
        }

        /// <summary>Per-effect display: human label, per-tier magnitude (0 = duration-only, no %), and whether it reads
        /// as a debuff. Magnitudes mirror effectshud's effect classes (±0.25/tier for the stat buffs).</summary>
        private static (string label, float perTier, bool debuff) BuffInfo(string? id) => id switch
        {
            "strengthmelee" => (Lang.Get("canrpgclasses:ui-eff-strengthmelee"), 0.25f, false),
            "walkspeed"     => (Lang.Get("canrpgclasses:ui-eff-walkspeed"), 0.25f, false),
            "weakmelee"     => (Lang.Get("canrpgclasses:ui-eff-weakmelee"), -0.25f, true),
            "walkslow"      => (Lang.Get("canrpgclasses:ui-eff-walkslow"), -0.25f, true),
            "invisibility"  => (Lang.Get("canrpgclasses:ui-eff-invisibility"), 0f, false),
            "regeneration"  => (Lang.Get("canrpgclasses:ui-eff-regeneration"), 0f, false),
            "thorns"        => (Lang.Get("canrpgclasses:ui-eff-thorns"), 0f, false),
            "vampirism"     => (Lang.Get("canrpgclasses:ui-eff-vampirism"), 0f, false),
            "jagged_blades"      => (Lang.Get("canrpgclasses:ui-eff-jagged_blades"), 0f, true),
            "poison"        => (Lang.Get("canrpgclasses:ui-eff-poison"), 0f, true),
            "canrpgclasses:slice_stacks" => (Lang.Get("canrpgclasses:ui-eff-strengthmelee"),
                canrpgclasses.Core.Config.BalanceConfig.Spell("quickblades").F("perStack", 0.06f), false),
            // Priest effects (magnitudes live in each spell's description; these just name the buff/debuff + duration).
            "canrpg_renew"             => (Lang.Get("canrpgclasses:ui-eff-renew"), 0f, false),
            "canrpg_shadow_brand"  => (Lang.Get("canrpgclasses:ui-eff-shadow_brand"), 0f, true),
            "canrpg_consuming_plague"  => (Lang.Get("canrpgclasses:ui-eff-consuming_plague"), 0f, true),
            "canrpg_sacred_flame"         => (Lang.Get("canrpgclasses:ui-eff-sacred_flame"), 0f, true),
            "canrpg_inner_flame"        => (Lang.Get("canrpgclasses:ui-eff-inner_flame"), 0f, false),
            "canrpg_suppress_pain"  => (Lang.Get("canrpgclasses:ui-eff-suppress_pain"), 0f, false),
            "canrpg_sheltering_spirit"   => (Lang.Get("canrpgclasses:ui-eff-sheltering_spirit"), 0f, false),
            "canrpg_dissipate"        => (Lang.Get("canrpgclasses:ui-eff-dissipate"), 0f, false),
            "canrpgaura_shadow_guise"    => (Lang.Get("canrpgclasses:ui-eff-shadow_guise"), 0f, false),
            // Mage effects (magnitudes live in each spell's description; these name the buff/debuff + duration).
            "canrpg_chilled"      => (Lang.Get("canrpgclasses:ui-eff-chilled"), -0.15f, true),
            "canrpg_ignite"       => (Lang.Get("canrpgclasses:ui-eff-ignite"), 0f, true),
            "canrpg_volatile_ember"  => (Lang.Get("canrpgclasses:ui-eff-volatile_ember"), 0f, true),
            "canrpg_conflagration"   => (Lang.Get("canrpgclasses:ui-eff-conflagration"), 0f, false),
            "canrpg_icy_veins"    => (Lang.Get("canrpgclasses:ui-eff-icy_veins"), 0f, false),
            "canrpg_mystic_surge" => (Lang.Get("canrpgclasses:ui-eff-mystic_surge"), 0f, false),

            "canrpg_static_shield"   => (Lang.Get("canrpgclasses:ui-eff-static_shield"), 0f, false),
            "canrpg_ember_shock"        => (Lang.Get("canrpgclasses:ui-eff-ember_shock"), 0f, true),
            "canrpg_elemental_surge"  => (Lang.Get("canrpgclasses:ui-eff-elemental_surge"), 0f, false),
            "canrpg_totem_earth"        => (Lang.Get("canrpgclasses:ui-eff-totem_earth"), 0f, false),
            "canrpg_imbue_flametongue"  => (Lang.Get("canrpgclasses:ui-eff-imbue_flametongue"), 0f, false),
            "canrpg_imbue_frostbrand"   => (Lang.Get("canrpgclasses:ui-eff-imbue_frostbrand"), 0f, false),
            "canrpg_imbue_windfury"     => (Lang.Get("canrpgclasses:ui-eff-imbue_windfury"), 0f, false),
            "canrpg_stormstrike_armed"  => (Lang.Get("canrpgclasses:ui-eff-stormstrike_armed"), 0f, false),
            "canrpg_thunder_cleave"        => (Lang.Get("canrpgclasses:ui-eff-thunder_cleave"), 0f, true),
            "canrpg_maelstrom"          => (Lang.Get("canrpgclasses:ui-eff-maelstrom"), 0f, false),
            "canrpg_war_chant"          => (Lang.Get("canrpgclasses:ui-eff-war_chant"), 0f, false),
            "canrpg_tide_surge"            => (Lang.Get("canrpgclasses:ui-eff-tide_surge"), 0f, false),
            "canrpg_stone_ward"       => (Lang.Get("canrpgclasses:ui-eff-stone_ward"), 0f, false),
            "canrpg_rising_tide"        => (Lang.Get("canrpgclasses:ui-eff-rising_tide"), 0f, false),
            "canrpgaura_spirit_wolf"     => (Lang.Get("canrpgclasses:ui-eff-spirit_wolf"), 0f, false),
            _ => ("", 0f, false),
        };
    }
}
