using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Shaman.Talents
{
    // Shaman tree 1 - Enhancement. Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("shaman:learn_flame_edge")]
    public class LearnFlameEdgeTalent : Talent
    {
        public LearnFlameEdgeTalent()
        {
            ClassId = "shaman"; TreeIndex = 1; Tier = 0; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:flame_edge";
            DisplayName = "Flame Edge Training"; Description = "Unlocks Flame Edge (every swing also burns).";
        }
    }

    [TalentRegistration("shaman:thundering_strikes")]
    public class ThunderingStrikesTalent : Talent
    {
        private readonly float perRank;
        public ThunderingStrikesTalent()
        {
            ClassId = "shaman"; TreeIndex = 1; Tier = 0; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("shaman:thundering_strikes").F("perRank", 0.02f);
            DisplayName = "Thundering Strikes"; Description = "+{0}% melee weapon damage per rank."; IconName = "swords-power";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MeleeWeaponsDamage, "canrpgtalent_thunderingstrikes", perRank * rank);
    }

    [TalentRegistration("shaman:ancestral_knowledge")]
    public class AncestralKnowledgeTalent : Talent
    {
        private readonly float perRank;
        public AncestralKnowledgeTalent()
        {
            ClassId = "shaman"; TreeIndex = 1; Tier = 0; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("shaman:ancestral_knowledge").F("perRank", 15f);
            DisplayName = "Ancestral Knowledge"; Description = "+{0} max mana per rank."; IconName = "two-feathers";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override float MaxResourceBonus(string poolId, int rank)
            => poolId == "mana" ? perRank * rank : 0f;
    }

    [TalentRegistration("shaman:learn_thunder_cleave")]
    public class LearnThunderCleaveTalent : Talent
    {
        public LearnThunderCleaveTalent()
        {
            ClassId = "shaman"; TreeIndex = 1; Tier = 1; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:thunder_cleave";
            DisplayName = "Thunder Cleave Training"; Description = "Unlocks Thunder Cleave (a nature-charged swing that marks the target for your lightning).";
        }
    }

    [TalentRegistration("shaman:elemental_weapons")]
    public class ElementalWeaponsTalent : Talent
    {
        private readonly float perRank;
        public ElementalWeaponsTalent()
        {
            ClassId = "shaman"; TreeIndex = 1; Tier = 1; Column = 0; MaxRank = 3;
            RequiresTalent = "shaman:learn_flame_edge";
            perRank = BalanceConfig.Talent("shaman:elemental_weapons").F("perRank", 0.05f);
            DisplayName = "Elemental Weapons"; Description = "+{0}% weapon imbue power per rank."; IconName = "match-head";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(ShamanStatKeys.ImbuePower, "canrpgtalent_elementalweapons", perRank * rank);
    }

    [TalentRegistration("shaman:toughness")]
    public class ToughnessTalent : Talent
    {
        private readonly float perRank;
        public ToughnessTalent()
        {
            ClassId = "shaman"; TreeIndex = 1; Tier = 1; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("shaman:toughness").F("perRank", 3f);
            DisplayName = "Toughness"; Description = "+{0} max health per rank."; IconName = "diamond-hard";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MaxHealthExtraPoints, "canrpgtalent_shamantoughness", perRank * rank);
    }

    [TalentRegistration("shaman:learn_gale_edge")]
    public class LearnGaleEdgeTalent : Talent
    {
        public LearnGaleEdgeTalent()
        {
            ClassId = "shaman"; TreeIndex = 1; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:gale_edge";
            DisplayName = "Gale Edge Training"; Description = "Unlocks Gale Edge (your swings sometimes strike twice).";
        }
    }

    /// <summary>Rides the generic talent on-melee-hit hook, so the core trigger never knows about shamans.</summary>
    [TalentRegistration("shaman:storm_charge")]
    public class StormChargeTalent : Talent
    {
        private readonly float perRank;
        public StormChargeTalent()
        {
            ClassId = "shaman"; TreeIndex = 1; Tier = 2; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("shaman:storm_charge").F("perRank", 0.20f);
            DisplayName = "Storm Charge";
            Description = "{0}% chance per rank that a melee hit grants a Storm Charge stack. At {1} stacks your Arc Bolt, Forked Lightning and Mending Wave are instant and consume them.";
            IconName = "air-zigzag";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f), MaelstromEffect.Cap };
        }
        public override MeleeHitHook? OnMeleeHit => (attacker, target) =>
        {
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0 || target == null || !target.Alive) return;
            if (attacker.World.Rand.NextDouble() >= perRank * rank) return;
            MaelstromEffect.Grant(attacker);
        };
    }

    [TalentRegistration("shaman:mental_quickness")]
    public class MentalQuicknessTalent : Talent
    {
        private readonly float perRank;
        public MentalQuicknessTalent()
        {
            ClassId = "shaman"; TreeIndex = 1; Tier = 2; Column = 2; MaxRank = 2;
            perRank = BalanceConfig.Talent("shaman:mental_quickness").F("perRank", 0.02f);
            DisplayName = "Mental Quickness"; Description = "-{0}% cooldown on all your spells per rank."; IconName = "sundial";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CooldownReduction, "canrpgtalent_mentalquickness", perRank * rank);
    }

    [TalentRegistration("shaman:learn_stone_strength_totem")]
    public class LearnStoneStrengthTotemTalent : Talent
    {
        public LearnStoneStrengthTotemTalent()
        {
            ClassId = "shaman"; TreeIndex = 1; Tier = 3; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:stone_strength_totem";
            DisplayName = "Stone Strength Training"; Description = "Unlocks Stone Strength Totem (a totem that strengthens your party's swings).";
        }
    }

    [TalentRegistration("shaman:thunder_cleave_mastery")]
    public class ThunderCleaveMasteryTalent : Talent
    {
        private readonly float perRank;
        public ThunderCleaveMasteryTalent()
        {
            ClassId = "shaman"; TreeIndex = 1; Tier = 3; Column = 1; MaxRank = 3;
            RequiresTalent = "shaman:learn_thunder_cleave";
            perRank = BalanceConfig.Talent("shaman:thunder_cleave_mastery").F("perRank", 0.05f);
            DisplayName = "Thunder Cleave Mastery"; Description = "+{0}% Thunder Cleave damage per rank."; IconName = "swords-power";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(ShamanStatKeys.ThunderCleaveDamage, "canrpgtalent_stormstrikemastery", perRank * rank);
    }

    [TalentRegistration("shaman:learn_frost_edge")]
    public class LearnFrostbrandWeaponTalent : Talent
    {
        public LearnFrostbrandWeaponTalent()
        {
            ClassId = "shaman"; TreeIndex = 1; Tier = 3; Column = 2; MaxRank = 1;
            RequiresTalent = "shaman:learn_flame_edge";
            GrantsSpellId = "canrpgclasses:frost_edge";
            DisplayName = "Frost Edge Training"; Description = "Unlocks Frost Edge (your swings chill the target).";
        }
    }

    [TalentRegistration("shaman:learn_war_chant")]
    public class LearnBloodlustTalent : Talent
    {
        public LearnBloodlustTalent()
        {
            ClassId = "shaman"; TreeIndex = 1; Tier = 4; Column = 1; MaxRank = 1;
            RequiresTalent = "shaman:learn_thunder_cleave";
            GrantsSpellId = "canrpgclasses:war_chant";
            DisplayName = "War Chant"; Description = "Unlocks War Chant (a party-wide burst of speed and strength).";
        }
    }
}
