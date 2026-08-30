using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Talents;
using Vintagestory.API.Common;

namespace canrpgclasses.Druid.Talents
{
    // Druid tree 2 - Restoration. Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("druid:boon_of_nature")]
    public class BoonOfNatureTalent : Talent
    {
        private readonly float perRank;
        public BoonOfNatureTalent()
        {
            ClassId = "druid"; TreeIndex = 2; Tier = 0; Column = 0; MaxRank = 3;
            perRank = BalanceConfig.Talent("druid:boon_of_nature").F("perRank", 0.04f);
            DisplayName = "Boon of Nature"; Description = "+{0}% healing power per rank."; IconName = "fruiting";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.HealingPower, "canrpgtalent_giftofnature", perRank * rank);
    }

    [TalentRegistration("druid:learn_restorative_bloom")]
    public class LearnRestorativeBloomTalent : Talent
    {
        public LearnRestorativeBloomTalent()
        {
            ClassId = "druid"; TreeIndex = 2; Tier = 0; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:restorative_bloom";
            DisplayName = "Restorative Bloom Training"; Description = "Unlocks Restorative Bloom (a fast heal plus a heal-over-time)."; IconName = "leaf-swirl";
        }
    }

    [TalentRegistration("druid:nature_warding")]
    public class DruidNatureWardingTalent : Talent
    {
        private readonly float perRank;
        public DruidNatureWardingTalent()
        {
            ClassId = "druid"; TreeIndex = 2; Tier = 0; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("druid:nature_warding").F("perRank", 0.04f);
            DisplayName = "Nature Warding"; Description = "+{0}% magic resistance per rank."; IconName = "tree-face";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MagicResist, "canrpgtalent_druid_naturewarding", perRank * rank);
    }

    [TalentRegistration("druid:learn_mending_touch")]
    public class LearnMendingTouchTalent : Talent
    {
        public LearnMendingTouchTalent()
        {
            ClassId = "druid"; TreeIndex = 2; Tier = 1; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:mending_touch";
            DisplayName = "Mending Touch Training"; Description = "Unlocks Mending Touch (a slow, heavy heal)."; IconName = "linden-leaf";
        }
    }

    [TalentRegistration("druid:learn_instant_mend")]
    public class LearnInstantMendTalent : Talent
    {
        public LearnInstantMendTalent()
        {
            ClassId = "druid"; TreeIndex = 2; Tier = 1; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:instant_mend";
            DisplayName = "Instant Mend Training"; Description = "Unlocks Instant Mend (an instant emergency heal)."; IconName = "acorn";
        }
    }

    [TalentRegistration("druid:learn_spreading_bloom")]
    public class LearnSpreadingBloomTalent : Talent
    {
        public LearnSpreadingBloomTalent()
        {
            ClassId = "druid"; TreeIndex = 2; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:spreading_bloom";
            DisplayName = "Spreading Bloom Training"; Description = "Unlocks Spreading Bloom (a group heal-over-time)."; IconName = "willow-tree";
        }
    }

    [TalentRegistration("druid:learn_living_blossom")]
    public class LearnLivingBlossomTalent : Talent
    {
        public LearnLivingBlossomTalent()
        {
            ClassId = "druid"; TreeIndex = 2; Tier = 1; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:living_blossom";
            DisplayName = "Living Blossom Training"; Description = "Unlocks Living Blossom (a fast, strong single-target HoT)."; IconName = "holy-oak";
        }
    }

    [TalentRegistration("druid:learn_quickening")]
    public class LearnQuickeningTalent : Talent
    {
        public LearnQuickeningTalent()
        {
            ClassId = "druid"; TreeIndex = 2; Tier = 2; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:quickening";
            DisplayName = "Quickening"; Description = "Unlocks Quickening (your next cast-time spell is instant).";
        }
    }

    [TalentRegistration("druid:calm_spirit")]
    public class CalmSpiritTalent : Talent
    {
        private readonly float perRank;
        public CalmSpiritTalent()
        {
            ClassId = "druid"; TreeIndex = 2; Tier = 2; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("druid:calm_spirit").F("perRank", 15f);
            DisplayName = "Calm Spirit"; Description = "+{0} max mana per rank."; IconName = "meditation";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override float MaxResourceBonus(string poolId, int rank)
            => poolId == "mana" ? perRank * rank : 0f;
    }

    [TalentRegistration("druid:vital_spirit")]
    public class VitalSpiritTalent : Talent
    {
        private readonly float perRank;
        public VitalSpiritTalent()
        {
            ClassId = "druid"; TreeIndex = 2; Tier = 3; Column = 0; MaxRank = 3;
            perRank = BalanceConfig.Talent("druid:vital_spirit").F("perRank", 0.03f);
            DisplayName = "Vital Spirit"; Description = "+{0}% healing power per rank."; IconName = "pine-tree";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.HealingPower, "canrpgtalent_livingspirit", perRank * rank);
    }

    [TalentRegistration("druid:renewing_touch")]
    public class RenewingTouchTalent : Talent
    {
        private readonly float perRank;
        public RenewingTouchTalent()
        {
            ClassId = "druid"; TreeIndex = 2; Tier = 3; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("druid:renewing_touch").F("perRank", 0.06f);
            DisplayName = "Renewing Touch"; Description = "+{0}% mana regeneration per rank."; IconName = "meditation";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.ResourceRegen, "canrpgtalent_renewing_touch", perRank * rank);
    }

    // No ApplyStats - DruidHotEffectBase.Recompute reads this rank and bakes it into the HoT at cast.
    [TalentRegistration("druid:empowered_renewal")]
    public class EmpoweredRenewalTalent : Talent
    {
        public EmpoweredRenewalTalent()
        {
            ClassId = "druid"; TreeIndex = 2; Tier = 3; Column = 2; MaxRank = 3;
            float perRank = BalanceConfig.Talent("druid:empowered_renewal").F("perRank", 0.06f);
            DisplayName = "Empowered Renewal"; Description = "+{0}% healing from your heal-over-time spells per rank."; IconName = "pine-tree";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
    }

    [TalentRegistration("druid:verdant_bounty")]
    public class VerdantBountyTalent : Talent
    {
        private readonly float perRank;
        public VerdantBountyTalent()
        {
            ClassId = "druid"; TreeIndex = 2; Tier = 4; Column = 0; MaxRank = 3;
            perRank = BalanceConfig.Talent("druid:verdant_bounty").F("perRank", 0.03f);
            DisplayName = "Verdant Bounty"; Description = "-{0}% cooldown on all your spells per rank."; IconName = "oak-leaf";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CooldownReduction, "canrpgtalent_naturesbounty", perRank * rank);
    }

    [TalentRegistration("druid:boon_of_the_grove")]
    public class BoonOfTheGroveTalent : Talent
    {
        private readonly float perRank;
        public BoonOfTheGroveTalent()
        {
            ClassId = "druid"; TreeIndex = 2; Tier = 4; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("druid:boon_of_the_grove").F("perRank", 0.04f);
            DisplayName = "Boon of the Grove"; Description = "+{0}% healing power per rank."; IconName = "fruiting";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.HealingPower, "canrpgtalent_giftofearthmother", perRank * rank);
    }
}
