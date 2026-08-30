using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Paladin.Talents
{
    // Paladin tree 0 - Holy: healing power, mana, holy spell power.
    // Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("paladin:devout_light")]
    public class DevoutLightTalent : Talent
    {
        private readonly float perRank;
        public DevoutLightTalent()
        {
            ClassId = "paladin"; TreeIndex = 0; Tier = 0; Column = 0; MaxRank = 5;
            perRank = BalanceConfig.Talent("paladin:devout_light").F("perRank", 0.04f);
            DisplayName = "Devout Light"; Description = "+{0}% holy spell power per rank."; IconName = "tarot-14-temperance";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.SpellpowerHoly, "canrpgtalent_holydevotion", perRank * rank);
    }

    // Aura talent: strengthens the (now base) Aura of Faith's group heal. Fills the slot the learn_aura_of_faith
    // talent freed when auras became base spells. Scaled in EBAuras via the AuraRegenStrength stat.
    [TalentRegistration("paladin:zealous_faith")]
    public class ZealousFaithTalent : Talent
    {
        private readonly float perRank;
        public ZealousFaithTalent()
        {
            ClassId = "paladin"; TreeIndex = 0; Tier = 0; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("paladin:zealous_faith").F("perRank", 0.20f);
            DisplayName = "Zealous Faith"; Description = "+{0}% Aura of Faith healing per rank."; IconName = "aura";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.AuraRegenStrength, "canrpgtalent_zealousdevotion", perRank * rank);
    }

    [TalentRegistration("paladin:learn_sudden_light")]
    public class LearnSuddenLightTalent : Talent
    {
        public LearnSuddenLightTalent()
        {
            ClassId = "paladin"; TreeIndex = 0; Tier = 0; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:sudden_light";
            DisplayName = "Sudden Light Training"; Description = "Unlocks Sudden Light (fast, costly heal).";
        }
    }

    [TalentRegistration("paladin:learn_radiant_word")]
    public class LearnRadiantWordTalent : Talent
    {
        public LearnRadiantWordTalent()
        {
            ClassId = "paladin"; TreeIndex = 0; Tier = 1; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:radiant_word";
            DisplayName = "Radiant Word Training"; Description = "Unlocks Radiant Word (instant heal that spends Devotion).";
        }
    }

    [TalentRegistration("paladin:learn_cleanse")]
    public class LearnCleanseTalent : Talent
    {
        public LearnCleanseTalent()
        {
            ClassId = "paladin"; TreeIndex = 0; Tier = 1; Column = 0; MaxRank = 1;
            RequiresTalent = "paladin:devout_light";
            GrantsSpellId = "canrpgclasses:cleanse";
            DisplayName = "Cleanse Training"; Description = "Unlocks Cleanse (remove harmful effects from an ally).";
        }
    }

    [TalentRegistration("paladin:radiant_return")]
    public class RadiantReturnTalent : Talent
    {
        private readonly float perRank;
        public RadiantReturnTalent()
        {
            ClassId = "paladin"; TreeIndex = 0; Tier = 1; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("paladin:radiant_return").F("perRank", 15f);
            DisplayName = "Radiant Return"; Description = "+{0} max mana per rank."; IconName = "embrassed-energy";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override float MaxResourceBonus(string poolId, int rank)
            => poolId == "mana" ? perRank * rank : 0f;
    }

    [TalentRegistration("paladin:learn_healing_hands")]
    public class LearnHealingHandsTalent : Talent
    {
        public LearnHealingHandsTalent()
        {
            ClassId = "paladin"; TreeIndex = 0; Tier = 2; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:healing_hands";
            DisplayName = "Healing Hands Training"; Description = "Unlocks Healing Hands (large emergency heal).";
        }
    }

    [TalentRegistration("paladin:light_infusion")]
    public class LightInfusionTalent : Talent
    {
        public LightInfusionTalent()
        {
            ClassId = "paladin"; TreeIndex = 0; Tier = 3; Column = 0; MaxRank = 3;
            RequiresTalent = "paladin:devout_light";
            perRank = BalanceConfig.Talent("paladin:light_infusion").F("perRank", 0.05f);
            DisplayName = "Light Infusion"; Description = "+{0}% holy spell power per rank."; IconName = "sundial";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        private readonly float perRank;
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.SpellpowerHoly, "canrpgtalent_lightinfusion", perRank * rank);
    }

    [TalentRegistration("paladin:swift_light")]
    public class SwiftLightTalent : Talent
    {
        private readonly float perRank;
        public SwiftLightTalent()
        {
            ClassId = "paladin"; TreeIndex = 0; Tier = 2; Column = 0; MaxRank = 3;
            perRank = BalanceConfig.Talent("paladin:swift_light").F("perRank", 0.02f);
            DisplayName = "Swift Light"; Description = "-{0}% skill cooldowns per rank."; IconName = "sundial";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CooldownReduction, "canrpgtalent_swiftlight", perRank * rank);
    }

    [TalentRegistration("paladin:safe_ground")]
    public class SanctuaryTalent : Talent
    {
        private readonly float perRank;
        public SanctuaryTalent()
        {
            ClassId = "paladin"; TreeIndex = 0; Tier = 3; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("paladin:safe_ground").F("perRank", 3f);
            DisplayName = "Safe Ground"; Description = "+{0} max health per rank."; IconName = "battle-gear";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MaxHealthExtraPoints, "canrpgtalent_safe_ground", perRank * rank);
    }

    // Per-spell cast-time reduction (affects only this player - read from their own stats by EBSpellCaster).
    [TalentRegistration("paladin:quickened_light")]
    public class QuickenedLightTalent : Talent
    {
        private readonly float perRank;
        public QuickenedLightTalent()
        {
            ClassId = "paladin"; TreeIndex = 0; Tier = 2; Column = 2; MaxRank = 5;
            perRank = BalanceConfig.Talent("paladin:quickened_light").F("perRank", 0.05f);
            DisplayName = "Quickened Light"; Description = "-{0}% Blessed Light cast time per rank."; IconName = "prayer";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CastReductionFor("blessed_light"), "canrpgtalent_quickenedlight", perRank * rank);
    }

    [TalentRegistration("paladin:aura_focus")]
    public class DevotedAurasTalent : Talent
    {
        private readonly float perRank;
        public DevotedAurasTalent()
        {
            ClassId = "paladin"; TreeIndex = 0; Tier = 3; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("paladin:aura_focus").F("perRank", 0.05f);
            DisplayName = "Aura Focus"; Description = "-{0}% aura upkeep per rank."; IconName = "book-aura";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        // Read by EBAuras when charging upkeep; clamped there so it can never drop below 0.
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.AuraUpkeepReduction, "canrpgtalent_devotedauras", perRank * rank);
    }

    [TalentRegistration("paladin:learn_light_cascade")]
    public class LearnLightCascadeTalent : Talent
    {
        public LearnLightCascadeTalent()
        {
            ClassId = "paladin"; TreeIndex = 0; Tier = 4; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:light_cascade";
            DisplayName = "Light Cascade"; Description = "Unlocks Light Cascade (instant AoE heal on you and nearby allies).";
        }
    }
}
