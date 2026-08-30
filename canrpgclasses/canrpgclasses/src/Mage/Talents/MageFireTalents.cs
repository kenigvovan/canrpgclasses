using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Mage.Talents
{
    // Mage tree 0 - Fire: burst nukes, ignite/living bomb damage-over-time, ground AoE.
    // Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("mage:learn_fire_bolt")]
    public class LearnFireBoltTalent : Talent
    {
        public LearnFireBoltTalent()
        {
            ClassId = "mage"; TreeIndex = 0; Tier = 0; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:fire_bolt";
            DisplayName = "Fire Bolt Training"; Description = "Unlocks Fire Bolt (the fire tree's core nuke).";
        }
    }

    [TalentRegistration("mage:flame_mastery")]
    public class FlameMasteryTalent : Talent
    {
        private readonly float perRank;
        public FlameMasteryTalent()
        {
            ClassId = "mage"; TreeIndex = 0; Tier = 0; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("mage:flame_mastery").F("perRank", 0.03f);
            DisplayName = "Flame Mastery"; Description = "+{0}% fire spell power per rank."; IconName = "lava";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Fire), "canrpgtalent_firepower", perRank * rank);
    }

    // Proc talent keyed on the VICTIM, so it fires when damage is dealt TO the mage.
    [TalentRegistration("mage:fire_momentum")]
    public class FireMomentumTalent : Talent
    {
        private readonly float chancePerRank, duration;
        public FireMomentumTalent()
        {
            ClassId = "mage"; TreeIndex = 0; Tier = 0; Column = 2; MaxRank = 3;
            var t = BalanceConfig.Talent("mage:fire_momentum");
            chancePerRank = t.F("perRank", 0.05f);
            duration = t.F("hasteDuration", 3f);
            DisplayName = "Fiery Momentum"; IconName = "fire-dash";
            Description = "{0}% chance per rank when you take damage to gain +{1}% move speed for {2}s.";
            DescArgs = new object[]
            {
                (int)System.Math.Round(chancePerRank * 100f),
                (int)System.Math.Round(t.F("hasteAmount", 0.15f) * 100f),
                (int)duration
            };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || victim == null) return;
            int rank = TalentState.Rank(victim, Id);
            if (rank <= 0) return;
            if (victim.World.Rand.NextDouble() >= chancePerRank * rank) return;
            SpellExecutor.ApplyEffect(victim, MageEffectIds.FieryHaste, 1, duration);
        };
    }

    [TalentRegistration("mage:learn_cinder_blast")]
    public class LearnCinderBlastTalent : Talent
    {
        public LearnCinderBlastTalent()
        {
            ClassId = "mage"; TreeIndex = 0; Tier = 1; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:cinder_blast";
            DisplayName = "Cinder Blast Training"; Description = "Unlocks Cinder Blast (heavy fire nuke plus an Ignite burn).";
        }
    }

    // Hook talent: bonus damage against a target that's burning with your Ignite.
    [TalentRegistration("mage:flame_adept")]
    public class FlameAdeptTalent : Talent
    {
        private readonly float perRank;
        public FlameAdeptTalent()
        {
            ClassId = "mage"; TreeIndex = 0; Tier = 1; Column = 1; MaxRank = 3;
            RequiresTalent = "mage:learn_cinder_blast";
            perRank = BalanceConfig.Talent("mage:flame_adept").F("perRank", 0.04f);
            DisplayName = "Flame Adept"; Description = "+{0}% damage to targets burning with Ignite per rank."; IconName = "burning-dot";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || attacker == null) return;
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0) return;
            if ((victim.GetBehavior<EBEffects>()?.GetEffectTier(MageEffectIds.Ignite) ?? -1) < 0) return;
            damage *= 1f + perRank * rank;
        };
    }

    [TalentRegistration("mage:learn_volatile_ember")]
    public class LearnVolatileEmberTalent : Talent
    {
        public LearnVolatileEmberTalent()
        {
            ClassId = "mage"; TreeIndex = 0; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:volatile_ember";
            DisplayName = "Volatile Ember Training"; Description = "Unlocks Volatile Ember (a fire DoT that explodes when it ends).";
        }
    }

    [TalentRegistration("mage:firebrand")]
    public class FirebrandTalent : Talent
    {
        private readonly float perRank;
        public FirebrandTalent()
        {
            ClassId = "mage"; TreeIndex = 0; Tier = 2; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("mage:firebrand").F("perRank", 0.05f);
            DisplayName = "Firebrand"; Description = "+{0}% Cinder Blast damage per rank."; IconName = "match-head";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(Spells.CinderBlastSpell.CinderBlastDamageStat, "canrpgtalent_firebrand", perRank * rank);
    }

    [TalentRegistration("mage:learn_clear_mind")]
    public class LearnClearMindTalent : Talent
    {
        public LearnClearMindTalent()
        {
            ClassId = "mage"; TreeIndex = 0; Tier = 2; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:clear_mind";
            DisplayName = "Clear Mind"; Description = "Unlocks Clear Mind (your next cast is instant).";
        }
    }

    [TalentRegistration("mage:learn_flame_field")]
    public class LearnFlameFieldTalent : Talent
    {
        public LearnFlameFieldTalent()
        {
            ClassId = "mage"; TreeIndex = 0; Tier = 3; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:flame_field";
            DisplayName = "Flame Field Training"; Description = "Unlocks Flame Field (a lingering fire carpet).";
        }
    }

    // Hook talent (no ApplyStats): covers nukes, DoTs and the ground zone alike.
    [TalentRegistration("mage:fire_storm")]
    public class FireStormTalent : Talent
    {
        private readonly float perRank;
        public FireStormTalent()
        {
            ClassId = "mage"; TreeIndex = 0; Tier = 3; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("mage:fire_storm").F("perRank", 0.05f);
            DisplayName = "Fire Storm"; Description = "+{0}% fire damage per rank."; IconName = "wind-hole";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || attacker == null
                || source is not canrpgclasses.Core.Spells.CanrpgDamageSource cds || cds.School != SpellSchool.Fire) return;
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0) return;
            damage *= 1f + perRank * rank;
        };
    }

    [TalentRegistration("mage:learn_conflagration")]
    public class LearnCombustionTalent : Talent
    {
        public LearnCombustionTalent()
        {
            ClassId = "mage"; TreeIndex = 0; Tier = 4; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:conflagration";
            DisplayName = "Conflagration"; Description = "Unlocks Conflagration (burst cooldown: +fire spell power for a window).";
        }
    }
}
