using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Talents;
using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Warrior.Talents
{
    // Warrior tree 1 - Fury. Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("warrior:learn_war_cry")]
    public class LearnWarCryTalent : Talent
    {
        public LearnWarCryTalent()
        {
            ClassId = "warrior"; TreeIndex = 1; Tier = 0; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:war_cry";
            DisplayName = "War Cry Training"; Description = "Unlocks War Cry (party melee-strength buff).";
        }
    }

    [TalentRegistration("warrior:boiling_wrath")]
    public class BoilingWrathTalent : Talent
    {
        private readonly float perRank;
        public BoilingWrathTalent()
        {
            ClassId = "warrior"; TreeIndex = 1; Tier = 0; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("warrior:boiling_wrath").F("perRank", 1f);
            DisplayName = "Boiling Wrath"; Description = "+{0} rage per melee swing per rank."; IconName = "enrage";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override MeleeHitHook? OnMeleeHit => (attacker, target) =>
        {
            int rank = TalentState.Rank(attacker, Id);
            if (rank > 0) WarriorRage.AddRage(attacker, perRank * rank);
        };
    }

    [TalentRegistration("warrior:fleet_of_foot")]
    public class FleetOfFootTalent : Talent
    {
        private readonly float perRank;
        public FleetOfFootTalent()
        {
            ClassId = "warrior"; TreeIndex = 1; Tier = 0; Column = 2; MaxRank = 2;
            perRank = BalanceConfig.Talent("warrior:fleet_of_foot").F("perRank", 0.03f);
            DisplayName = "Fleet of Foot"; Description = "+{0}% movement speed per rank."; IconName = "sprint";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.WalkSpeed, "canrpgtalent_fleetoffoot", perRank * rank);
    }

    [TalentRegistration("warrior:learn_fearsome_shout")]
    public class LearnFearsomeShoutTalent : Talent
    {
        public LearnFearsomeShoutTalent()
        {
            ClassId = "warrior"; TreeIndex = 1; Tier = 1; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:fearsome_shout";
            DisplayName = "Fearsome Shout Training"; Description = "Unlocks Fearsome Shout (weakens nearby enemies).";
        }
    }

    [TalentRegistration("warrior:blood_rush")]
    public class BloodRushTalent : Talent
    {
        private readonly float perRank;
        private readonly float threshold;
        public BloodRushTalent()
        {
            ClassId = "warrior"; TreeIndex = 1; Tier = 1; Column = 1; MaxRank = 3;
            var b = BalanceConfig.Talent("warrior:blood_rush");
            perRank = b.F("perRank", 0.04f);
            threshold = b.F("threshold", 0.5f);
            DisplayName = "Blood Rush"; Description = "+{0}% damage while below {1}% health per rank."; IconName = "blood";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f), (int)System.Math.Round(threshold * 100f) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || attacker == null) return;
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0) return;
            var htree = attacker.WatchedAttributes.GetTreeAttribute("health");
            if (htree == null) return;
            float max = htree.GetFloat("maxhealth");
            if (max <= 0f || htree.GetFloat("currenthealth") / max >= threshold) return;
            damage *= 1f + perRank * rank;
        };
    }

    [TalentRegistration("warrior:rage_reservoir")]
    public class RageReservoirTalent : Talent
    {
        private readonly float perRank;
        public RageReservoirTalent()
        {
            ClassId = "warrior"; TreeIndex = 1; Tier = 1; Column = 2; MaxRank = 2;
            RequiresTalent = "warrior:blood_rush";
            perRank = BalanceConfig.Talent("warrior:rage_reservoir").F("perRank", 10f);
            DisplayName = "Rage Reservoir"; Description = "+{0} maximum rage per rank."; IconName = "lava";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override float MaxResourceBonus(string poolId, int rank)
            => poolId == "rage" ? perRank * rank : 0f;
    }

    [TalentRegistration("warrior:learn_sweeping_blades")]
    public class LearnSweepingBladesTalent : Talent
    {
        public LearnSweepingBladesTalent()
        {
            ClassId = "warrior"; TreeIndex = 1; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:sweeping_blades";
            DisplayName = "Sweeping Blades Training"; Description = "Unlocks Sweeping Blades (AoE melee sweep).";
        }
    }

    [TalentRegistration("warrior:bloodied_vigor")]
    public class BloodiedVigorTalent : Talent
    {
        private readonly float perRank;
        public BloodiedVigorTalent()
        {
            ClassId = "warrior"; TreeIndex = 1; Tier = 2; Column = 1; MaxRank = 3;
            RequiresTalent = "warrior:blood_rush";
            perRank = BalanceConfig.Talent("warrior:bloodied_vigor").F("perRank", 0.02f);
            DisplayName = "Bloodied Vigor"; Description = "Melee swings heal you for {0}% of the damage dealt per rank."; IconName = "blade-bite";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            // Only real weapon swings lifesteal (Source == Player), never our own spell damage (CanrpgDamageSource)
            // - the guard also stops the self-heal below from re-entering this hook (heals are Source == Internal).
            if (damage <= 0f || attacker == null || source.Source != EnumDamageSource.Player
                || source is canrpgclasses.Core.Spells.CanrpgDamageSource) return;
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0) return;
            float heal = damage * perRank * rank;
            if (heal > 0f)
                attacker.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, heal);
        };
    }

    [TalentRegistration("warrior:learn_wild_rage")]
    public class LearnWildRageTalent : Talent
    {
        public LearnWildRageTalent()
        {
            ClassId = "warrior"; TreeIndex = 1; Tier = 3; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:wild_rage";
            DisplayName = "Wild Rage Training"; Description = "Unlocks Wild Rage (break crowd control, gain rage).";
        }
    }

    [TalentRegistration("warrior:frenzy")]
    public class FrenzyTalent : Talent
    {
        private readonly float chancePerRank, duration, icd;
        public FrenzyTalent()
        {
            ClassId = "warrior"; TreeIndex = 1; Tier = 3; Column = 0; MaxRank = 3;
            var b = BalanceConfig.Talent("warrior:frenzy");
            chancePerRank = b.F("chancePerRank", 0.10f);
            duration = b.F("duration", 6f);
            icd = b.F("enrageIcd", 10f); // internal cooldown so Enrage can't reach permanent uptime
            float dmg = b.F("enrageDamage", 0.15f);
            DisplayName = "Enrage"; IconName = "enrage";
            Description = "{0}% chance per rank on a melee swing to Enrage: +{1}% melee damage and move speed for {2}s (can't retrigger for {3}s).";
            DescArgs = new object[]
            {
                (int)System.Math.Round(chancePerRank * 100f),
                (int)System.Math.Round(dmg * 100f),
                (int)System.Math.Round(duration),
                (int)System.Math.Round(icd)
            };
        }
        public override MeleeHitHook? OnMeleeHit => (attacker, target) =>
        {
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0 || attacker?.World == null) return;
            if (WarriorStatKeys.OnIcd(attacker, WarriorStatKeys.EnrageIcdMs, icd)) return;
            if (attacker.World.Rand.NextDouble() >= chancePerRank * rank) return;
            WarriorStatKeys.ArmIcd(attacker, WarriorStatKeys.EnrageIcdMs, icd);
            SpellExecutor.ApplyEffect(attacker, EnrageEffectId.Id, 1, duration);
        };
    }

    [TalentRegistration("warrior:learn_gore")]
    public class LearnGoreTalent : Talent
    {
        public LearnGoreTalent()
        {
            ClassId = "warrior"; TreeIndex = 1; Tier = 2; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:gore";
            DisplayName = "Gore Training"; Description = "Unlocks Gore (a strike that heals you).";
        }
    }

    [TalentRegistration("warrior:learn_all_out_attack")]
    public class LearnRecklessnessTalent : Talent
    {
        public LearnRecklessnessTalent()
        {
            ClassId = "warrior"; TreeIndex = 1; Tier = 4; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:all_out_attack";
            DisplayName = "All-Out Attack"; Description = "Unlocks All-Out Attack (burst of melee strength and rage).";
        }
    }

    [TalentRegistration("warrior:learn_leaping_charge")]
    public class LearnHeroicLeapTalent : Talent
    {
        public LearnHeroicLeapTalent()
        {
            ClassId = "warrior"; TreeIndex = 1; Tier = 4; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:leaping_charge";
            DisplayName = "Leaping Charge Training"; Description = "Unlocks Leaping Charge (a long repositioning jump).";
        }
    }

    [TalentRegistration("warrior:reckless_fury")]
    public class DeathwishTalent : Talent
    {
        private readonly float value;
        public DeathwishTalent()
        {
            ClassId = "warrior"; TreeIndex = 1; Tier = 4; Column = 0; MaxRank = 1;
            RequiresTalent = "warrior:frenzy"; // amplifies Enrage - dead without it
            value = BalanceConfig.Talent("warrior:reckless_fury").F("value", 0.25f);
            DisplayName = "Reckless Fury"; IconName = "enrage";
            Description = "While Enraged, +{0}% melee damage.";
            DescArgs = new object[] { (int)System.Math.Round(value * 100f) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || attacker == null) return;
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0) return;
            if ((attacker.GetBehavior<EBEffects>()?.GetEffectTier(EnrageEffectId.Id) ?? -1) < 0) return;
            damage *= 1f + value;
        };
    }
}
