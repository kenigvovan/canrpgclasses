using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Rogues.Talents
{
    // Rogue tree 0 - Stealth: mobility, avoidance, control/burst openers.
    // Tiers unlock at 0/4/8/12/16 points spent in this tree (TalentState.PointsPerTier).

    [TalentRegistration("rogue:nimble")]
    public class NimbleTalent : Talent
    {
        private readonly float perRank;
        public NimbleTalent()
        {
            ClassId = "rogue"; TreeIndex = 0; Tier = 0; Column = 0; MaxRank = 5;
            perRank = BalanceConfig.Talent("rogue:nimble").F("perRank", 0.04f);
            DisplayName = "Light Step"; Description = "+{0}% movement speed per rank."; IconName = "sonic-shoes";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.WalkSpeed, "canrpgtalent_nimble", perRank * rank);
    }

    [TalentRegistration("rogue:quiet_steps")]
    public class QuietStepsTalent : Talent
    {
        private readonly float perRank;
        public QuietStepsTalent()
        {
            ClassId = "rogue"; TreeIndex = 0; Tier = 0; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("rogue:quiet_steps").F("perRank", 0.12f);
            DisplayName = "Quiet Steps"; Description = "Mobs notice you from {0}% less range per rank."; IconName = "cat";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.AnimalSeekingRange, "canrpgtalent_quietsteps", -perRank * rank);
    }

    [TalentRegistration("rogue:learn_venom_coat")]
    public class LearnVenomCoatTalent : Talent
    {
        public LearnVenomCoatTalent()
        {
            ClassId = "rogue"; TreeIndex = 0; Tier = 1; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:venom_coat";
            DisplayName = "Venom Coat Training"; Description = "Unlocks Venom Coat (coat your weapon in poison).";
        }
    }

    [TalentRegistration("rogue:learn_eye_jab")]
    public class LearnEyeJabTalent : Talent
    {
        public LearnEyeJabTalent()
        {
            ClassId = "rogue"; TreeIndex = 0; Tier = 1; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:eye_jab";
            DisplayName = "Eye Jab Training"; Description = "Unlocks Eye Jab (incapacitate a target).";
        }
    }

    [TalentRegistration("rogue:learn_blind")]
    public class LearnBlindTalent : Talent
    {
        public LearnBlindTalent()
        {
            ClassId = "rogue"; TreeIndex = 0; Tier = 3; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:blind";
            DisplayName = "Blind Training"; Description = "Unlocks Blind (incapacitate at range; breaks on damage).";
        }
    }

    // Hook talent: positional damage that stacks with Blindside.
    [TalentRegistration("rogue:exposed_flesh")]
    public class ExposedFleshTalent : Talent
    {
        public ExposedFleshTalent()
        {
            ClassId = "rogue"; TreeIndex = 0; Tier = 2; Column = 0; MaxRank = 5;
            DisplayName = "Exposed Flesh"; Description = "+{0}% damage from behind per rank."; IconName = "achilles-heel";
            DescArgs = new object[] { (int)System.Math.Round(PerRank * 100f) };
        }
        /// <summary>Bonus per rank, from config (default 0.04).</summary>
        public static float PerRank => BalanceConfig.Talent("rogue:exposed_flesh").F("perRank", 0.04f);
        public static float Multiplier(int rank) => 1f + PerRank * rank;

        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || attacker == null || !DamageModifiers.IsBehind(victim, attacker)) return;
            int rank = TalentState.Rank(attacker, Id);
            if (rank > 0) damage *= Multiplier(rank);
        };
    }

    [TalentRegistration("rogue:learn_crippling_oil")]
    public class LearnCripplingOilTalent : Talent
    {
        public LearnCripplingOilTalent()
        {
            ClassId = "rogue"; TreeIndex = 0; Tier = 2; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:crippling_oil";
            DisplayName = "Crippling Oil Training"; Description = "Unlocks Crippling Oil (hits slow the target).";
        }
    }

    [TalentRegistration("rogue:learn_shadow_stride")]
    public class LearnShadowStrideTalent : Talent
    {
        public LearnShadowStrideTalent()
        {
            ClassId = "rogue"; TreeIndex = 0; Tier = 2; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:shadow_stride";
            DisplayName = "Shadow Stride Training"; Description = "Unlocks Shadow Stride (blink behind the target).";
        }
    }

    [TalentRegistration("rogue:learn_shock_powder")]
    public class LearnShockPowderTalent : Talent
    {
        public LearnShockPowderTalent()
        {
            ClassId = "rogue"; TreeIndex = 0; Tier = 3; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:shock_powder";
            DisplayName = "Shock Powder Training"; Description = "Unlocks Shock Powder (AoE stun).";
        }
    }

    [TalentRegistration("rogue:nimble_fingers")]
    public class NimbleFingersTalent : Talent
    {
        private readonly float perRank;
        public NimbleFingersTalent()
        {
            ClassId = "rogue"; TreeIndex = 0; Tier = 1; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("rogue:nimble_fingers").F("perRank", 0.02f);
            DisplayName = "Nimble Fingers"; Description = "-{0}% skill cooldowns per rank."; IconName = "rupee";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CooldownReduction, "canrpgtalent_defthands", perRank * rank);
    }

    [TalentRegistration("rogue:shadow_adept")]
    public class ShadowAdeptTalent : Talent
    {
        private readonly float perRank;
        public ShadowAdeptTalent()
        {
            ClassId = "rogue"; TreeIndex = 0; Tier = 3; Column = 0; MaxRank = 3;
            perRank = BalanceConfig.Talent("rogue:shadow_adept").F("perRank", 0.03f);
            DisplayName = "Shadow Adept"; Description = "+{0}% movement speed per rank."; IconName = "eclipse";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.WalkSpeed, "canrpgtalent_mastershadows", perRank * rank);
    }

    [TalentRegistration("rogue:learn_slip_away")]
    public class LearnSlipAwayTalent : Talent
    {
        public LearnSlipAwayTalent()
        {
            ClassId = "rogue"; TreeIndex = 0; Tier = 4; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:slip_away";
            DisplayName = "Slip Away"; Description = "Unlocks Slip Away (turn invisible and break combat).";
        }
    }
}
