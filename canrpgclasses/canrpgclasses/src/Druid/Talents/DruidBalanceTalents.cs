using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using Vintagestory.API.Common;

namespace canrpgclasses.Druid.Talents
{
    // Druid tree 0 - Balance. Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("druid:starlight")]
    public class StarlightTalent : Talent
    {
        private readonly float perRank;
        public StarlightTalent()
        {
            ClassId = "druid"; TreeIndex = 0; Tier = 0; Column = 0; MaxRank = 3;
            perRank = BalanceConfig.Talent("druid:starlight").F("perRank", 0.03f);
            DisplayName = "Starlight"; Description = "+{0}% nature and astral spell power per rank."; IconName = "star-cycle";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
        {
            e.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Nature), "canrpgtalent_starlight_n", perRank * rank);
            e.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Arcane), "canrpgtalent_starlight_a", perRank * rank);
        }
    }

    [TalentRegistration("druid:learn_star_lance")]
    public class LearnStarLanceTalent : Talent
    {
        public LearnStarLanceTalent()
        {
            ClassId = "druid"; TreeIndex = 0; Tier = 0; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:star_lance";
            DisplayName = "Star Lance Training"; Description = "Unlocks Star Lance (a heavy astral nuke).";
        }
    }

    [TalentRegistration("druid:wildheart_warding")]
    public class WildheartWardingTalent : Talent
    {
        private readonly float perRank;
        public WildheartWardingTalent()
        {
            ClassId = "druid"; TreeIndex = 0; Tier = 0; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("druid:wildheart_warding").F("perRank", 0.04f);
            DisplayName = "Wildheart Warding"; Description = "+{0}% magic resistance per rank."; IconName = "wizard-staff";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MagicResist, "canrpgtalent_wildheartwarding", perRank * rank);
    }

    [TalentRegistration("druid:learn_astral_burst")]
    public class LearnAstralBurstTalent : Talent
    {
        public LearnAstralBurstTalent()
        {
            ClassId = "druid"; TreeIndex = 0; Tier = 1; Column = 1; MaxRank = 1;
            RequiresTalent = "druid:learn_star_lance";
            GrantsSpellId = "canrpgclasses:astral_burst";
            DisplayName = "Astral Burst Training"; Description = "Unlocks Astral Burst (an instant astral burst).";
        }
    }

    [TalentRegistration("druid:natures_flow")]
    public class NaturesFlowTalent : Talent
    {
        private readonly float perRank;
        public NaturesFlowTalent()
        {
            ClassId = "druid"; TreeIndex = 0; Tier = 1; Column = 0; MaxRank = 2;
            perRank = BalanceConfig.Talent("druid:natures_flow").F("perRank", 0.04f);
            DisplayName = "Nature's Flow"; Description = "-{0}% cooldown on all your spells per rank."; IconName = "oak-leaf";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CooldownReduction, "canrpgtalent_naturesgrace", perRank * rank);
    }

    [TalentRegistration("druid:learn_stinging_swarm")]
    public class LearnStingingSwarmTalent : Talent
    {
        public LearnStingingSwarmTalent()
        {
            ClassId = "druid"; TreeIndex = 0; Tier = 1; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:stinging_swarm";
            DisplayName = "Stinging Swarm Training"; Description = "Unlocks Stinging Swarm (a second nature DoT)."; IconName = "tree-beehive";
        }
    }

    [TalentRegistration("druid:learn_raging_winds")]
    public class LearnRagingWindsTalent : Talent
    {
        public LearnRagingWindsTalent()
        {
            ClassId = "druid"; TreeIndex = 0; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:raging_winds";
            DisplayName = "Raging Winds Training"; Description = "Unlocks Raging Winds (a channeled AoE storm)."; IconName = "flower-twirl";
        }
    }

    [TalentRegistration("druid:learn_wind_blast")]
    public class LearnWindBlastTalent : Talent
    {
        public LearnWindBlastTalent()
        {
            ClassId = "druid"; TreeIndex = 0; Tier = 2; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:wind_blast";
            DisplayName = "Wind Blast Training"; Description = "Unlocks Wind Blast (a knockback nova)."; IconName = "fluffy-cloud";
        }
    }

    [TalentRegistration("druid:lunar_might")]
    public class LunarMightTalent : Talent
    {
        private readonly float perRank;
        public LunarMightTalent()
        {
            ClassId = "druid"; TreeIndex = 0; Tier = 2; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("druid:lunar_might").F("perRank", 0.03f);
            DisplayName = "Lunar Might"; Description = "+{0}% nature and astral spell power per rank."; IconName = "moon";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
        {
            e.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Nature), "canrpgtalent_moonfury_n", perRank * rank);
            e.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Arcane), "canrpgtalent_moonfury_a", perRank * rank);
        }
    }

    [TalentRegistration("druid:waking_dream")]
    public class WakingDreamTalent : Talent
    {
        private readonly float perRank;
        public WakingDreamTalent()
        {
            ClassId = "druid"; TreeIndex = 0; Tier = 3; Column = 0; MaxRank = 3;
            perRank = BalanceConfig.Talent("druid:waking_dream").F("perRank", 0.06f);
            DisplayName = "Waking Dream"; Description = "+{0}% mana regeneration per rank."; IconName = "meditation";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.ResourceRegen, "canrpgtalent_waking_dream", perRank * rank);
    }

    // Magic PENETRATION, not to be confused with Wildheart Warding's defensive magic RESISTANCE.
    [TalentRegistration("druid:skyward_focus")]
    public class SkywardFocusTalent : Talent
    {
        private readonly float perRank;
        public SkywardFocusTalent()
        {
            ClassId = "druid"; TreeIndex = 0; Tier = 3; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("druid:skyward_focus").F("perRank", 0.03f);
            DisplayName = "Skyward Focus"; Description = "+{0}% magic penetration per rank."; IconName = "star-cycle";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MagicPen, "canrpgtalent_celestialfocus", perRank * rank);
    }

    [TalentRegistration("druid:moonlit_insight")]
    public class LunarGuidanceTalent : Talent
    {
        private readonly float perRank;
        public LunarGuidanceTalent()
        {
            ClassId = "druid"; TreeIndex = 0; Tier = 3; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("druid:moonlit_insight").F("perRank", 15f);
            DisplayName = "Moonlit Insight"; Description = "+{0} max mana per rank."; IconName = "moon";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override float MaxResourceBonus(string poolId, int rank)
            => poolId == "mana" ? perRank * rank : 0f;
    }

    // Proc talent with no ApplyStats - DruidBalanceProcs.RollFallingStars reads this rank on every DoT tick.
    [TalentRegistration("druid:falling_stars")]
    public class ShootingStarsTalent : Talent
    {
        public ShootingStarsTalent()
        {
            ClassId = "druid"; TreeIndex = 0; Tier = 4; Column = 1; MaxRank = 3;
            float chance = BalanceConfig.Talent("druid:falling_stars").F("procChancePerRank", 0.02f);
            DisplayName = "Falling Stars"; Description = "Your Stinging Swarm and Lunar Flare ticks have a {0}% chance per rank to make your next cast-time spell instant."; IconName = "star-satellites";
            DescArgs = new object[] { (int)System.Math.Round(chance * 100f) };
        }
    }

}
