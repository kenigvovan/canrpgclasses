using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Paladin.Talents
{
    // Paladin tree 2 - Retribution: melee damage and offensive cooldowns.
    // Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("paladin:zealot")]
    public class ZealotTalent : Talent
    {
        private readonly float perRank;
        public ZealotTalent()
        {
            ClassId = "paladin"; TreeIndex = 2; Tier = 0; Column = 0; MaxRank = 5;
            perRank = BalanceConfig.Talent("paladin:zealot").F("perRank", 0.03f);
            DisplayName = "Zealot"; Description = "+{0}% melee weapon damage per rank."; IconName = "lion";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MeleeWeaponsDamage, "canrpgtalent_zealot", perRank * rank);
    }

    // Aura talent: while your Vengeful Aura is active, EVERYONE UNDER IT burns melee attackers for a share of the
    // hit, as Holy. Implemented as a caster stat baked into the applied Might effect by EBAuras
    // (AuraMightEffect.reflectFraction), so the reflect covers every recipient, not just the paladin.
    [TalentRegistration("paladin:retribution_vengeance")]
    public class RetributionVengeanceTalent : Talent
    {
        private readonly float perRank;
        public RetributionVengeanceTalent()
        {
            ClassId = "paladin"; TreeIndex = 2; Tier = 0; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("paladin:retribution_vengeance").F("perRank", 0.10f);
            DisplayName = "Retribution Vengeance"; Description = "You and party allies under your Vengeful Aura reflect {0}% of melee hits as Holy per rank."; IconName = "enrage";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.AuraReflectFraction, "canrpgtalent_retvengeance", perRank * rank);
    }

    [TalentRegistration("paladin:learn_sentence")]
    public class LearnSentenceTalent : Talent
    {
        public LearnSentenceTalent()
        {
            ClassId = "paladin"; TreeIndex = 2; Tier = 0; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:sentence";
            DisplayName = "Sentence Training"; Description = "Unlocks Sentence (ranged holy builder).";
        }
    }

    [TalentRegistration("paladin:zeal")]
    public class ZealTalent : Talent
    {
        private readonly float perRank;
        public ZealTalent()
        {
            ClassId = "paladin"; TreeIndex = 2; Tier = 1; Column = 0; MaxRank = 5;
            perRank = BalanceConfig.Talent("paladin:zeal").F("perRank", 0.03f);
            DisplayName = "Zeal"; Description = "+{0}% holy spell power per rank."; IconName = "enrage";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.SpellpowerHoly, "canrpgtalent_zeal", perRank * rank);
    }

    [TalentRegistration("paladin:learn_righteous_verdict")]
    public class LearnRighteousVerdictTalent : Talent
    {
        public LearnRighteousVerdictTalent()
        {
            ClassId = "paladin"; TreeIndex = 2; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:righteous_verdict";
            DisplayName = "Righteous Verdict Training"; Description = "Unlocks Righteous Verdict (Devotion finisher).";
        }
    }

    [TalentRegistration("paladin:vengeance")]
    public class VengeanceTalent : Talent
    {
        public VengeanceTalent()
        {
            ClassId = "paladin"; TreeIndex = 2; Tier = 2; Column = 1; MaxRank = 5;
            RequiresTalent = "paladin:zealot";
            perRank = BalanceConfig.Talent("paladin:vengeance").F("perRank", 0.04f);
            DisplayName = "Vengeance"; Description = "+{0}% melee weapon damage per rank."; IconName = "swords-power";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        private readonly float perRank;
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MeleeWeaponsDamage, "canrpgtalent_vengeance", perRank * rank);
    }

    [TalentRegistration("paladin:hallowed_wrath")]
    public class HallowedWrathTalent : Talent
    {
        private readonly float perRank;
        public HallowedWrathTalent()
        {
            ClassId = "paladin"; TreeIndex = 2; Tier = 3; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("paladin:hallowed_wrath").F("perRank", 0.03f);
            DisplayName = "Hallowed Wrath"; Description = "+{0}% holy spell power per rank."; IconName = "enrage";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.SpellpowerHoly, "canrpgtalent_sanctifiedwrath", perRank * rank);
    }

    [TalentRegistration("paladin:zealots_momentum")]
    public class ZealotsMomentumTalent : Talent
    {
        private readonly float perRank;
        public ZealotsMomentumTalent()
        {
            ClassId = "paladin"; TreeIndex = 2; Tier = 3; Column = 0; MaxRank = 3;
            perRank = BalanceConfig.Talent("paladin:zealots_momentum").F("perRank", 0.03f);
            DisplayName = "Zealot's Momentum"; Description = "+{0}% movement speed per rank."; IconName = "swordwoman";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.WalkSpeed, "canrpgtalent_crusadersmomentum", perRank * rank);
    }

    // Sharpens Sentence alone, through the stat SentenceSpell declares as its DamageMultiplierStat.
    [TalentRegistration("paladin:zealous_sentence")]
    public class ZealousSentenceTalent : Talent
    {
        private readonly float perRank;
        public ZealousSentenceTalent()
        {
            ClassId = "paladin"; TreeIndex = 2; Tier = 2; Column = 2; MaxRank = 3;
            RequiresTalent = "paladin:learn_sentence";
            perRank = BalanceConfig.Talent("paladin:zealous_sentence").F("perRank", 0.05f);
            DisplayName = "Zealous Sentence"; Description = "+{0}% Sentence damage per rank."; IconName = "heraldic-sun";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.JudgementDamage, "canrpgtalent_zealousjudgement", perRank * rank);
    }

    [TalentRegistration("paladin:fervor")]
    public class FervorTalent : Talent
    {
        private readonly float perRank;
        public FervorTalent()
        {
            ClassId = "paladin"; TreeIndex = 2; Tier = 1; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("paladin:fervor").F("perRank", 3f);
            DisplayName = "Fervor"; Description = "+{0} max health per rank."; IconName = "battle-gear";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MaxHealthExtraPoints, "canrpgtalent_fervor", perRank * rank);
    }

    // Read in SpellExecutor.ComputeDamage via the 1.0-based finisherDamage stat (seeded in EBRpgStats).
    [TalentRegistration("paladin:sacred_purpose")]
    public class SacredPurposeTalent : Talent
    {
        private readonly float perRank;
        public SacredPurposeTalent()
        {
            ClassId = "paladin"; TreeIndex = 2; Tier = 3; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("paladin:sacred_purpose").F("perRank", 0.04f);
            DisplayName = "Sacred Purpose"; Description = "+{0}% finisher damage per rank."; IconName = "holy-grail";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.FinisherDamage, "canrpgtalent_divinepurpose", perRank * rank);
    }

    [TalentRegistration("paladin:learn_avenging_light")]
    public class LearnAvengingLightTalent : Talent
    {
        public LearnAvengingLightTalent()
        {
            ClassId = "paladin"; TreeIndex = 2; Tier = 4; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:avenging_light";
            DisplayName = "Avenging Light"; Description = "Unlocks Avenging Light (burst of strength and speed).";
        }
    }
}
