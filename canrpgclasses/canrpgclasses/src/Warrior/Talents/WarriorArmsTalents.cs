using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Talents;

using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Warrior.Talents
{
    // Warrior tree 0 - Arms. Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("warrior:learn_charge")]
    public class LearnChargeTalent : Talent
    {
        public LearnChargeTalent()
        {
            ClassId = "warrior"; TreeIndex = 0; Tier = 0; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:charge";
            DisplayName = "Charge"; Description = "Unlocks Charge (gap-closer that builds rage).";
        }
    }

    [TalentRegistration("warrior:weapon_expertise")]
    public class WeaponExpertiseTalent : Talent
    {
        private readonly float perRank;
        public WeaponExpertiseTalent()
        {
            ClassId = "warrior"; TreeIndex = 0; Tier = 0; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("warrior:weapon_expertise").F("perRank", 0.03f);
            DisplayName = "Weapon Expertise"; Description = "+{0}% melee weapon damage per rank."; IconName = "spinning-blades";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MeleeWeaponsDamage, "canrpgtalent_weaponexpertise", perRank * rank);
    }

    // Inert until Maiming Strike is learned (Tier 2).
    [TalentRegistration("warrior:battle_sense")]
    public class TacticianTalent : Talent
    {
        private readonly float chancePerRank;
        public TacticianTalent()
        {
            ClassId = "warrior"; TreeIndex = 0; Tier = 0; Column = 2; MaxRank = 3;
            chancePerRank = BalanceConfig.Talent("warrior:battle_sense").F("chancePerRank", 0.07f);
            DisplayName = "Battle Sense"; Description = "{0}% chance per rank on a melee swing to reset Maiming Strike's cooldown."; IconName = "battle-gear";
            DescArgs = new object[] { (int)System.Math.Round(chancePerRank * 100f) };
        }
        public override MeleeHitHook? OnMeleeHit => (attacker, target) =>
        {
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0 || attacker?.World == null) return;
            if (attacker.World.Rand.NextDouble() >= chancePerRank * rank) return;
            attacker.GetBehavior<EBSpellCooldowns>()?.ClearCooldown("canrpgclasses:maiming_strike");
        };
    }

    [TalentRegistration("warrior:improved_charge")]
    public class ImprovedChargeTalent : Talent
    {
        private readonly float perRank;
        public ImprovedChargeTalent()
        {
            ClassId = "warrior"; TreeIndex = 0; Tier = 1; Column = 0; MaxRank = 2;
            RequiresTalent = "warrior:learn_charge";
            perRank = BalanceConfig.Talent("warrior:improved_charge").F("perRank", 0.10f);
            DisplayName = "Improved Charge"; Description = "-{0}% Charge cooldown per rank."; IconName = "sprint";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CooldownReductionFor("charge"), "canrpgtalent_improvedcharge", perRank * rank);
    }

    [TalentRegistration("warrior:learn_rend")]
    public class LearnRendTalent : Talent
    {
        public LearnRendTalent()
        {
            ClassId = "warrior"; TreeIndex = 0; Tier = 1; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:rend";
            DisplayName = "Bleeding Cut Training"; Description = "Unlocks Bleeding Cut (a bleed strike).";
        }
    }

    [TalentRegistration("warrior:crippling_strikes")]
    public class CripplingStrikesTalent : Talent
    {
        private readonly float perRank;
        public CripplingStrikesTalent()
        {
            ClassId = "warrior"; TreeIndex = 0; Tier = 1; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("warrior:crippling_strikes").F("perRank", 0.04f);
            DisplayName = "Crippling Strikes"; Description = "+{0}% damage to slowed targets per rank."; IconName = "achilles-heel";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || attacker == null) return;
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0) return;
            if ((victim.GetBehavior<EBEffects>()?.GetEffectTier("walkslow") ?? 0) <= 0) return;
            damage *= 1f + perRank * rank;
        };
    }

    [TalentRegistration("warrior:learn_maiming_strike")]
    public class LearnMaimingStrikeTalent : Talent
    {
        public LearnMaimingStrikeTalent()
        {
            ClassId = "warrior"; TreeIndex = 0; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:maiming_strike";
            DisplayName = "Maiming Strike Training"; Description = "Unlocks Maiming Strike (heavy blow that cuts healing).";
        }
    }

    [TalentRegistration("warrior:lasting_wounds")]
    public class LastingWoundsTalent : Talent
    {
        private readonly float perRank;
        public LastingWoundsTalent()
        {
            ClassId = "warrior"; TreeIndex = 0; Tier = 2; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("warrior:lasting_wounds").F("perRank", 0.04f);
            DisplayName = "Lasting Wounds"; Description = "+{0}% damage to bleeding targets per rank."; IconName = "dripping-blade";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || attacker == null) return;
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0) return;
            if ((victim.GetBehavior<EBEffects>()?.GetEffectTier("jagged_blades") ?? 0) <= 0) return;
            damage *= 1f + perRank * rank;
        };
    }

    [TalentRegistration("warrior:precise_strikes")]
    public class PreciseStrikesTalent : Talent
    {
        private readonly float perRank;
        public PreciseStrikesTalent()
        {
            ClassId = "warrior"; TreeIndex = 0; Tier = 2; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("warrior:precise_strikes").F("perRank", 0.05f);
            DisplayName = "Precise Strikes"; Description = "+{0}% Brutal Strike and Maiming Strike damage per rank."; IconName = "blade-fall";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
        {
            e.Stats.Set(WarriorStatKeys.BrutalDamage, "canrpgtalent_precisestrikes", perRank * rank);
            e.Stats.Set(WarriorStatKeys.MaimingDamage, "canrpgtalent_precisestrikes", perRank * rank);
        }
    }

    [TalentRegistration("warrior:learn_death_blow")]
    public class LearnDeathBlowTalent : Talent
    {
        public LearnDeathBlowTalent()
        {
            ClassId = "warrior"; TreeIndex = 0; Tier = 3; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:death_blow";
            DisplayName = "Death Blow Training"; Description = "Unlocks Death Blow (finisher that hits harder on hurt targets).";
        }
    }

    [TalentRegistration("warrior:killing_instinct")]
    public class KillingInstinctTalent : Talent
    {
        private readonly float perRank;
        private readonly float threshold;
        public KillingInstinctTalent()
        {
            ClassId = "warrior"; TreeIndex = 0; Tier = 3; Column = 0; MaxRank = 3;
            RequiresTalent = "warrior:learn_death_blow";
            var b = BalanceConfig.Talent("warrior:killing_instinct");
            perRank = b.F("perRank", 0.05f);
            threshold = b.F("threshold", 0.35f);
            DisplayName = "Killing Instinct"; Description = "+{0}% damage to targets below {1}% health per rank."; IconName = "voodoo-doll";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f), (int)System.Math.Round(threshold * 100f) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || attacker == null) return;
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0) return;
            var htree = victim.WatchedAttributes.GetTreeAttribute("health");
            if (htree == null) return;
            float max = htree.GetFloat("maxhealth");
            if (max <= 0f || htree.GetFloat("currenthealth") / max >= threshold) return;
            damage *= 1f + perRank * rank;
        };
    }

    [TalentRegistration("warrior:headsman")]
    public class HeadsmanTalent : Talent
    {
        private readonly float value;
        public HeadsmanTalent()
        {
            ClassId = "warrior"; TreeIndex = 0; Tier = 4; Column = 1; MaxRank = 1;
            RequiresTalent = "warrior:learn_death_blow"; // a CDR/damage boost for Death Blow is dead without Death Blow
            value = BalanceConfig.Talent("warrior:headsman").F("value", 0.5f);
            DisplayName = "Headsman"; Description = "+{0}% Death Blow damage."; IconName = "axe-in-stump";
            DescArgs = new object[] { (int)System.Math.Round(value * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(WarriorStatKeys.DeathBlowDamage, "canrpgtalent_headsman", value * rank);
    }

    // Marker talent: read by Charge's talent-gated Stun rider (SpellImpact.RequiresTalentId) - no stats/hook here.
    [TalentRegistration("warrior:bull_rush")]
    public class BullRushTalent : Talent
    {
        public BullRushTalent()
        {
            ClassId = "warrior"; TreeIndex = 0; Tier = 3; Column = 2; MaxRank = 1;
            RequiresTalent = "warrior:learn_charge";
            DisplayName = "Bull Rush"; Description = "Charge stuns the enemy it slams into."; IconName = "knockout";
        }
    }

    [TalentRegistration("warrior:learn_skull_rattle")]
    public class LearnSkullRattleTalent : Talent
    {
        public LearnSkullRattleTalent()
        {
            ClassId = "warrior"; TreeIndex = 0; Tier = 4; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:skull_rattle";
            DisplayName = "Skull Rattle Training"; Description = "Unlocks Skull Rattle (a stunning strike).";
        }
    }
}
