using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using Vintagestory.API.Common;

namespace canrpgclasses.Druid.Talents
{
    // Druid tree 1 - Feral. Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("druid:honed_claws")]
    public class HonedClawsTalent : Talent
    {
        private readonly float perRank;
        public HonedClawsTalent()
        {
            ClassId = "druid"; TreeIndex = 1; Tier = 0; Column = 1; MaxRank = 5;
            perRank = BalanceConfig.Talent("druid:honed_claws").F("perRank", 0.04f);
            DisplayName = "Honed Claws"; Description = "+{0}% nature spell power per rank (feral abilities)."; IconName = "steel-claws";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Nature), "canrpgtalent_sharpenedclaws", perRank * rank);
    }

    [TalentRegistration("druid:learn_claw_slash")]
    public class LearnClawSlashTalent : Talent
    {
        public LearnClawSlashTalent()
        {
            ClassId = "druid"; TreeIndex = 1; Tier = 0; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:claw_slash";
            DisplayName = "Claw Slash Training"; Description = "Unlocks Claw Slash (a Cat builder that leaves a bleed)."; IconName = "claw-slashes";
        }
    }

    [TalentRegistration("druid:learn_ursine_form")]
    public class LearnUrsineFormTalent : Talent
    {
        public LearnUrsineFormTalent()
        {
            ClassId = "druid"; TreeIndex = 1; Tier = 0; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:ursine_form";
            DisplayName = "Ursine Form Training"; Description = "Unlocks Ursine Form (a rage-fuelled tank shapeshift)."; IconName = "bear-head";
        }
    }

    [TalentRegistration("druid:learn_deep_rend")]
    public class LearnDeepRendTalent : Talent
    {
        public LearnDeepRendTalent()
        {
            ClassId = "druid"; TreeIndex = 1; Tier = 1; Column = 0; MaxRank = 1;
            RequiresTalent = "druid:learn_claw_slash";
            GrantsSpellId = "canrpgclasses:deep_rend";
            DisplayName = "Deep Rend Training"; Description = "Unlocks Deep Rend (a Cat finisher bleed that lasts longer per combo point).";
        }
    }

    [TalentRegistration("druid:learn_bone_crunch")]
    public class LearnBoneCrunchTalent : Talent
    {
        public LearnBoneCrunchTalent()
        {
            ClassId = "druid"; TreeIndex = 1; Tier = 1; Column = 1; MaxRank = 1;
            RequiresTalent = "druid:learn_heavy_paw";
            GrantsSpellId = "canrpgclasses:bone_crunch";
            DisplayName = "Bone Crunch Training"; Description = "Unlocks Bone Crunch (the Bear's free rage builder)."; IconName = "wolverine-claws";
        }
    }

    [TalentRegistration("druid:learn_heavy_paw")]
    public class LearnHeavyPawTalent : Talent
    {
        public LearnHeavyPawTalent()
        {
            ClassId = "druid"; TreeIndex = 1; Tier = 1; Column = 2; MaxRank = 1;
            RequiresTalent = "druid:learn_ursine_form";
            GrantsSpellId = "canrpgclasses:heavy_paw";
            DisplayName = "Heavy Paw Training"; Description = "Unlocks Heavy Paw (a heavy Rage-spending strike)."; IconName = "knockout";
        }
    }

    [TalentRegistration("druid:learn_claw_sweep")]
    public class LearnClawSweepTalent : Talent
    {
        public LearnClawSweepTalent()
        {
            ClassId = "druid"; TreeIndex = 1; Tier = 2; Column = 0; MaxRank = 1;
            RequiresTalent = "druid:learn_ursine_form";
            GrantsSpellId = "canrpgclasses:claw_sweep";
            DisplayName = "Claw Sweep Training"; Description = "Unlocks Claw Sweep (a Bear AoE sweep)."; IconName = "triple-scratches";
        }
    }

    [TalentRegistration("druid:learn_gash")]
    public class LearnGashTalent : Talent
    {
        public LearnGashTalent()
        {
            ClassId = "druid"; TreeIndex = 1; Tier = 2; Column = 1; MaxRank = 1;
            RequiresTalent = "druid:learn_ursine_form";
            GrantsSpellId = "canrpgclasses:gash";
            DisplayName = "Gash Training"; Description = "Unlocks Gash (a Bear bleed).";
        }
    }

    [TalentRegistration("druid:tough_hide")]
    public class ToughHideTalent : Talent
    {
        private readonly float perRank;
        public ToughHideTalent()
        {
            ClassId = "druid"; TreeIndex = 1; Tier = 2; Column = 2; MaxRank = 5;
            perRank = BalanceConfig.Talent("druid:tough_hide").F("perRank", 0.03f);
            DisplayName = "Tough Hide"; Description = "+{0}% damage reduction per rank (doubled while in Ursine Form)."; IconName = "animal-hide";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.DamageReduction, "canrpgtalent_thickhide", perRank * rank);
    }

    [TalentRegistration("druid:learn_roar")]
    public class LearnRoarTalent : Talent
    {
        public LearnRoarTalent()
        {
            ClassId = "druid"; TreeIndex = 1; Tier = 3; Column = 0; MaxRank = 1;
            RequiresTalent = "druid:learn_ursine_form";
            GrantsSpellId = "canrpgclasses:roar";
            DisplayName = "Roar Training"; Description = "Unlocks Roar (a Bear taunt).";
        }
    }

    [TalentRegistration("druid:learn_stalk")]
    public class LearnStalkTalent : Talent
    {
        public LearnStalkTalent()
        {
            ClassId = "druid"; TreeIndex = 1; Tier = 3; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:stalk";
            DisplayName = "Stalk Training"; Description = "Unlocks Stalk (Cat stealth)."; IconName = "cloudy-fork";
        }
    }

    [TalentRegistration("druid:learn_feral_recovery")]
    public class LearnFeralRecoveryTalent : Talent
    {
        public LearnFeralRecoveryTalent()
        {
            ClassId = "druid"; TreeIndex = 1; Tier = 4; Column = 1; MaxRank = 1;
            RequiresTalent = "druid:learn_ursine_form";
            GrantsSpellId = "canrpgclasses:feral_recovery";
            DisplayName = "Feral Recovery"; Description = "Unlocks Feral Recovery (spend Rage to self-heal)."; IconName = "bull";
        }
    }

    // Raises the ceiling of the "energy" secondary pool through MaxResourceBonus. Energy is spent only in Feline Form,
    // so a Bear tank gains nothing here.
    [TalentRegistration("druid:savage_power")]
    public class SavagePowerTalent : Talent
    {
        private readonly float perRank;
        public SavagePowerTalent()
        {
            ClassId = "druid"; TreeIndex = 1; Tier = 3; Column = 2; MaxRank = 5;
            perRank = BalanceConfig.Talent("druid:savage_power").F("perRank", 8f);
            DisplayName = "Wild Vigor"; Description = "+{0} max Cat energy per rank."; IconName = "wolverine-claws";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override float MaxResourceBonus(string poolId, int rank)
            => poolId == Druid.DruidEnergy.PoolId ? perRank * rank : 0f;
    }

    [TalentRegistration("druid:wild_heart")]
    public class WildHeartTalent : Talent
    {
        private readonly float perRank;
        public WildHeartTalent()
        {
            ClassId = "druid"; TreeIndex = 1; Tier = 4; Column = 0; MaxRank = 3;
            perRank = BalanceConfig.Talent("druid:wild_heart").F("perRank", 1.0f);
            DisplayName = "Wild Heart"; Description = "+{0} max health per rank."; IconName = "diamond-hard";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MaxHealthExtraPoints, "canrpgtalent_heartofthewild", perRank * rank);
    }

    [TalentRegistration("druid:feral_stride")]
    public class FeralStrideTalent : Talent
    {
        private readonly float perRank;
        public FeralStrideTalent()
        {
            ClassId = "druid"; TreeIndex = 1; Tier = 4; Column = 2; MaxRank = 2;
            perRank = BalanceConfig.Talent("druid:feral_stride").F("perRank", 0.05f);
            DisplayName = "Feral Stride"; Description = "+{0}% movement speed per rank."; IconName = "hollow-cat";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.WalkSpeed, "canrpgtalent_feralswiftness", perRank * rank);
    }
}
