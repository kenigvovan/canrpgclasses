using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Priest.Talents
{
    // Priest tree 2 - Shadow: damage-over-time, dark direct damage, Shadow Guise.
    // Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("priest:learn_shadow_brand")]
    public class LearnShadowBrandTalent : Talent
    {
        public LearnShadowBrandTalent()
        {
            ClassId = "priest"; TreeIndex = 2; Tier = 1; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:shadow_brand";
            DisplayName = "Shadow Brand Training"; Description = "Unlocks Shadow Brand (core shadow damage-over-time).";
        }
    }

    [TalentRegistration("priest:learn_mind_rend")]
    public class LearnMindRendTalent : Talent
    {
        public LearnMindRendTalent()
        {
            ClassId = "priest"; TreeIndex = 2; Tier = 1; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:mind_rend";
            DisplayName = "Mind Rend Training"; Description = "Unlocks Mind Rend (channelled shadow ray that snares its victim).";
        }
    }

    [TalentRegistration("priest:deeper_shadow")]
    public class DeeperShadowTalent : Talent
    {
        private readonly float perRank;
        public DeeperShadowTalent()
        {
            ClassId = "priest"; TreeIndex = 2; Tier = 0; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("priest:deeper_shadow").F("perRank", 0.03f);
            DisplayName = "Deeper Shadow"; Description = "+{0}% shadow spell power per rank."; IconName = "despair";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Shadow), "canrpgtalent_deeper_shadow", perRank * rank);
    }

    // StunPatches subtracts this from the victim's shadow resist, never pushing damage past 0 resist.
    [TalentRegistration("priest:shadow_piercing")]
    public class ShadowPiercingTalent : Talent
    {
        private readonly float perRank;
        public ShadowPiercingTalent()
        {
            ClassId = "priest"; TreeIndex = 2; Tier = 3; Column = 0; MaxRank = 3;
            perRank = BalanceConfig.Talent("priest:shadow_piercing").F("perRank", 0.05f);
            DisplayName = "Shadow Piercing";
            Description = "Your shadow spells ignore {0}% of the target's shadow resistance per rank.";
            IconName = "despair";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(DamageSchools.For(SpellSchool.Shadow).PenStat, "canrpgtalent_shadowpiercing", perRank * rank);
    }

    [TalentRegistration("priest:learn_mind_shock")]
    public class LearnMindShockTalent : Talent
    {
        public LearnMindShockTalent()
        {
            ClassId = "priest"; TreeIndex = 2; Tier = 0; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:mind_shock";
            DisplayName = "Mind Shock Training"; Description = "Unlocks Mind Shock (hard-hitting shadow nuke).";
        }
    }

    // Hook talent: bonus damage against a target suffering your Shadow Brand.
    [TalentRegistration("priest:improved_brand")]
    public class ImprovedBrandTalent : Talent
    {
        private readonly float perRank;
        public ImprovedBrandTalent()
        {
            ClassId = "priest"; TreeIndex = 2; Tier = 1; Column = 1; MaxRank = 3;
            RequiresTalent = "priest:learn_shadow_brand";
            perRank = BalanceConfig.Talent("priest:improved_brand").F("perRank", 0.04f);
            DisplayName = "Improved Shadow Brand"; Description = "+{0}% damage to targets afflicted by Shadow Brand per rank."; IconName = "despair";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || attacker == null) return;
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0) return;
            if ((victim.GetBehavior<EBEffects>()?.GetEffectTier(PriestEffectIds.ShadowBrand) ?? -1) < 0) return;
            damage *= 1f + perRank * rank;
        };
    }

    [TalentRegistration("priest:learn_consuming_plague")]
    public class LearnConsumingPlagueTalent : Talent
    {
        public LearnConsumingPlagueTalent()
        {
            ClassId = "priest"; TreeIndex = 2; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:consuming_plague";
            DisplayName = "Consuming Plague Training"; Description = "Unlocks Consuming Plague (heavy shadow DoT that leeches life).";
        }
    }

    // Hook talent: your shadow damage heals you for a fraction of the damage dealt.
    [TalentRegistration("priest:siphoning_embrace")]
    public class SiphoningEmbraceTalent : Talent
    {
        private readonly float perRank;
        public SiphoningEmbraceTalent()
        {
            ClassId = "priest"; TreeIndex = 2; Tier = 2; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("priest:siphoning_embrace").F("perRank", 0.05f);
            DisplayName = "Siphoning Embrace"; Description = "Your shadow damage heals you for {0}% of the damage dealt per rank."; IconName = "carrion";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            // Only our shadow-school spell damage leeches (SW:Pain / Devouring / Mind Shock). Heals are plain
            // Internal DamageSources, so this can't re-enter off its own self-heal below.
            if (damage <= 0f || attacker == null
                || source is not canrpgclasses.Core.Spells.CanrpgDamageSource cds || cds.School != SpellSchool.Shadow) return;
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0) return;
            float heal = damage * perRank * rank;
            if (heal > 0f)
                attacker.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, heal);
        };
    }

    [TalentRegistration("priest:learn_blend")]
    public class LearnBlendTalent : Talent
    {
        public LearnBlendTalent()
        {
            ClassId = "priest"; TreeIndex = 2; Tier = 2; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:blend";
            DisplayName = "Blend Training"; Description = "Unlocks Blend (brief break-on-attack stealth to shed pursuers).";
        }
    }

    [TalentRegistration("priest:learn_dissipate")]
    public class LearnDissipateTalent : Talent
    {
        public LearnDissipateTalent()
        {
            ClassId = "priest"; TreeIndex = 2; Tier = 0; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:dissipate";
            DisplayName = "Dissipate Training"; Description = "Unlocks Dissipate (strong damage reduction plus a mana refund).";
        }
    }

    [TalentRegistration("priest:mind_erosion")]
    public class MindErosionTalent : Talent
    {
        private readonly float perRank;
        public MindErosionTalent()
        {
            ClassId = "priest"; TreeIndex = 2; Tier = 3; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("priest:mind_erosion").F("perRank", 0.05f);
            DisplayName = "Mind Erosion"; Description = "+{0}% Mind Shock damage per rank."; IconName = "chopped-skull";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(Spells.MindShockSpell.MindShockDamageStat, "canrpgtalent_mindmelt", perRank * rank);
    }

    [TalentRegistration("priest:learn_shadow_guise")]
    public class LearnShadowGuiseTalent : Talent
    {
        public LearnShadowGuiseTalent()
        {
            ClassId = "priest"; TreeIndex = 2; Tier = 4; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:shadow_guise";
            DisplayName = "Shadow Guise"; Description = "Unlocks Shadow Guise (toggle: more shadow power, less healing).";
        }
    }

    [TalentRegistration("priest:learn_terrifying_scream")]
    public class LearnTerrifyingScreamTalent : Talent
    {
        public LearnTerrifyingScreamTalent()
        {
            ClassId = "priest"; TreeIndex = 2; Tier = 3; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:terrifying_scream";
            DisplayName = "Terrifying Scream Training"; Description = "Unlocks Terrifying Scream (AoE fear - enemies flee in terror).";
        }
    }

    [TalentRegistration("priest:learn_silence")]
    public class LearnSilenceTalent : Talent
    {
        public LearnSilenceTalent()
        {
            ClassId = "priest"; TreeIndex = 2; Tier = 4; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:silence";
            DisplayName = "Silence Training"; Description = "Unlocks Silence (lock a single enemy out of casting).";
        }
    }
}
