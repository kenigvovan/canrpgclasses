using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Entities;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Hunter.Talents
{
    // Hunter tree 0 - Marksmanship: ranged spell power, special shots, and shot-based perks.
    // Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("hunter:learn_careful_shot")]
    public class LearnCarefulShotTalent : Talent
    {
        public LearnCarefulShotTalent()
        {
            ClassId = "hunter"; TreeIndex = 0; Tier = 0; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:careful_shot";
            DisplayName = "Careful Shot Training"; Description = "Unlocks Careful Shot (a slow, heavy ranged nuke).";
        }
    }

    [TalentRegistration("hunter:bow_expertise")]
    public class BowExpertiseTalent : Talent
    {
        private readonly float perRank;
        public BowExpertiseTalent()
        {
            ClassId = "hunter"; TreeIndex = 0; Tier = 0; Column = 2; MaxRank = 5;
            perRank = BalanceConfig.Talent("hunter:bow_expertise").F("perRank", 0.03f);
            DisplayName = "Bow Expertise"; Description = "+{0}% bow damage per rank."; IconName = "bowman";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.RangedWeaponsDamage, "canrpgtalent_bowexpertise", perRank * rank);
    }

    // Hook talent: bonus damage against distant targets. The distance gate naturally makes this a ranged-only
    // perk (melee attackers stand on top of their target). Read in the ReceiveDamage pipeline (DamageModifiers).
    [TalentRegistration("hunter:deadeye")]
    public class DeadeyeTalent : Talent
    {
        public DeadeyeTalent()
        {
            ClassId = "hunter"; TreeIndex = 0; Tier = 1; Column = 0; MaxRank = 5;
            DisplayName = "Deadeye"; Description = "+{0}% damage to targets more than {1} blocks away per rank."; IconName = "achilles-heel";
            DescArgs = new object[] { (int)System.Math.Round(PerRank * 100f), (int)System.Math.Round(MinDistance) };
        }
        public static float PerRank => BalanceConfig.Talent("hunter:deadeye").F("perRank", 0.04f);
        public static float MinDistance => BalanceConfig.Talent("hunter:deadeye").F("minDistance", 20f);

        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || attacker == null || victim == attacker) return;
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0) return;
            if (attacker.Pos.DistanceTo(victim.Pos.XYZ) < MinDistance) return;
            damage *= 1f + PerRank * rank;
        };
    }

    [TalentRegistration("hunter:learn_split_shot")]
    public class LearnSplitShotTalent : Talent
    {
        public LearnSplitShotTalent()
        {
            ClassId = "hunter"; TreeIndex = 0; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:split_shot";
            DisplayName = "Split Shot Training"; Description = "Unlocks Split Shot (a frontal fan of arrows).";
        }
    }

    [TalentRegistration("hunter:learn_jarring_shot")]
    public class LearnJarringShotTalent : Talent
    {
        public LearnJarringShotTalent()
        {
            ClassId = "hunter"; TreeIndex = 0; Tier = 1; Column = 1; MaxRank = 1;
            RequiresTalent = "hunter:learn_careful_shot";
            GrantsSpellId = "canrpgclasses:jarring_shot";
            DisplayName = "Jarring Shot Training"; Description = "Unlocks Jarring Shot (a damaging slow).";
        }
    }

    // Hook talent: landing a hit with a real vanilla bow (an arrow) builds a combo point, so the hunter can
    // feed Finishing Shot by shooting normally, not only via Measured Shot. Excludes our own spell projectiles (they
    // already grant combo as builders) to avoid double-counting.
    [TalentRegistration("hunter:bow_combo")]
    public class BowComboTalent : Talent
    {
        public BowComboTalent()
        {
            ClassId = "hunter"; TreeIndex = 0; Tier = 2; Column = 1; MaxRank = 1;
            DisplayName = "Hunter's Rhythm"; Description = "Landing a bow shot builds a combo point."; IconName = "two-feathers";
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || attacker == null || victim == attacker) return;
            if (TalentState.Rank(attacker, Id) <= 0) return;
            // Only a real thrown/shot projectile that isn't one of our own spell shots (a vanilla arrow).
            if (source.SourceEntity is not EntityProjectile proj || proj is EntitySpellProjectile) return;
            if (SpellExecutor.IsAlly(attacker, victim)) return;
            ResourceState.AddCombo(attacker, 1);
        };
    }

    [TalentRegistration("hunter:learn_wounding_shot")]
    public class LearnWoundingShotTalent : Talent
    {
        public LearnWoundingShotTalent()
        {
            ClassId = "hunter"; TreeIndex = 0; Tier = 2; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:wounding_shot";
            DisplayName = "Wounding Shot Training"; Description = "Unlocks Wounding Shot (a shot that cuts the target's healing).";
        }
    }

    [TalentRegistration("hunter:learn_draining_shot")]
    public class LearnDrainingShotTalent : Talent
    {
        public LearnDrainingShotTalent()
        {
            ClassId = "hunter"; TreeIndex = 0; Tier = 4; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:draining_shot";
            DisplayName = "Draining Shot Training"; Description = "Unlocks Draining Shot (a shot that drains the target's resource).";
        }
    }

    [TalentRegistration("hunter:learn_incendiary_shot")]
    public class LearnIncendiaryShotTalent : Talent
    {
        public LearnIncendiaryShotTalent()
        {
            ClassId = "hunter"; TreeIndex = 0; Tier = 3; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:incendiary_shot";
            DisplayName = "Incendiary Shot Training"; Description = "Unlocks Incendiary Shot (a shot that sets the target ablaze).";
        }
    }

    // Hook talent, gated on vanilla arrows the same way BowComboTalent is.
    [TalentRegistration("hunter:precise_aim")]
    public class PreciseAimTalent : Talent
    {
        private readonly float chancePerRank;
        private readonly float critMult;
        public PreciseAimTalent()
        {
            ClassId = "hunter"; TreeIndex = 0; Tier = 3; Column = 1; MaxRank = 3;
            chancePerRank = BalanceConfig.Talent("hunter:precise_aim").F("chancePerRank", 0.08f);
            critMult = BalanceConfig.Talent("hunter:precise_aim").F("critMult", 1.5f);
            DisplayName = "Precise Aim"; Description = "Bow shots have a {0}% chance per rank to crit for +{1}% damage."; IconName = "deadly-strike";
            DescArgs = new object[] { (int)System.Math.Round(chancePerRank * 100f), (int)System.Math.Round((critMult - 1f) * 100f) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || attacker == null || victim == attacker) return;
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0) return;
            // Only a real thrown/shot projectile that isn't one of our own spell shots (a vanilla arrow).
            if (source.SourceEntity is not EntityProjectile proj || proj is EntitySpellProjectile) return;
            if (SpellExecutor.IsAlly(attacker, victim)) return;
            if (attacker.World.Rand.NextDouble() < chancePerRank * rank)
                damage *= critMult;
        };
    }

    // Skill augment (marker read by HunterShots.ApplyArm): Split Shot looses extra arrows on top of its fan.
    [TalentRegistration("hunter:bank_shot")]
    public class BankShotTalent : Talent
    {
        public BankShotTalent()
        {
            ClassId = "hunter"; TreeIndex = 0; Tier = 3; Column = 0; MaxRank = 1;
            RequiresTalent = "hunter:learn_split_shot";
            int extra = (int)BalanceConfig.Talent("hunter:bank_shot").F("extraArrows", 2f);
            DisplayName = "Bank Shot"; Description = "Split Shot fires {0} extra arrows."; IconName = "handheld-fan";
            DescArgs = new object[] { extra };
        }
    }
}
