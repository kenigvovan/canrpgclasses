using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Talents;
using Vintagestory.API.Common;

namespace canrpgclasses.Rogues.Talents
{
    // Rogue tree 2 - Utility: survival, sustain, mobility, escapes.
    // Tiers unlock at 0/4/8/12/16 points spent in this tree.

    // Hook talent: registers a DamageModifier into the core damage pipeline. Reduces fall damage per rank.
    [TalentRegistration("rogue:soft_landing")]
    public class SoftLandingTalent : Talent
    {
        public SoftLandingTalent()
        {
            ClassId = "rogue"; TreeIndex = 2; Tier = 0; Column = 0; MaxRank = 3;
            DisplayName = "Soft Landing"; Description = "-{0}% fall damage per rank."; IconName = "two-feathers";
            DescArgs = new object[] { (int)System.Math.Round(PerRank * 100f) };
        }
        /// <summary>Fall-damage reduction per rank, from config (default 0.20).</summary>
        public static float PerRank => BalanceConfig.Talent("rogue:soft_landing").F("perRank", 0.20f);
        public static float Multiplier(int rank) => System.Math.Max(0f, 1f - PerRank * rank);

        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || source.Source != EnumDamageSource.Fall) return;
            int rank = TalentState.Rank(victim, Id);
            if (rank > 0) damage *= Multiplier(rank);
        };
    }

    [TalentRegistration("rogue:hardy")]
    public class HardyTalent : Talent
    {
        private readonly float perRank;
        public HardyTalent()
        {
            ClassId = "rogue"; TreeIndex = 2; Tier = 0; Column = 1; MaxRank = 5;
            perRank = BalanceConfig.Talent("rogue:hardy").F("perRank", 0.05f);
            DisplayName = "Hardy"; Description = "-{0}% hunger rate per rank."; IconName = "diamond-hard";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.HungerRate, "canrpgtalent_hardy", -perRank * rank);
    }

    [TalentRegistration("rogue:learn_sprint")]
    public class LearnSprintTalent : Talent
    {
        public LearnSprintTalent()
        {
            ClassId = "rogue"; TreeIndex = 2; Tier = 0; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:sprint";
            DisplayName = "Sprint Training"; Description = "Unlocks Sprint (burst of movement speed).";
        }
    }

    [TalentRegistration("rogue:learn_second_wind")]
    public class LearnSecondWindTalent : Talent
    {
        public LearnSecondWindTalent()
        {
            ClassId = "rogue"; TreeIndex = 2; Tier = 1; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:second_wind";
            DisplayName = "Second Wind Training"; Description = "Unlocks Second Wind (heal + regeneration).";
        }
    }

    /// <summary>Resource-max talent: raises the rogue's maximum energy by 10 per rank (read live by
    /// <see cref="canrpgclasses.Core.Resources.ResourceState.EffectiveMax"/>).</summary>
    [TalentRegistration("rogue:vitality")]
    public class VitalityTalent : Talent
    {
        private readonly float perRank;
        public VitalityTalent()
        {
            ClassId = "rogue"; TreeIndex = 2; Tier = 1; Column = 1; MaxRank = 3;
            RequiresTalent = "rogue:hardy";
            perRank = BalanceConfig.Talent("rogue:vitality").F("perRank", 10f);
            DisplayName = "Vitality"; Description = "+{0} max energy per rank."; IconName = "tarot-08-strength";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override float MaxResourceBonus(string poolId, int rank)
            => poolId == "energy" ? perRank * rank : 0f;
    }

    [TalentRegistration("rogue:learn_disengage")]
    public class LearnDisengageTalent : Talent
    {
        public LearnDisengageTalent()
        {
            ClassId = "rogue"; TreeIndex = 2; Tier = 1; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:disengage";
            DisplayName = "Disengage Training"; Description = "Unlocks Disengage (leap back and vanish briefly).";
        }
    }

    [TalentRegistration("rogue:learn_nerve_strike")]
    public class LearnNerveStrikeTalent : Talent
    {
        public LearnNerveStrikeTalent()
        {
            ClassId = "rogue"; TreeIndex = 2; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:nerve_strike";
            DisplayName = "Nerve Strike Training"; Description = "Unlocks Nerve Strike (stun finisher).";
        }
    }

    [TalentRegistration("rogue:learn_disarm")]
    public class LearnDisarmTalent : Talent
    {
        public LearnDisarmTalent()
        {
            ClassId = "rogue"; TreeIndex = 2; Tier = 2; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:disarm";
            DisplayName = "Disarm Training"; Description = "Unlocks Disarm (knock the target's weapon out of hand).";
        }
    }

    // Rides the vanilla 'healingeffectivness' stat, so it scales poultices and bandages, not our own heals.
    [TalentRegistration("rogue:fast_mending")]
    public class FastMendingTalent : Talent
    {
        private readonly float perRank;
        public FastMendingTalent()
        {
            ClassId = "rogue"; TreeIndex = 2; Tier = 2; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("rogue:fast_mending").F("perRank", 0.08f);
            DisplayName = "Fast Mending"; Description = "+{0}% healing received per rank."; IconName = "mouth-watering";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.HealingEffectiveness, "canrpgtalent_quickrecovery", perRank * rank);
    }

    [TalentRegistration("rogue:toughness")]
    public class ToughnessTalent : Talent
    {
        private readonly float perRank;
        public ToughnessTalent()
        {
            ClassId = "rogue"; TreeIndex = 2; Tier = 3; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("rogue:toughness").F("perRank", 3f);
            DisplayName = "Toughness"; Description = "+{0} max health per rank."; IconName = "diamond-hard";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MaxHealthExtraPoints, "canrpgtalent_toughness", perRank * rank);
    }

    // Per-spell cooldown reduction (affects only this player - read from their own stats by EBSpellCaster).
    [TalentRegistration("rogue:practiced_regroup")]
    public class PracticedRegroupTalent : Talent
    {
        private readonly float perRank;
        public PracticedRegroupTalent()
        {
            ClassId = "rogue"; TreeIndex = 2; Tier = 3; Column = 2; MaxRank = 5;
            RequiresTalent = "rogue:soft_landing";
            perRank = BalanceConfig.Talent("rogue:practiced_regroup").F("perRank", 0.05f);
            DisplayName = "Practiced Regroup"; Description = "-{0}% Regroup cooldown per rank."; IconName = "sundial";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CooldownReductionFor("regroup"), "canrpgtalent_practicedprep", perRank * rank);
    }

    [TalentRegistration("rogue:learn_regroup")]
    public class LearnRegroupTalent : Talent
    {
        public LearnRegroupTalent()
        {
            ClassId = "rogue"; TreeIndex = 2; Tier = 4; Column = 1; MaxRank = 1;
            RequiresTalent = "rogue:soft_landing";
            GrantsSpellId = "canrpgclasses:regroup";
            DisplayName = "Regroup"; Description = "Unlocks Regroup (resets your mobility/survival cooldowns).";
        }
    }
}
