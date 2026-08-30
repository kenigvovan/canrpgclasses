using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Rogues.Talents
{
    // Rogue tree 1 - Combat: sustained melee damage, finishers, burst.
    // Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("rogue:blade_master")]
    public class BladeMasterTalent : Talent
    {
        private readonly float perRank;
        public BladeMasterTalent()
        {
            ClassId = "rogue"; TreeIndex = 1; Tier = 0; Column = 0; MaxRank = 5;
            perRank = BalanceConfig.Talent("rogue:blade_master").F("perRank", 0.03f);
            DisplayName = "Blade Master"; Description = "+{0}% melee weapon damage per rank."; IconName = "spinning-blades";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MeleeWeaponsDamage, "canrpgtalent_blademaster", perRank * rank);
    }

    [TalentRegistration("rogue:learn_quickblades")]
    public class LearnQuickbladesTalent : Talent
    {
        public LearnQuickbladesTalent()
        {
            ClassId = "rogue"; TreeIndex = 1; Tier = 0; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:quickblades";
            DisplayName = "Quickblades Training"; Description = "Unlocks Quickblades (attack-speed/strength buff).";
        }
    }

    // Hook talent: registers a DamageModifier into the core damage pipeline. Bonus damage per rank when
    // striking the target from behind (value from config).
    [TalentRegistration("rogue:blindside")]
    public class BlindsideTalent : Talent
    {
        public BlindsideTalent()
        {
            ClassId = "rogue"; TreeIndex = 1; Tier = 1; Column = 1; MaxRank = 5;
            DisplayName = "Blindside"; Description = "+{0}% damage from behind per rank."; IconName = "backstab";
            DescArgs = new object[] { (int)System.Math.Round(PerRank * 100f) };
        }
        /// <summary>Bonus per rank, from config (default 0.10).</summary>
        public static float PerRank => BalanceConfig.Talent("rogue:blindside").F("perRank", 0.10f);
        public static float Multiplier(int rank) => 1f + PerRank * rank;

        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || attacker == null || !DamageModifiers.IsBehind(victim, attacker)) return;
            int rank = TalentState.Rank(attacker, Id);
            if (rank > 0) damage *= Multiplier(rank);
        };
    }

    // Read in SpellExecutor.ApplyDamage via the 1.0-based finisherDamage stat (seeded in EBRpgStats).
    [TalentRegistration("rogue:deadly_precision")]
    public class DeadlyPrecisionTalent : Talent
    {
        private readonly float perRank;
        public DeadlyPrecisionTalent()
        {
            ClassId = "rogue"; TreeIndex = 1; Tier = 1; Column = 0; MaxRank = 5;
            perRank = BalanceConfig.Talent("rogue:deadly_precision").F("perRank", 0.04f);
            DisplayName = "Deadly Precision"; Description = "+{0}% finisher damage per rank."; IconName = "deadly-strike";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.FinisherDamage, "canrpgtalent_deadly_precision", perRank * rank);
    }

    [TalentRegistration("rogue:learn_counterstrike")]
    public class LearnCounterstrikeTalent : Talent
    {
        public LearnCounterstrikeTalent()
        {
            ClassId = "rogue"; TreeIndex = 1; Tier = 1; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:counterstrike";
            DisplayName = "Counterstrike Training"; Description = "Unlocks Counterstrike (reflect incoming damage).";
        }
    }

    // Hook talent: registers an OnMeleeHit perk into the core melee trigger. Melee hits bleed the target
    // (longer per rank); duration from BalanceConfig (single source).
    [TalentRegistration("rogue:jagged_blades")]
    public class JaggedBladesTalent : Talent
    {
        public JaggedBladesTalent()
        {
            ClassId = "rogue"; TreeIndex = 1; Tier = 2; Column = 1; MaxRank = 3;
            RequiresTalent = "rogue:blindside";
            DisplayName = "Jagged Blades"; Description = "Melee hits make the target bleed (longer per rank)."; IconName = "bowie-knife";
        }
        public static int DurationSeconds(int rank)
        {
            var b = BalanceConfig.Talent("rogue:jagged_blades");
            return (int)System.Math.Round(b.F("baseSeconds", 4f) + b.F("perRank", 2f) * rank);
        }

        public override MeleeHitHook? OnMeleeHit => (attacker, target) =>
        {
            int rank = TalentState.Rank(attacker, Id);
            if (rank > 0) EBSpellCaster.ApplyEffectshud(target, "jagged_blades", rank, DurationSeconds(rank));
        };
    }

    [TalentRegistration("rogue:learn_open_wound")]
    public class LearnOpenWoundTalent : Talent
    {
        public LearnOpenWoundTalent()
        {
            ClassId = "rogue"; TreeIndex = 1; Tier = 2; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:open_wound";
            DisplayName = "Open Wound Training"; Description = "Unlocks Open Wound (bleed finisher, scales with combo).";
        }
    }

    [TalentRegistration("rogue:learn_bloodletting")]
    public class LearnBloodlettingTalent : Talent
    {
        public LearnBloodlettingTalent()
        {
            ClassId = "rogue"; TreeIndex = 1; Tier = 3; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:bloodletting";
            DisplayName = "Bloodletting Training"; Description = "Unlocks Bloodletting (melee hits lifesteal).";
        }
    }

    [TalentRegistration("rogue:learn_evasion")]
    public class LearnEvasionTalent : Talent
    {
        public LearnEvasionTalent()
        {
            ClassId = "rogue"; TreeIndex = 1; Tier = 3; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:evasion";
            DisplayName = "Evasion Training"; Description = "Unlocks Evasion (chance to dodge physical attacks).";
        }
    }

    [TalentRegistration("rogue:learn_whirling_knives")]
    public class LearnWhirlingKnivesTalent : Talent
    {
        public LearnWhirlingKnivesTalent()
        {
            ClassId = "rogue"; TreeIndex = 1; Tier = 3; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:whirling_knives";
            DisplayName = "Whirling Knives Training"; Description = "Unlocks Whirling Knives (AoE damage around you).";
        }
    }

    [TalentRegistration("rogue:learn_battle_rush")]
    public class LearnBattleRushTalent : Talent
    {
        public LearnBattleRushTalent()
        {
            ClassId = "rogue"; TreeIndex = 1; Tier = 4; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:battle_rush";
            DisplayName = "Battle Rush"; Description = "Unlocks Battle Rush (burst of energy/haste).";
        }
    }
}
