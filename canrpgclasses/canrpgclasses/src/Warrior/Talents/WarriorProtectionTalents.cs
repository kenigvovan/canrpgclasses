using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Warrior.Talents
{
    // Warrior tree 2 - Protection. Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("warrior:learn_taunt")]
    public class LearnTauntTalent : Talent
    {
        public LearnTauntTalent()
        {
            ClassId = "warrior"; TreeIndex = 2; Tier = 0; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:taunt";
            DisplayName = "Taunt Training"; Description = "Unlocks Taunt (force nearby mobs to attack you).";
        }
    }

    [TalentRegistration("warrior:toughness")]
    public class ToughnessTalent : Talent
    {
        private readonly float perRank;
        public ToughnessTalent()
        {
            ClassId = "warrior"; TreeIndex = 2; Tier = 0; Column = 1; MaxRank = 5;
            perRank = BalanceConfig.Talent("warrior:toughness").F("perRank", 3f);
            DisplayName = "Toughness"; Description = "+{0} maximum health per rank."; IconName = "mountaintop";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MaxHealthExtraPoints, "canrpgtalent_toughness", perRank * rank);
    }

    [TalentRegistration("warrior:ironhide")]
    public class IronhideTalent : Talent
    {
        private readonly float perRank;
        public IronhideTalent()
        {
            ClassId = "warrior"; TreeIndex = 2; Tier = 0; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("warrior:ironhide").F("perRank", 0.03f);
            DisplayName = "Ironhide"; Description = "+{0}% damage reduction per rank."; IconName = "cross-shield";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.DamageReduction, "canrpgtalent_ironskin", perRank * rank);
    }

    [TalentRegistration("warrior:watchful_guard")]
    public class WatchfulGuardTalent : Talent
    {
        private readonly float perRank;
        public WatchfulGuardTalent()
        {
            ClassId = "warrior"; TreeIndex = 2; Tier = 1; Column = 0; MaxRank = 2;
            RequiresTalent = "warrior:learn_taunt";
            perRank = BalanceConfig.Talent("warrior:watchful_guard").F("perRank", 0.15f);
            DisplayName = "Watchful Guard"; Description = "-{0}% Taunt cooldown per rank."; IconName = "bell-shield";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CooldownReductionFor("taunt"), "canrpgtalent_watchful_guard", perRank * rank);
    }

    [TalentRegistration("warrior:learn_ground_tremor")]
    public class LearnGroundTremorTalent : Talent
    {
        public LearnGroundTremorTalent()
        {
            ClassId = "warrior"; TreeIndex = 2; Tier = 1; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:ground_tremor";
            DisplayName = "Ground Tremor Training"; Description = "Unlocks Ground Tremor (AoE damage and slow).";
        }
    }

    // Marker talent with no stats or hook of its own - WarriorRage reads its rank when granting rage from damage taken.
    [TalentRegistration("warrior:shield_handling")]
    public class ShieldHandlingTalent : Talent
    {
        public ShieldHandlingTalent()
        {
            ClassId = "warrior"; TreeIndex = 2; Tier = 1; Column = 2; MaxRank = 3;
            float perRank = BalanceConfig.Talent("warrior:shield_handling").F("perRank", 0.15f);
            DisplayName = "Shield Handling"; Description = "+{0}% rage from taking damage per rank."; IconName = "viking-shield";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
    }

    [TalentRegistration("warrior:learn_full_guard")]
    public class LearnFullGuardTalent : Talent
    {
        public LearnFullGuardTalent()
        {
            ClassId = "warrior"; TreeIndex = 2; Tier = 2; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:full_guard";
            DisplayName = "Full Guard Training"; Description = "Unlocks Full Guard (burst of damage reduction).";
        }
    }

    [TalentRegistration("warrior:payback")]
    public class PaybackTalent : Talent
    {
        private readonly float perRank;
        public PaybackTalent()
        {
            ClassId = "warrior"; TreeIndex = 2; Tier = 2; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("warrior:payback").F("perRank", 0.05f);
            DisplayName = "Payback"; Description = "Reflect {0}% of melee damage taken back at the attacker per rank."; IconName = "surrounded-shield";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            // Only real melee swings reflect (Source == Player), never our own spell damage - and the reflected hit
            // is a CanrpgDamageSource (Source == Entity), so it can't re-trigger this hook on the attacker.
            if (damage <= 0f || attacker == null || source.Source != EnumDamageSource.Player
                || source is CanrpgDamageSource) return;
            int rank = TalentState.Rank(victim, Id);
            if (rank <= 0) return;
            float reflect = damage * perRank * rank;
            if (reflect <= 0f) return;
            attacker.ReceiveDamage(new CanrpgDamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = victim,
                CauseEntity = victim,
                School = SpellSchool.PhysicalMelee,
                Type = DamageSchools.For(SpellSchool.PhysicalMelee).EngineType
            }, reflect);
        };
    }

    // Inert until Shield Strike is learned.
    [TalentRegistration("warrior:retort")]
    public class RetortTalent : Talent
    {
        private readonly float chancePerRank, icd;
        public RetortTalent()
        {
            ClassId = "warrior"; TreeIndex = 2; Tier = 2; Column = 0; MaxRank = 3;
            var b = BalanceConfig.Talent("warrior:retort");
            chancePerRank = b.F("chancePerRank", 0.10f);
            icd = b.F("icd", 4f); // ICD so a tank under a stream of hits can't reset Shield Strike every swing
            DisplayName = "Retort"; Description = "When you take a hit, {0}% chance per rank to reset Shield Strike's cooldown (once per {1}s)."; IconName = "surrounded-shield";
            DescArgs = new object[] { (int)System.Math.Round(chancePerRank * 100f), (int)System.Math.Round(icd) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            // Real incoming hit from an attacker, not our own reflect/spell damage; the proc only clears a cooldown.
            if (damage <= 0f || attacker == null || source is CanrpgDamageSource
                || (source.Source != EnumDamageSource.Player && source.Source != EnumDamageSource.Entity)) return;
            int rank = TalentState.Rank(victim, Id);
            if (rank <= 0 || victim?.World == null) return;
            if (WarriorStatKeys.OnIcd(victim, WarriorStatKeys.RevengeIcdMs, icd)) return;
            // Only spend the roll/ICD when Shield Strike is actually on cooldown - otherwise the reset is a no-op.
            var cd = victim.GetBehavior<EBSpellCooldowns>();
            if (cd == null || !cd.IsOnCooldown("canrpgclasses:shield_strike")) return;
            if (victim.World.Rand.NextDouble() >= chancePerRank * rank) return;
            WarriorStatKeys.ArmIcd(victim, WarriorStatKeys.RevengeIcdMs, icd);
            cd.ClearCooldown("canrpgclasses:shield_strike");
        };
    }

    [TalentRegistration("warrior:learn_final_stand")]
    public class LearnFinalStandTalent : Talent
    {
        public LearnFinalStandTalent()
        {
            ClassId = "warrior"; TreeIndex = 2; Tier = 3; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:final_stand";
            DisplayName = "Final Stand Training"; Description = "Unlocks Final Stand (emergency absorb barrier).";
        }
    }

    // Applied in the damage pipeline before the flat DR cap, so it stacks past 50%.
    [TalentRegistration("warrior:shield_rhythm")]
    public class ShieldRhythmTalent : Talent
    {
        private readonly float perRank;
        public ShieldRhythmTalent()
        {
            ClassId = "warrior"; TreeIndex = 2; Tier = 3; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("warrior:shield_rhythm").F("perRank", 0.03f);
            DisplayName = "Shield Rhythm"; Description = "-{0}% damage taken in Guarded Stance per rank."; IconName = "swordwoman";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f) return;
            int rank = TalentState.Rank(victim, Id);
            if (rank <= 0) return;
            if (victim.WatchedAttributes.GetString(AttrKeys.ActiveAura, "") != WarriorRage.DefensiveStanceId) return;
            damage *= Math.Max(0f, 1f - perRank * rank);
        };
    }

    [TalentRegistration("warrior:learn_shield_guard")]
    public class LearnShieldGuardTalent : Talent
    {
        public LearnShieldGuardTalent()
        {
            ClassId = "warrior"; TreeIndex = 2; Tier = 3; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:shield_guard";
            DisplayName = "Shield Guard Training"; Description = "Unlocks Shield Guard (short-cooldown active mitigation).";
        }
    }

    [TalentRegistration("warrior:unbreakable")]
    public class UnbreakableTalent : Talent
    {
        private readonly float value;
        public UnbreakableTalent()
        {
            ClassId = "warrior"; TreeIndex = 2; Tier = 4; Column = 1; MaxRank = 1;
            RequiresTalent = "warrior:learn_full_guard"; // a Full Guard CDR capstone is dead without Full Guard
            value = BalanceConfig.Talent("warrior:unbreakable").F("value", 0.5f);
            DisplayName = "Unbreakable"; Description = "-{0}% Full Guard cooldown."; IconName = "diamond-hard";
            DescArgs = new object[] { (int)System.Math.Round(value * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CooldownReductionFor("full_guard"), "canrpgtalent_unbreakable", value * rank);
    }

    [TalentRegistration("warrior:learn_armor_break")]
    public class LearnArmorBreakTalent : Talent
    {
        public LearnArmorBreakTalent()
        {
            ClassId = "warrior"; TreeIndex = 2; Tier = 4; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:armor_break";
            DisplayName = "Armor Break Training"; Description = "Unlocks Armor Break (stacking vulnerability).";
        }
    }

    [TalentRegistration("warrior:learn_shield_strike")]
    public class LearnShieldStrikeTalent : Talent
    {
        public LearnShieldStrikeTalent()
        {
            ClassId = "warrior"; TreeIndex = 2; Tier = 4; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:shield_strike";
            DisplayName = "Shield Strike Training"; Description = "Unlocks Shield Strike (a hard shield bash).";
        }
    }
}
