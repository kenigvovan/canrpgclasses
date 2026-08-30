using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Talents;
using Vintagestory.API.Common;

namespace canrpgclasses.Shaman.Talents
{
    // Shaman tree 2 - Restoration. Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("shaman:learn_tide_surge")]
    public class LearnTideSurgeTalent : Talent
    {
        public LearnTideSurgeTalent()
        {
            ClassId = "shaman"; TreeIndex = 2; Tier = 0; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:tide_surge";
            DisplayName = "Tide Surge Training"; Description = "Unlocks Tide Surge (an instant heal that leaves a heal-over-time).";
        }
    }

    [TalentRegistration("shaman:purification")]
    public class PurificationTalent : Talent
    {
        private readonly float perRank;
        public PurificationTalent()
        {
            ClassId = "shaman"; TreeIndex = 2; Tier = 0; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("shaman:purification").F("perRank", 0.03f);
            DisplayName = "Purification"; Description = "+{0}% healing power per rank."; IconName = "drop";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.HealingPower, "canrpgtalent_purification", perRank * rank);
    }

    [TalentRegistration("shaman:totemic_focus")]
    public class TotemicFocusTalent : Talent
    {
        private readonly float perRank;
        public TotemicFocusTalent()
        {
            ClassId = "shaman"; TreeIndex = 2; Tier = 0; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("shaman:totemic_focus").F("perRank", 15f);
            DisplayName = "Totemic Focus"; Description = "+{0} max mana per rank."; IconName = "mountains";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override float MaxResourceBonus(string poolId, int rank)
            => poolId == "mana" ? perRank * rank : 0f;
    }

    [TalentRegistration("shaman:learn_cascading_heal")]
    public class LearnCascadingHealTalent : Talent
    {
        public LearnCascadingHealTalent()
        {
            ClassId = "shaman"; TreeIndex = 2; Tier = 1; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:cascading_heal";
            DisplayName = "Cascading Heal Training"; Description = "Unlocks Cascading Heal (a heal that bounces to your most wounded allies).";
        }
    }

    // Speeds up the group heal rather than inflating it - healing done is already covered by Purification and the
    // tree mastery.
    [TalentRegistration("shaman:tidal_rhythm")]
    public class TidalRhythmTalent : Talent
    {
        private readonly float perRank;
        public TidalRhythmTalent()
        {
            ClassId = "shaman"; TreeIndex = 2; Tier = 1; Column = 1; MaxRank = 3;
            RequiresTalent = "shaman:learn_cascading_heal";
            perRank = BalanceConfig.Talent("shaman:tidal_rhythm").F("perRank", 0.05f);
            DisplayName = "Tidal Rhythm"; Description = "-{0}% Cascading Heal cast time per rank."; IconName = "wave-crest";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CastReductionFor("cascading_heal"), "canrpgtalent_tidalwaves", perRank * rank);
    }

    [TalentRegistration("shaman:nature_warding")]
    public class NatureWardingTalent : Talent
    {
        private readonly float perRank;
        public NatureWardingTalent()
        {
            ClassId = "shaman"; TreeIndex = 2; Tier = 1; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("shaman:nature_warding").F("perRank", 0.04f);
            DisplayName = "Nature Warding"; Description = "+{0}% magic resistance per rank."; IconName = "diamond-hard";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        // Raw HP lives only in the Enhancement tree (Toughness); the caster/healer wards give MAGIC RESISTANCE instead.
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MagicResist, "canrpgtalent_naturewarding", perRank * rank);
    }

    [TalentRegistration("shaman:learn_mending_totem")]
    public class LearnMendingTotemTalent : Talent
    {
        public LearnMendingTotemTalent()
        {
            ClassId = "shaman"; TreeIndex = 2; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:mending_totem";
            DisplayName = "Mending Totem Training"; Description = "Unlocks Mending Totem (a totem that trickles healing into your party).";
        }
    }

    [TalentRegistration("shaman:improved_shields")]
    public class ImprovedShieldsTalent : Talent
    {
        private readonly float perRank;
        public ImprovedShieldsTalent()
        {
            ClassId = "shaman"; TreeIndex = 2; Tier = 2; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("shaman:improved_shields").F("perRank", 0.05f);
            DisplayName = "Improved Shields"; Description = "+{0}% Static Shield and Stone Ward proc strength per rank."; IconName = "shield-echoes";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(ShamanStatKeys.ShieldPower, "canrpgtalent_improvedshields", perRank * rank);
    }

    [TalentRegistration("shaman:tidal_mastery")]
    public class TidalMasteryTalent : Talent
    {
        private readonly float perRank;
        public TidalMasteryTalent()
        {
            ClassId = "shaman"; TreeIndex = 2; Tier = 2; Column = 2; MaxRank = 2;
            perRank = BalanceConfig.Talent("shaman:tidal_mastery").F("perRank", 0.02f);
            DisplayName = "Tidal Mastery"; Description = "-{0}% cooldown on all your spells per rank."; IconName = "sundial";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CooldownReduction, "canrpgtalent_tidalmastery", perRank * rank);
    }

    [TalentRegistration("shaman:learn_stone_ward")]
    public class LearnStoneWardTalent : Talent
    {
        public LearnStoneWardTalent()
        {
            ClassId = "shaman"; TreeIndex = 2; Tier = 3; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:stone_ward";
            DisplayName = "Stone Ward Training"; Description = "Unlocks Stone Ward (a shield on an ally that heals them when they're hit).";
        }
    }

    [TalentRegistration("shaman:water_meditation")]
    public class WaterMeditationTalent : Talent
    {
        private readonly float perRank;
        public WaterMeditationTalent()
        {
            ClassId = "shaman"; TreeIndex = 2; Tier = 3; Column = 1; MaxRank = 3;
            RequiresTalent = "shaman:totemic_focus";
            perRank = BalanceConfig.Talent("shaman:water_meditation").F("perRank", 0.15f);
            DisplayName = "Water Meditation"; Description = "+{0}% mana regen per rank."; IconName = "meditation";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.ResourceRegen, "canrpgtalent_watermeditation", perRank * rank);
    }

    [TalentRegistration("shaman:learn_spirit_purge")]
    public class LearnSpiritPurgeTalent : Talent
    {
        public LearnSpiritPurgeTalent()
        {
            ClassId = "shaman"; TreeIndex = 2; Tier = 3; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:spirit_purge";
            DisplayName = "Spirit Purge Training"; Description = "Unlocks Spirit Purge (strips every negative effect off an ally).";
        }
    }

    [TalentRegistration("shaman:learn_rising_tide")]
    public class LearnRisingTideTalent : Talent
    {
        public LearnRisingTideTalent()
        {
            ClassId = "shaman"; TreeIndex = 2; Tier = 4; Column = 1; MaxRank = 1;
            RequiresTalent = "shaman:learn_cascading_heal";
            GrantsSpellId = "canrpgclasses:rising_tide";
            DisplayName = "Rising Tide"; Description = "Unlocks Rising Tide (+healing power for a window).";
        }
    }

    [TalentRegistration("shaman:learn_wellspring_totem")]
    public class LearnWellspringTotemTalent : Talent
    {
        public LearnWellspringTotemTalent()
        {
            ClassId = "shaman"; TreeIndex = 2; Tier = 4; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:wellspring_totem";
            DisplayName = "Wellspring Totem Training";
            Description = "Unlocks Wellspring Totem (restores mana to nearby mana-using allies).";
        }
    }
}
