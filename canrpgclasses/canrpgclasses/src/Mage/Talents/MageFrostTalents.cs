using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Mage.Talents
{
    // Mage tree 1 - Frost: control (roots, chills), the Ice Shard shatter combo, and survival (Ice Ward).
    // Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("mage:learn_ice_shard")]
    public class LearnIceShardTalent : Talent
    {
        public LearnIceShardTalent()
        {
            ClassId = "mage"; TreeIndex = 1; Tier = 0; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:ice_shard";
            DisplayName = "Ice Shard Training"; Description = "Unlocks Ice Shard (an instant frost dart that shatters frozen targets).";
        }
    }

    [TalentRegistration("mage:frost_power")]
    public class FrostPowerTalent : Talent
    {
        private readonly float perRank;
        public FrostPowerTalent()
        {
            ClassId = "mage"; TreeIndex = 1; Tier = 0; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("mage:frost_power").F("perRank", 0.03f);
            DisplayName = "Frost Power"; Description = "+{0}% frost spell power per rank."; IconName = "beveled-star";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Frost), "canrpgtalent_frostpower", perRank * rank);
    }

    [TalentRegistration("mage:learn_freezing_burst")]
    public class LearnFreezingBurstTalent : Talent
    {
        public LearnFreezingBurstTalent()
        {
            ClassId = "mage"; TreeIndex = 1; Tier = 1; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:freezing_burst";
            DisplayName = "Freezing Burst Training"; Description = "Unlocks Freezing Burst (an AoE root around you).";
        }
    }

    // Hook talent: keys off the mage's OWN root/chill markers, matching Ice Shard's shatter.
    [TalentRegistration("mage:icebreaker")]
    public class IcebreakerTalent : Talent
    {
        private readonly float perRank;
        public IcebreakerTalent()
        {
            ClassId = "mage"; TreeIndex = 1; Tier = 1; Column = 1; MaxRank = 3;
            RequiresTalent = "mage:learn_freezing_burst"; // Icebreaker rewards the Freezing Burst lockdown - gate it behind it
            perRank = BalanceConfig.Talent("mage:icebreaker").F("perRank", 0.05f);
            DisplayName = "Icebreaker"; Description = "+{0}% damage to targets you've rooted or chilled per rank."; IconName = "cracked-glass";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || attacker == null) return;
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0) return;
            bool frozen = victim.WatchedAttributes?.GetBool(CombatFlags.Rooted) == true
                          || (victim.GetBehavior<EBEffects>()?.GetEffectTier(MageEffectIds.Chilled) ?? -1) >= 0;
            if (!frozen) return;
            damage *= 1f + perRank * rank;
        };
    }

    [TalentRegistration("mage:learn_ice_storm")]
    public class LearnIceStormTalent : Talent
    {
        public LearnIceStormTalent()
        {
            ClassId = "mage"; TreeIndex = 1; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:ice_storm";
            DisplayName = "Ice Storm Training"; Description = "Unlocks Ice Storm (a lingering ice carpet that chills).";
        }
    }

    [TalentRegistration("mage:sharp_ice")]
    public class SharpIceTalent : Talent
    {
        private readonly float perRank;
        public SharpIceTalent()
        {
            ClassId = "mage"; TreeIndex = 1; Tier = 2; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("mage:sharp_ice").F("perRank", 0.05f);
            DisplayName = "Sharp Ice"; Description = "+{0}% Ice Shard damage per rank."; IconName = "wave-crest";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set("iceLanceDamage", "canrpgtalent_piercingice", perRank * rank);
    }

    // Uses the per-spell castReduction_&lt;spell&gt; stat read by EBSpellCaster.EffectiveCastDuration.
    [TalentRegistration("mage:frost_channeling")]
    public class FrostChannelingTalent : Talent
    {
        private readonly float perRank;
        public FrostChannelingTalent()
        {
            ClassId = "mage"; TreeIndex = 1; Tier = 0; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("mage:frost_channeling").F("perRank", 0.04f);
            DisplayName = "Ice Focus"; Description = "-{0}% Ice Bolt cast time per rank."; IconName = "wave-crest";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CastReductionFor("ice_bolt"), "canrpgtalent_frostchanneling", perRank * rank);
    }

    [TalentRegistration("mage:learn_ice_ward")]
    public class LearnIceWardTalent : Talent
    {
        public LearnIceWardTalent()
        {
            ClassId = "mage"; TreeIndex = 1; Tier = 3; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:ice_ward";
            DisplayName = "Ice Ward Training"; Description = "Unlocks Ice Ward (a frost absorb shield).";
        }
    }

    [TalentRegistration("mage:deep_chill")]
    public class DeepChillTalent : Talent
    {
        private readonly float perRank;
        public DeepChillTalent()
        {
            ClassId = "mage"; TreeIndex = 1; Tier = 3; Column = 0; MaxRank = 3;
            perRank = BalanceConfig.Talent("mage:deep_chill").F("perRank", 0.10f);
            DisplayName = "Deep Chill"; Description = "-{0}% Ice Ward cooldown per rank."; IconName = "surrounded-shield";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CooldownReductionFor("ice_ward"), "canrpgtalent_deep_chill", perRank * rank);
    }

    [TalentRegistration("mage:learn_frozen_shell")]
    public class LearnFrozenShellTalent : Talent
    {
        public LearnFrozenShellTalent()
        {
            ClassId = "mage"; TreeIndex = 1; Tier = 3; Column = 2; MaxRank = 1;
            RequiresTalent = "mage:learn_ice_ward";
            GrantsSpellId = "canrpgclasses:frozen_shell";
            DisplayName = "Frozen Shell Training"; Description = "Unlocks Frozen Shell (a few seconds of near-invulnerability - but you're frozen in place).";
        }
    }

    [TalentRegistration("mage:learn_icy_veins")]
    public class LearnIcyVeinsTalent : Talent
    {
        public LearnIcyVeinsTalent()
        {
            ClassId = "mage"; TreeIndex = 1; Tier = 4; Column = 1; MaxRank = 1;
            RequiresTalent = "mage:learn_ice_ward";
            GrantsSpellId = "canrpgclasses:icy_veins";
            DisplayName = "Frozen Mind"; Description = "Unlocks Frozen Mind (+frost spell power for a window).";
        }
    }
}
