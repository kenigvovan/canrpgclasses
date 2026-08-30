using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Paladin.Talents
{
    // Paladin tree 1 - Protection: survivability and control.
    // Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("paladin:toughness")]
    public class PaladinToughnessTalent : Talent
    {
        private readonly float perRank;
        public PaladinToughnessTalent()
        {
            ClassId = "paladin"; TreeIndex = 1; Tier = 0; Column = 0; MaxRank = 5;
            perRank = BalanceConfig.Talent("paladin:toughness").F("perRank", 3f);
            DisplayName = "Toughness"; Description = "+{0} max health per rank."; IconName = "diamond-hard";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MaxHealthExtraPoints, "canrpgtalent_pal_toughness", perRank * rank);
    }

    // Aura talent: while any of your (now base) Auras is active, YOU and EVERYONE UNDER IT take less damage. A flat
    // mitigation lever the thin Protection tree lacked. Implemented as a caster stat baked into the applied aura
    // effect by EBAuras (AuraEffectBase.sanctuaryDr), so the DR reaches every recipient, not just the paladin.
    // Feeds the shared DamageReduction stat (capped with everything else at 50% in StunPatches).
    [TalentRegistration("paladin:aura_of_refuge")]
    public class AuraOfSanctuaryTalent : Talent
    {
        private readonly float perRank;
        public AuraOfSanctuaryTalent()
        {
            ClassId = "paladin"; TreeIndex = 1; Tier = 0; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("paladin:aura_of_refuge").F("perRank", 0.03f);
            DisplayName = "Aura of Refuge"; Description = "You and party allies under your Auras take {0}% less damage per rank."; IconName = "aura";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.AuraSanctuaryDr, "canrpgtalent_aurasanctuary", perRank * rank);
    }

    [TalentRegistration("paladin:learn_hallowed_ground")]
    public class LearnHallowedGroundTalent : Talent
    {
        public LearnHallowedGroundTalent()
        {
            ClassId = "paladin"; TreeIndex = 1; Tier = 1; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:hallowed_ground";
            DisplayName = "Hallowed Ground Training"; Description = "Unlocks Hallowed Ground (holy AoE around you).";
        }
    }

    [TalentRegistration("paladin:learn_hammer_of_order")]
    public class LearnHammerOfOrderTalent : Talent
    {
        public LearnHammerOfOrderTalent()
        {
            ClassId = "paladin"; TreeIndex = 1; Tier = 1; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:hammer_of_order";
            DisplayName = "Hammer of Order Training"; Description = "Unlocks Hammer of Order (single-target stun).";
        }
    }

    // Magic mitigation: worn armor gives only a small magic ward, so talents are how a Protection paladin builds
    // real resistance.
    [TalentRegistration("paladin:faithful_ward")]
    public class FaithfulWardTalent : Talent
    {
        private readonly float perRank;
        public FaithfulWardTalent()
        {
            ClassId = "paladin"; TreeIndex = 1; Tier = 0; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("paladin:faithful_ward").F("perRank", 0.04f);
            DisplayName = "Faithful Ward"; Description = "+{0}% magic resistance per rank."; IconName = "tarot-14-temperance";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MagicResist, "canrpgtalent_faithfulward", perRank * rank);
    }

    [TalentRegistration("paladin:learn_radiant_seal")]
    public class LearnRadiantSealTalent : Talent
    {
        public LearnRadiantSealTalent()
        {
            ClassId = "paladin"; TreeIndex = 1; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:radiant_seal";
            DisplayName = "Radiant Seal Training"; Description = "Unlocks Radiant Seal (coat your weapon to weaken foes).";
        }
    }

    [TalentRegistration("paladin:guardian")]
    public class GuardianTalent : Talent
    {
        public GuardianTalent()
        {
            ClassId = "paladin"; TreeIndex = 1; Tier = 3; Column = 0; MaxRank = 3;
            perRank = BalanceConfig.Talent("paladin:guardian").F("perRank", 5f);
            DisplayName = "Guardian"; Description = "+{0} max health per rank."; IconName = "battle-gear";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        private readonly float perRank;
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MaxHealthExtraPoints, "canrpgtalent_guardian", perRank * rank);
    }

    // Ranked passive: flat damage mitigation - the tank's mitigation lever, and depth in tier 3 so the path to
    // the capstone isn't forced through Guardian alone. Read by StunPatches.Prefix_ReceiveDamage via the
    // 1.0-based canrpgDamageReduction stat (seeded in EBRpgStats).
    [TalentRegistration("paladin:stalwart")]
    public class StalwartTalent : Talent
    {
        private readonly float perRank;
        public StalwartTalent()
        {
            ClassId = "paladin"; TreeIndex = 1; Tier = 3; Column = 1; MaxRank = 5;
            perRank = BalanceConfig.Talent("paladin:stalwart").F("perRank", 0.03f);
            DisplayName = "Stalwart"; Description = "-{0}% damage taken per rank."; IconName = "leg-armor";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.DamageReduction, "canrpgtalent_stalwart", perRank * rank);
    }

    [TalentRegistration("paladin:blessed_endurance")]
    public class BlessedEnduranceTalent : Talent
    {
        private readonly float perRank;
        public BlessedEnduranceTalent()
        {
            ClassId = "paladin"; TreeIndex = 1; Tier = 1; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("paladin:blessed_endurance").F("perRank", 0.06f);
            DisplayName = "Blessed Endurance"; Description = "+{0}% healing received per rank."; IconName = "jeweled-chalice";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.HealingEffectiveness, "canrpgtalent_blessedresilience", perRank * rank);
    }

    [TalentRegistration("paladin:learn_reckoning")]
    public class LearnReckoningTalent : Talent
    {
        public LearnReckoningTalent()
        {
            ClassId = "paladin"; TreeIndex = 1; Tier = 2; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:reckoning";
            DisplayName = "Reckoning Training"; Description = "Unlocks Reckoning (taunt nearby mobs).";
        }
    }

    [TalentRegistration("paladin:learn_shield_of_faith")]
    public class LearnShieldOfFaithTalent : Talent
    {
        public LearnShieldOfFaithTalent()
        {
            ClassId = "paladin"; TreeIndex = 1; Tier = 3; Column = 2; MaxRank = 1;
            RequiresTalent = "paladin:learn_hallowed_ground";
            GrantsSpellId = "canrpgclasses:shield_of_faith";
            DisplayName = "Shield of Faith Training"; Description = "Unlocks Shield of Faith (absorb shield on an ally).";
        }
    }

    [TalentRegistration("paladin:learn_sacred_barrier")]
    public class LearnSacredBarrierTalent : Talent
    {
        public LearnSacredBarrierTalent()
        {
            ClassId = "paladin"; TreeIndex = 1; Tier = 4; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:sacred_barrier";
            DisplayName = "Sacred Barrier"; Description = "Unlocks Sacred Barrier (absorb a burst of damage).";
        }
    }
}
