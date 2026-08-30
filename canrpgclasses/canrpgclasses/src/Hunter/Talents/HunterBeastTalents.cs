using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Hunter.Talents
{
    // Hunter tree 2 - Beast Mastery: strengthens the wolf companion (all hunters have one from level 1).
    // Pet-scaling talents set 1.0-based owner stats (hunterPetHealth / hunterPetDamage) read by
    // HunterPetSystem: health is baked on summon, damage is read live on each of the pet's hits.
    // Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("hunter:learn_tend_beast")]
    public class LearnTendBeastTalent : Talent
    {
        public LearnTendBeastTalent()
        {
            ClassId = "hunter"; TreeIndex = 2; Tier = 0; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:tend_beast";
            DisplayName = "Tend Beast Training"; Description = "Unlocks Tend Beast (heal your wolf).";
        }
    }

    [TalentRegistration("hunter:alpha")]
    public class AlphaTalent : Talent
    {
        private readonly float perRank;
        public AlphaTalent()
        {
            ClassId = "hunter"; TreeIndex = 2; Tier = 0; Column = 1; MaxRank = 5;
            perRank = BalanceConfig.Talent("hunter:alpha").F("perRank", 0.06f);
            DisplayName = "Alpha"; Description = "+{0}% pet health per rank."; IconName = "lion";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(HunterPetSystem.PetHealthStat, "canrpgtalent_packleader", perRank * rank);
    }

    [TalentRegistration("hunter:sharp_fangs")]
    public class SharpFangsTalent : Talent
    {
        private readonly float perRank;
        public SharpFangsTalent()
        {
            ClassId = "hunter"; TreeIndex = 2; Tier = 0; Column = 2; MaxRank = 5;
            perRank = BalanceConfig.Talent("hunter:sharp_fangs").F("perRank", 0.05f);
            DisplayName = "Sharp Fangs"; Description = "+{0}% pet damage per rank."; IconName = "neck-bite";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(HunterPetSystem.PetDamageStat, "canrpgtalent_sharpfangs", perRank * rank);
    }

    // Focus REGEN (not max): a bigger focus pool did nothing when focus was never scarce; regen is the lever that
    // actually eases the shot-spam drain. 1.0-based ResourceRegen multiplier, folded into the regen tick by EBResources.
    [TalentRegistration("hunter:beast_kinship")]
    public class BeastKinshipTalent : Talent
    {
        private readonly float perRank;
        public BeastKinshipTalent()
        {
            ClassId = "hunter"; TreeIndex = 2; Tier = 1; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("hunter:beast_kinship").F("perRank", 0.12f);
            DisplayName = "Beast Kinship"; Description = "+{0}% focus regeneration per rank."; IconName = "tarot-08-strength";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.ResourceRegen, "canrpgtalent_beastbond", perRank * rank);
    }

    [TalentRegistration("hunter:learn_beast_fury")]
    public class LearnBeastFuryTalent : Talent
    {
        public LearnBeastFuryTalent()
        {
            ClassId = "hunter"; TreeIndex = 2; Tier = 1; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:beast_fury";
            DisplayName = "Beast Fury Training"; Description = "Unlocks Beast Fury (enrage your wolf).";
        }
    }

    // Ranked pet on-hit effect (read by HunterPetSystem.ApplyBeastOnHit): the wolf's bite poisons its target,
    // applying the "poison" effect at amplifier = rank. No owner stat - the hook reads this talent's rank.
    [TalentRegistration("hunter:venom")]
    public class VenomTalent : Talent
    {
        public VenomTalent()
        {
            ClassId = "hunter"; TreeIndex = 2; Tier = 1; Column = 2; MaxRank = 3;
            RequiresTalent = "hunter:sharp_fangs";
            DisplayName = "Venom"; Description = "Your pet's bite poisons its target (stronger per rank)."; IconName = "mucous-pillar";
        }
    }

    // Ranked pet lifesteal (HunterPetSystem.ApplyBeastOnHit): the pet heals for a fraction of the damage it deals.
    [TalentRegistration("hunter:bloodletting")]
    public class BloodlettingTalent : Talent
    {
        private readonly float perRank;
        public BloodlettingTalent()
        {
            ClassId = "hunter"; TreeIndex = 2; Tier = 2; Column = 0; MaxRank = 3;
            RequiresTalent = "hunter:learn_beast_fury";
            perRank = BalanceConfig.Talent("hunter:bloodletting").F("perRank", 0.06f);
            DisplayName = "Bloodletting"; Description = "Your pet heals for {0}% of the damage it deals per rank."; IconName = "blood";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
    }

    // Ranked owner focus-fire (HunterPetSystem hook): you deal bonus damage to whatever your pet is biting.
    [TalentRegistration("hunter:pack_tactics")]
    public class PackTacticsTalent : Talent
    {
        private readonly float perRank;
        public PackTacticsTalent()
        {
            ClassId = "hunter"; TreeIndex = 2; Tier = 2; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("hunter:pack_tactics").F("perRank", 0.04f);
            DisplayName = "Pack Tactics"; Description = "+{0}% damage to your pet's current target per rank."; IconName = "dark-squad";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
    }

    // Ranked pet on-hit effect (HunterPetSystem.ApplyBeastOnHit): "walkslow" at amplifier = rank.
    [TalentRegistration("hunter:cowing_roar")]
    public class CowingRoarTalent : Talent
    {
        public CowingRoarTalent()
        {
            ClassId = "hunter"; TreeIndex = 2; Tier = 2; Column = 2; MaxRank = 3;
            DisplayName = "Cowing Roar"; Description = "Your pet's bite slows its target (stronger per rank)."; IconName = "despair";
        }
    }

    [TalentRegistration("hunter:beast_call")]
    public class BeastCallTalent : Talent
    {
        private readonly float perRank;
        public BeastCallTalent()
        {
            ClassId = "hunter"; TreeIndex = 2; Tier = 3; Column = 0; MaxRank = 3;
            perRank = BalanceConfig.Talent("hunter:beast_call").F("perRank", 0.15f);
            DisplayName = "Beast Call"; Description = "-{0}% Summon Wolf cooldown per rank."; IconName = "screaming";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CooldownReductionFor("summon_pet"), "canrpgtalent_wildcall", perRank * rank);
    }

    // The bond with the beast toughens the hunter too: +max health to the owner (not the pet).
    [TalentRegistration("hunter:kindred_spirit")]
    public class KindredSpiritTalent : Talent
    {
        private readonly float perRank;
        public KindredSpiritTalent()
        {
            ClassId = "hunter"; TreeIndex = 2; Tier = 3; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("hunter:kindred_spirit").F("perRank", 3f);
            DisplayName = "Kindred Spirit"; Description = "+{0} max health per rank."; IconName = "duality";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MaxHealthExtraPoints, "canrpgtalent_spiritbond", perRank * rank);
    }

    [TalentRegistration("hunter:beast_adept")]
    public class BeastAdeptTalent : Talent
    {
        private readonly float perRank;
        public BeastAdeptTalent()
        {
            ClassId = "hunter"; TreeIndex = 2; Tier = 4; Column = 1; MaxRank = 1;
            RequiresTalent = "hunter:learn_beast_fury";
            perRank = BalanceConfig.Talent("hunter:beast_adept").F("perRank", 0.5f);
            DisplayName = "Beast Adept"; Description = "+{0}% pet damage."; IconName = "sharp-crown";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(HunterPetSystem.PetDamageStat, "canrpgtalent_masterofbeasts", perRank * rank);
    }

    // Skill augment (marker read by HunterPetSystem.ApplyPetTarget): Beast Fury rages longer and hits harder.
    [TalentRegistration("hunter:wild_chase")]
    public class WildChaseTalent : Talent
    {
        public WildChaseTalent()
        {
            ClassId = "hunter"; TreeIndex = 2; Tier = 4; Column = 0; MaxRank = 1;
            RequiresTalent = "hunter:learn_beast_fury";
            var wc = BalanceConfig.Talent("hunter:wild_chase");
            DisplayName = "Wild Chase"; Description = "Beast Fury lasts {0}% longer and adds {1}% more pet damage."; IconName = "wyvern";
            DescArgs = new object[]
            {
                (int)System.Math.Round(wc.F("durationBonus", 0.5f) * 100f),
                (int)System.Math.Round(wc.F("damageBonus", 0.2f) * 100f)
            };
        }
    }
}
