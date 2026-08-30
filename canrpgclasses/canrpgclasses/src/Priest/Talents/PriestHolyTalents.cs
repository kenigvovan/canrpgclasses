using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Priest.Talents
{
    // Priest tree 1 - Holy: pure healing (throughput, cast speed, survivability).
    // Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("priest:learn_swift_heal")]
    public class LearnSwiftHealTalent : Talent
    {
        public LearnSwiftHealTalent()
        {
            ClassId = "priest"; TreeIndex = 1; Tier = 0; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:swift_heal";
            DisplayName = "Swift Heal Training"; Description = "Unlocks Swift Heal (fast, costly single-target heal).";
        }
    }

    [TalentRegistration("priest:devout_healing")]
    public class DevoutHealingTalent : Talent
    {
        private readonly float perRank;
        public DevoutHealingTalent()
        {
            ClassId = "priest"; TreeIndex = 1; Tier = 0; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("priest:devout_healing").F("perRank", 0.03f);
            DisplayName = "Devout Healing"; Description = "+{0}% healing done per rank."; IconName = "prayer";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.HealingPower, "canrpgtalent_spiritualhealing", perRank * rank);
    }

    [TalentRegistration("priest:light_specialization")]
    public class LightSpecializationTalent : Talent
    {
        private readonly float perRank;
        public LightSpecializationTalent()
        {
            ClassId = "priest"; TreeIndex = 1; Tier = 0; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("priest:light_specialization").F("perRank", 0.03f);
            DisplayName = "Light Specialization"; Description = "+{0}% holy spell power per rank."; IconName = "heraldic-sun";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.SpellpowerHoly, "canrpgtalent_holyspec", perRank * rank);
    }

    [TalentRegistration("priest:learn_deep_heal")]
    public class LearnDeepHealTalent : Talent
    {
        public LearnDeepHealTalent()
        {
            ClassId = "priest"; TreeIndex = 1; Tier = 1; Column = 0; MaxRank = 1;
            RequiresTalent = "priest:learn_swift_heal";
            GrantsSpellId = "canrpgclasses:deep_heal";
            DisplayName = "Deep Heal Training"; Description = "Unlocks Deep Heal (big, efficient single-target heal).";
        }
    }

    /// <summary>Marker talent (no stat/grant of its own): <see cref="SoothingPrayerEffect"/> reads its rank on the caster and
    /// scales Soothing Prayer's per-tick heal by +{perRank} per rank.</summary>
    [TalentRegistration("priest:lasting_renew")]
    public class LastingRenewTalent : Talent
    {
        public LastingRenewTalent()
        {
            ClassId = "priest"; TreeIndex = 1; Tier = 1; Column = 1; MaxRank = 3;
            float perRank = BalanceConfig.Talent("priest:lasting_renew").F("perRank", 0.10f);
            DisplayName = "Lasting Renew"; Description = "+{0}% Soothing Prayer healing per rank."; IconName = "bandaged";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
    }

    [TalentRegistration("priest:quiet_grace")]
    public class QuietGraceTalent : Talent
    {
        private readonly float perRank;
        public QuietGraceTalent()
        {
            ClassId = "priest"; TreeIndex = 1; Tier = 1; Column = 2; MaxRank = 2;
            perRank = BalanceConfig.Talent("priest:quiet_grace").F("perRank", 0.10f);
            DisplayName = "Quiet Grace"; Description = "-{0}% Deep Heal cast time per rank."; IconName = "sundial";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CastReductionFor("deep_heal"), "canrpgtalent_quiet_grace", perRank * rank);
    }

    [TalentRegistration("priest:learn_sacred_flame")]
    public class LearnSacredFlameTalent : Talent
    {
        public LearnSacredFlameTalent()
        {
            ClassId = "priest"; TreeIndex = 1; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:sacred_flame";
            DisplayName = "Sacred Flame Training"; Description = "Unlocks Sacred Flame (holy bolt plus a burning damage-over-time).";
        }
    }

    [TalentRegistration("priest:reach_of_light")]
    public class ReachOfLightTalent : Talent
    {
        private readonly float perRank;
        public ReachOfLightTalent()
        {
            ClassId = "priest"; TreeIndex = 1; Tier = 2; Column = 1; MaxRank = 2;
            perRank = BalanceConfig.Talent("priest:reach_of_light").F("perRank", 0.02f);
            DisplayName = "Reach of Light"; Description = "-{0}% skill cooldowns per rank."; IconName = "sunbeams";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CooldownReduction, "canrpgtalent_holyreach", perRank * rank);
    }

    [TalentRegistration("priest:learn_communal_prayer")]
    public class LearnCommunalPrayerTalent : Talent
    {
        public LearnCommunalPrayerTalent()
        {
            ClassId = "priest"; TreeIndex = 1; Tier = 3; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:communal_prayer";
            DisplayName = "Communal Prayer Training"; Description = "Unlocks Communal Prayer (group AoE heal).";
        }
    }

    [TalentRegistration("priest:learn_sheltering_spirit")]
    public class LearnShelteringSpiritTalent : Talent
    {
        public LearnShelteringSpiritTalent()
        {
            ClassId = "priest"; TreeIndex = 1; Tier = 3; Column = 1; MaxRank = 1;
            RequiresTalent = "priest:devout_healing";
            GrantsSpellId = "canrpgclasses:sheltering_spirit";
            DisplayName = "Sheltering Spirit Training"; Description = "Unlocks Sheltering Spirit (greatly boost the healing an ally receives).";
        }
    }

    [TalentRegistration("priest:learn_sacred_hymn")]
    public class LearnSacredHymnTalent : Talent
    {
        public LearnSacredHymnTalent()
        {
            ClassId = "priest"; TreeIndex = 1; Tier = 4; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:sacred_hymn";
            DisplayName = "Sacred Hymn"; Description = "Unlocks Sacred Hymn (powerful channelled group heal).";
        }
    }
}
