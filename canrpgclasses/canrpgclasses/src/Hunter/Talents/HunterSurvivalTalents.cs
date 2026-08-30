using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Hunter.Talents
{
    // Hunter tree 1 - Survival: traps, mobility, sustain, durability.
    // Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("hunter:learn_venom_sting")]
    public class LearnVenomStingTalent : Talent
    {
        public LearnVenomStingTalent()
        {
            ClassId = "hunter"; TreeIndex = 1; Tier = 0; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:venom_sting";
            DisplayName = "Venom Sting Training"; Description = "Unlocks Venom Sting (a poison shot).";
        }
    }

    [TalentRegistration("hunter:hunters_hide")]
    public class HuntersHideTalent : Talent
    {
        private readonly float perRank;
        public HuntersHideTalent()
        {
            ClassId = "hunter"; TreeIndex = 1; Tier = 0; Column = 1; MaxRank = 5;
            perRank = BalanceConfig.Talent("hunter:hunters_hide").F("perRank", 3f);
            DisplayName = "Hunter's Hide"; Description = "+{0} max health per rank."; IconName = "diamond-hard";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MaxHealthExtraPoints, "canrpgtalent_huntershide", perRank * rank);
    }

    [TalentRegistration("hunter:learn_chill_trap")]
    public class LearnChillTrapTalent : Talent
    {
        public LearnChillTrapTalent()
        {
            ClassId = "hunter"; TreeIndex = 1; Tier = 0; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:chill_trap";
            DisplayName = "Chill Trap Training"; Description = "Unlocks Chill Trap (a slowing area).";
        }
    }

    [TalentRegistration("hunter:light_footed")]
    public class LightFootedTalent : Talent
    {
        private readonly float perRank;
        public LightFootedTalent()
        {
            ClassId = "hunter"; TreeIndex = 1; Tier = 1; Column = 0; MaxRank = 5;
            perRank = BalanceConfig.Talent("hunter:light_footed").F("perRank", 0.03f);
            DisplayName = "Light Footed"; Description = "+{0}% movement speed per rank."; IconName = "sonic-shoes";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.WalkSpeed, "canrpgtalent_fleetfooted", perRank * rank);
    }

    [TalentRegistration("hunter:learn_second_breath")]
    public class LearnSecondBreathTalent : Talent
    {
        public LearnSecondBreathTalent()
        {
            ClassId = "hunter"; TreeIndex = 1; Tier = 1; Column = 1; MaxRank = 1;
            RequiresTalent = "hunter:hunters_hide";
            GrantsSpellId = "canrpgclasses:second_breath";
            DisplayName = "Second Breath Training"; Description = "Unlocks Second Breath (self-heal + regeneration).";
        }
    }

    [TalentRegistration("hunter:learn_disengage")]
    public class LearnDisengageTalent : Talent
    {
        public LearnDisengageTalent()
        {
            ClassId = "hunter"; TreeIndex = 1; Tier = 1; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:hunter_disengage";
            DisplayName = "Disengage Training"; Description = "Unlocks Disengage (leap back and chill nearby foes).";
        }
    }

    [TalentRegistration("hunter:learn_blast_trap")]
    public class LearnBlastTrapTalent : Talent
    {
        public LearnBlastTrapTalent()
        {
            ClassId = "hunter"; TreeIndex = 1; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:blast_trap";
            DisplayName = "Blast Trap Training"; Description = "Unlocks Blast Trap (a fiery damage area).";
        }
    }

    [TalentRegistration("hunter:natural_recovery")]
    public class NaturalRecoveryTalent : Talent
    {
        private readonly float perRank;
        public NaturalRecoveryTalent()
        {
            ClassId = "hunter"; TreeIndex = 1; Tier = 2; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("hunter:natural_recovery").F("perRank", 0.08f);
            DisplayName = "Natural Recovery"; Description = "+{0}% healing received per rank."; IconName = "mouth-watering";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.HealingEffectiveness, "canrpgtalent_naturalrecovery", perRank * rank);
    }

    [TalentRegistration("hunter:learn_concealment")]
    public class LearnConcealmentTalent : Talent
    {
        public LearnConcealmentTalent()
        {
            ClassId = "hunter"; TreeIndex = 1; Tier = 2; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:concealment";
            DisplayName = "Concealment Training"; Description = "Unlocks Concealment (turn invisible to reposition).";
        }
    }

    // Ranked passive: your traps hit harder and their control lasts longer. Read by ZoneManager.Trigger, which
    // multiplies both the blast damage and the status duration by the trapPower stat (1.0 = unmodified).
    [TalentRegistration("hunter:trap_mastery")]
    public class TrapMasteryTalent : Talent
    {
        private readonly float perRank;
        public TrapMasteryTalent()
        {
            ClassId = "hunter"; TreeIndex = 1; Tier = 3; Column = 0; MaxRank = 5;
            perRank = BalanceConfig.Talent("hunter:trap_mastery").F("perRank", 0.08f);
            DisplayName = "Trap Mastery"; Description = "+{0}% trap damage and control duration per rank."; IconName = "spinning-blades";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        // GetBlended is 1.0-based (unset = 1.0, named values add on top), so Set just the bonus - the engine adds
        // the baseline 1.0. (Setting 1f + bonus here would double-count and make traps ~2x too strong.)
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.TrapPower, "canrpgtalent_trapmastery", perRank * rank);
    }

    // Ranked passive: flat damage mitigation (read by StunPatches via the 1.0-based canrpgDamageReduction stat).
    [TalentRegistration("hunter:tough_hide")]
    public class ToughHideTalent : Talent
    {
        private readonly float perRank;
        public ToughHideTalent()
        {
            ClassId = "hunter"; TreeIndex = 1; Tier = 3; Column = 1; MaxRank = 3;
            RequiresTalent = "hunter:hunters_hide";
            perRank = BalanceConfig.Talent("hunter:tough_hide").F("perRank", 0.03f);
            DisplayName = "Tough Hide"; Description = "-{0}% damage taken per rank."; IconName = "leg-armor";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.DamageReduction, "canrpgtalent_thickhide", perRank * rank);
    }

    [TalentRegistration("hunter:learn_signal_flare")]
    public class LearnSignalFlareTalent : Talent
    {
        public LearnSignalFlareTalent()
        {
            ClassId = "hunter"; TreeIndex = 1; Tier = 3; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:signal_flare";
            DisplayName = "Signal Flare Training"; Description = "Unlocks Signal Flare (reveals hidden enemies in an area).";
        }
    }

    // Skill augment: a damaging trap (Blast Trap) also sets what it catches on fire. Sets a generic flag stat
    // read by ZoneManager (core stays class-agnostic).
    [TalentRegistration("hunter:napalm")]
    public class NapalmTalent : Talent
    {
        public NapalmTalent()
        {
            ClassId = "hunter"; TreeIndex = 1; Tier = 4; Column = 0; MaxRank = 1;
            RequiresTalent = "hunter:learn_blast_trap";
            DisplayName = "Napalm Traps"; Description = "Your Blast Trap also sets enemies caught in the blast on fire."; IconName = "lava";
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.TrapIgnite, "canrpgtalent_napalm", rank > 0 ? 1f : 0f);
    }

    [TalentRegistration("hunter:learn_fresh_quiver")]
    public class LearnFreshQuiverTalent : Talent
    {
        public LearnFreshQuiverTalent()
        {
            ClassId = "hunter"; TreeIndex = 1; Tier = 4; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:fresh_quiver";
            DisplayName = "Fresh Quiver"; Description = "Unlocks Fresh Quiver (resets your trap and survival cooldowns).";
        }
    }
}
