using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Shaman.Talents
{
    // Shaman tree 0 - Elemental. Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("shaman:learn_ember_shock")]
    public class LearnEmberShockTalent : Talent
    {
        public LearnEmberShockTalent()
        {
            ClassId = "shaman"; TreeIndex = 0; Tier = 0; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:ember_shock";
            DisplayName = "Ember Shock Training"; Description = "Unlocks Ember Shock (an instant hit that leaves the target burning).";
        }
    }

    [TalentRegistration("shaman:concussion")]
    public class ConcussionTalent : Talent
    {
        private readonly float perRank;
        public ConcussionTalent()
        {
            ClassId = "shaman"; TreeIndex = 0; Tier = 0; Column = 0; MaxRank = 3;
            perRank = BalanceConfig.Talent("shaman:concussion").F("perRank", 0.03f);
            DisplayName = "Concussion"; Description = "+{0}% nature spell power per rank."; IconName = "air-zigzag";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Nature), "canrpgtalent_concussion", perRank * rank);
    }

    [TalentRegistration("shaman:storm_attunement")]
    public class StormAttunementTalent : Talent
    {
        private readonly float perRank;
        public StormAttunementTalent()
        {
            ClassId = "shaman"; TreeIndex = 0; Tier = 0; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("shaman:storm_attunement").F("perRank", 8f);
            DisplayName = "Storm Attunement"; Description = "+{0} max mana per rank."; IconName = "hypersonic-bolt";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override float MaxResourceBonus(string poolId, int rank)
            => poolId == "mana" ? perRank * rank : 0f;
    }

    [TalentRegistration("shaman:learn_forked_lightning")]
    public class LearnForkedLightningTalent : Talent
    {
        public LearnForkedLightningTalent()
        {
            ClassId = "shaman"; TreeIndex = 0; Tier = 1; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:forked_lightning";
            DisplayName = "Forked Lightning Training"; Description = "Unlocks Forked Lightning (a bolt that arcs to nearby enemies).";
        }
    }

    // Keyed to the caster's OWN Ember Shock, so another shaman's DoT does not feed this bonus.
    [TalentRegistration("shaman:magma_conduit")]
    public class MagmaConduitTalent : Talent
    {
        private readonly float perRank;
        public MagmaConduitTalent()
        {
            ClassId = "shaman"; TreeIndex = 0; Tier = 1; Column = 1; MaxRank = 3;
            RequiresTalent = "shaman:learn_ember_shock";
            perRank = BalanceConfig.Talent("shaman:magma_conduit").F("perRank", 0.04f);
            DisplayName = "Magma Conduit"; Description = "+{0}% damage to targets burning under Ember Shock per rank."; IconName = "burning-dot";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || attacker == null) return;
            int rank = TalentState.Rank(attacker, Id);
            if (rank <= 0) return;
            if ((victim.GetBehavior<EBEffects>()?.GetEffectTier(ShamanEffectIds.EmberShock) ?? -1) < 0) return;
            damage *= 1f + perRank * rank;
        };
    }

    [TalentRegistration("shaman:elemental_warding")]
    public class ElementalWardingTalent : Talent
    {
        private readonly float perRank;
        public ElementalWardingTalent()
        {
            ClassId = "shaman"; TreeIndex = 0; Tier = 1; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("shaman:elemental_warding").F("perRank", 0.04f);
            DisplayName = "Elemental Warding"; Description = "+{0}% magic resistance per rank."; IconName = "diamond-hard";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        // Raw HP lives only in the Enhancement tree (Toughness); the caster/healer wards give MAGIC RESISTANCE instead.
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MagicResist, "canrpgtalent_elementalwarding", perRank * rank);
    }

    [TalentRegistration("shaman:learn_magma_burst")]
    public class LearnMagmaBurstTalent : Talent
    {
        public LearnMagmaBurstTalent()
        {
            ClassId = "shaman"; TreeIndex = 0; Tier = 2; Column = 0; MaxRank = 1;
            RequiresTalent = "shaman:learn_ember_shock"; // Magma Burst is built around the burn - gate it behind it
            GrantsSpellId = "canrpgclasses:magma_burst";
            DisplayName = "Magma Burst Training"; Description = "Unlocks Magma Burst (a heavy hit, far bigger against a burning target).";
        }
    }

    [TalentRegistration("shaman:magma_flow")]
    public class MagmaFlowTalent : Talent
    {
        private readonly float perRank;
        public MagmaFlowTalent()
        {
            ClassId = "shaman"; TreeIndex = 0; Tier = 2; Column = 1; MaxRank = 3;
            RequiresTalent = "shaman:learn_magma_burst"; // it only boosts Magma Burst - useless without the spell
            perRank = BalanceConfig.Talent("shaman:magma_flow").F("perRank", 0.05f);
            DisplayName = "Magma Flow"; Description = "+{0}% Magma Burst damage per rank."; IconName = "lava";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(ShamanStatKeys.MagmaBurstDamage, "canrpgtalent_lavaflows", perRank * rank);
    }

    [TalentRegistration("shaman:elemental_focus")]
    public class ElementalFocusTalent : Talent
    {
        private readonly float perRank;
        public ElementalFocusTalent()
        {
            ClassId = "shaman"; TreeIndex = 0; Tier = 2; Column = 2; MaxRank = 2;
            perRank = BalanceConfig.Talent("shaman:elemental_focus").F("perRank", 0.02f);
            DisplayName = "Elemental Focus"; Description = "-{0}% cooldown on all your spells per rank."; IconName = "sundial";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CooldownReduction, "canrpgtalent_elementalfocus", perRank * rank);
    }

    [TalentRegistration("shaman:learn_ember_totem")]
    public class LearnEmberTotemTalent : Talent
    {
        public LearnEmberTotemTalent()
        {
            ClassId = "shaman"; TreeIndex = 0; Tier = 3; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:ember_totem";
            DisplayName = "Ember Totem Training"; Description = "Unlocks Ember Totem (a planted fire turret).";
        }
    }

    [TalentRegistration("shaman:storm_reach")]
    public class StormReachTalent : Talent
    {
        private readonly float perRank;
        public StormReachTalent()
        {
            ClassId = "shaman"; TreeIndex = 0; Tier = 3; Column = 1; MaxRank = 3;
            RequiresTalent = "shaman:learn_forked_lightning";
            perRank = BalanceConfig.Talent("shaman:storm_reach").F("perRank", 0.05f);
            DisplayName = "Storm Reach"; Description = "+{0}% Forked Lightning damage per rank."; IconName = "air-zigzag";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(ShamanStatKeys.ForkedLightningDamage, "canrpgtalent_stormreach", perRank * rank);
    }

    [TalentRegistration("shaman:learn_tempest")]
    public class LearnTempestTalent : Talent
    {
        public LearnTempestTalent()
        {
            ClassId = "shaman"; TreeIndex = 0; Tier = 3; Column = 2; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:tempest";
            DisplayName = "Tempest Training";
            Description = "Unlocks Tempest (a nature blast that knocks every nearby enemy back).";
        }
    }

    [TalentRegistration("shaman:learn_elemental_surge")]
    public class LearnElementalSurgeTalent : Talent
    {
        public LearnElementalSurgeTalent()
        {
            ClassId = "shaman"; TreeIndex = 0; Tier = 4; Column = 1; MaxRank = 1;
            RequiresTalent = "shaman:learn_magma_burst";
            GrantsSpellId = "canrpgclasses:elemental_surge";
            DisplayName = "Elemental Surge"; Description = "Unlocks Elemental Surge (+nature and fire spell power for a window).";
        }
    }
}
