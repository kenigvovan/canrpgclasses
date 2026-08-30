using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using Vintagestory.API.Common;

namespace canrpgclasses.Mage.Talents
{
    // Mage tree 2 - Arcane: high-tempo burst nukes, mana sustain (Mana Draw), and the utility CC (Transmute,
    // Spell Break). Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("mage:learn_mystic_blast")]
    public class LearnMysticBlastTalent : Talent
    {
        public LearnMysticBlastTalent()
        {
            ClassId = "mage"; TreeIndex = 2; Tier = 0; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:mystic_blast";
            DisplayName = "Mystic Blast Training"; Description = "Unlocks Mystic Blast (the arcane tree's core nuke).";
        }
    }

    [TalentRegistration("mage:mystic_focus")]
    public class MysticFocusTalent : Talent
    {
        private readonly float perRank;
        public MysticFocusTalent()
        {
            ClassId = "mage"; TreeIndex = 2; Tier = 0; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("mage:mystic_focus").F("perRank", 0.03f);
            DisplayName = "Mystic Focus"; Description = "+{0}% arcane spell power per rank."; IconName = "ursa-major";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Arcane), "canrpgtalent_arcanefocus", perRank * rank);
    }

    [TalentRegistration("mage:insight")]
    public class InsightTalent : Talent
    {
        private readonly float perRank;
        public InsightTalent()
        {
            ClassId = "mage"; TreeIndex = 2; Tier = 0; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("mage:insight").F("perRank", 15f);
            DisplayName = "Insight"; Description = "+{0} max mana per rank."; IconName = "embrassed-energy";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override float MaxResourceBonus(string poolId, int rank)
            => poolId == "mana" ? perRank * rank : 0f;
    }

    [TalentRegistration("mage:learn_mystic_barrage")]
    public class LearnMysticBarrageTalent : Talent
    {
        public LearnMysticBarrageTalent()
        {
            ClassId = "mage"; TreeIndex = 2; Tier = 1; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:mystic_barrage";
            DisplayName = "Mystic Barrage Training"; Description = "Unlocks Mystic Barrage (an instant arcane filler).";
        }
    }

    [TalentRegistration("mage:mystic_meditation")]
    public class MysticMeditationTalent : Talent
    {
        private readonly float perRank;
        public MysticMeditationTalent()
        {
            ClassId = "mage"; TreeIndex = 2; Tier = 1; Column = 2; MaxRank = 3;
            RequiresTalent = "mage:insight";
            perRank = BalanceConfig.Talent("mage:mystic_meditation").F("perRank", 0.15f);
            DisplayName = "Mystic Meditation"; Description = "+{0}% mana regen per rank."; IconName = "swirl-string";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.ResourceRegen, "canrpgtalent_arcanemeditation", perRank * rank);
    }

    [TalentRegistration("mage:learn_mystic_burst")]
    public class LearnMysticBurstTalent : Talent
    {
        public LearnMysticBurstTalent()
        {
            ClassId = "mage"; TreeIndex = 2; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:mystic_burst";
            DisplayName = "Mystic Burst Training"; Description = "Unlocks Mystic Burst (a panic AoE around you).";
        }
    }

    [TalentRegistration("mage:mystic_instability")]
    public class MysticInstabilityTalent : Talent
    {
        private readonly float perRank;
        public MysticInstabilityTalent()
        {
            ClassId = "mage"; TreeIndex = 2; Tier = 2; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("mage:mystic_instability").F("perRank", 0.05f);
            DisplayName = "Mystic Instability"; Description = "+{0}% Mystic Blast damage per rank."; IconName = "double-ringed-orb";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(Spells.MysticBlastSpell.MysticBlastDamageStat, "canrpgtalent_arcaneinstability", perRank * rank);
    }

    [TalentRegistration("mage:mystic_subtlety")]
    public class MysticSubtletyTalent : Talent
    {
        private readonly float perRank;
        public MysticSubtletyTalent()
        {
            ClassId = "mage"; TreeIndex = 2; Tier = 2; Column = 2; MaxRank = 2;
            perRank = BalanceConfig.Talent("mage:mystic_subtlety").F("perRank", 0.02f);
            DisplayName = "Mystic Subtlety"; Description = "-{0}% skill cooldowns per rank."; IconName = "swirl-string";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CooldownReduction, "canrpgtalent_arcanesubtlety", perRank * rank);
    }

    [TalentRegistration("mage:learn_mana_draw")]
    public class LearnManaDrawTalent : Talent
    {
        public LearnManaDrawTalent()
        {
            ClassId = "mage"; TreeIndex = 2; Tier = 3; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:mana_draw";
            DisplayName = "Mana Draw Training"; Description = "Unlocks Mana Draw (a large mana refund cooldown).";
        }
    }

    // Generic magic penetration (all three schools). StunPatches subtracts it from the victim's resist before
    // the cap, never pushing damage past 0 resist.
    [TalentRegistration("mage:spell_penetration")]
    public class SpellPenetrationTalent : Talent
    {
        private readonly float perRank;
        public SpellPenetrationTalent()
        {
            ClassId = "mage"; TreeIndex = 2; Tier = 3; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("mage:spell_penetration").F("perRank", 0.05f);
            DisplayName = "Spell Penetration";
            Description = "Your spells ignore {0}% of the target's magic resistance per rank.";
            IconName = "double-ringed-orb";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MagicPen, "canrpgtalent_spellpen", perRank * rank);
    }

    [TalentRegistration("mage:learn_transmute")]
    public class LearnTransmuteTalent : Talent
    {
        public LearnTransmuteTalent()
        {
            ClassId = "mage"; TreeIndex = 2; Tier = 1; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:transmute";
            DisplayName = "Transmute Training"; Description = "Unlocks Transmute (turn an enemy into a harmless sheep).";
        }
    }

    [TalentRegistration("mage:learn_spell_break")]
    public class LearnSpellBreakTalent : Talent
    {
        public LearnSpellBreakTalent()
        {
            ClassId = "mage"; TreeIndex = 2; Tier = 3; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:spell_break";
            DisplayName = "Spell Break Training"; Description = "Unlocks Spell Break (silence and interrupt an enemy).";
        }
    }

    [TalentRegistration("mage:learn_mystic_surge")]
    public class LearnMysticSurgeTalent : Talent
    {
        public LearnMysticSurgeTalent()
        {
            ClassId = "mage"; TreeIndex = 2; Tier = 4; Column = 1; MaxRank = 1;
            RequiresTalent = "mage:mystic_instability";
            GrantsSpellId = "canrpgclasses:mystic_surge";
            DisplayName = "Mystic Surge"; Description = "Unlocks Mystic Surge (+arcane spell power for a window).";
        }
    }
}
