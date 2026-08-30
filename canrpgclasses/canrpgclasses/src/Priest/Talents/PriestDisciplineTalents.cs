using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Priest.Talents
{
    // Priest tree 0 - Discipline: shields/absorbs, mana, utility, mitigation.
    // Tiers unlock at 0/4/8/12/16 points spent in this tree.

    [TalentRegistration("priest:learn_inner_flame")]
    public class LearnInnerFlameTalent : Talent
    {
        public LearnInnerFlameTalent()
        {
            ClassId = "priest"; TreeIndex = 0; Tier = 0; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:inner_flame";
            DisplayName = "Inner Flame Training"; Description = "Unlocks Inner Flame (a lasting minor damage-reduction buff).";
        }
    }

    [TalentRegistration("priest:dual_discipline")]
    public class DualDisciplineTalent : Talent
    {
        private readonly float perRank;
        public DualDisciplineTalent()
        {
            ClassId = "priest"; TreeIndex = 0; Tier = 0; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("priest:dual_discipline").F("perRank", 0.03f);
            DisplayName = "Dual Discipline"; Description = "+{0}% holy spell power per rank (also strengthens your shields)."; IconName = "iron-cross";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.SpellpowerHoly, "canrpgtalent_twindisciplines", perRank * rank);
    }

    [TalentRegistration("priest:nimble_mind")]
    public class NimbleMindTalent : Talent
    {
        private readonly float perRank;
        public NimbleMindTalent()
        {
            ClassId = "priest"; TreeIndex = 0; Tier = 0; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("priest:nimble_mind").F("perRank", 15f);
            DisplayName = "Nimble Mind"; Description = "+{0} max mana per rank."; IconName = "brain";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override float MaxResourceBonus(string poolId, int rank)
            => poolId == "mana" ? perRank * rank : 0f;
    }

    [TalentRegistration("priest:learn_purify")]
    public class LearnPurifyTalent : Talent
    {
        public LearnPurifyTalent()
        {
            ClassId = "priest"; TreeIndex = 0; Tier = 1; Column = 0; MaxRank = 1;
            RequiresTalent = "priest:learn_inner_flame";
            GrantsSpellId = "canrpgclasses:purify";
            DisplayName = "Purify Training"; Description = "Unlocks Purify (remove harmful effects from an ally).";
        }
    }

    [TalentRegistration("priest:soul_shelter")]
    public class SoulShelterTalent : Talent
    {
        private readonly float perRank;
        public SoulShelterTalent()
        {
            ClassId = "priest"; TreeIndex = 0; Tier = 1; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("priest:soul_shelter").F("perRank", 0.10f);
            DisplayName = "Soul Shelter"; Description = "-{0}% Warding Word cooldown per rank."; IconName = "surrounded-shield";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.CooldownReductionFor("warding_word"), "canrpgtalent_soulwarding", perRank * rank);
    }

    // Hook talent: taking damage stacks SteadyWillEffect, up to the talent's rank in stacks.
    [TalentRegistration("priest:steady_will")]
    public class SteadyWillTalent : Talent
    {
        private readonly float perRank, duration;
        public SteadyWillTalent()
        {
            ClassId = "priest"; TreeIndex = 0; Tier = 1; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("priest:steady_will").F("perRank", 0.03f);
            duration = BalanceConfig.Talent("priest:steady_will").F("duration", 8f);
            DisplayName = "Steady Will"; IconName = "cross-shield";
            Description = "Taking damage grants +{0}% damage reduction, stacking up to {1}. Each hit refreshes it.";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f), MaxRank };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            if (damage <= 0f || victim == null) return;
            int rank = TalentState.Rank(victim, Id);
            if (rank <= 0) return;
            int cur = victim.GetBehavior<EBEffects>()?.GetEffectTier(PriestEffectIds.SteadyWill) ?? 0;
            int next = System.Math.Min(rank, System.Math.Max(1, cur + 1)); // add a stack, capped at the talent's rank
            SpellExecutor.ApplyEffect(victim, PriestEffectIds.SteadyWill, next, duration);
        };
    }

    [TalentRegistration("priest:learn_suppress_pain")]
    public class LearnSuppressPainTalent : Talent
    {
        public LearnSuppressPainTalent()
        {
            ClassId = "priest"; TreeIndex = 0; Tier = 2; Column = 0; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:suppress_pain";
            DisplayName = "Suppress Pain Training"; Description = "Unlocks Suppress Pain (a strong short damage-reduction cooldown on an ally).";
        }
    }

    [TalentRegistration("priest:kindled_hope")]
    public class KindledHopeTalent : Talent
    {
        private readonly float perRank;
        public KindledHopeTalent()
        {
            ClassId = "priest"; TreeIndex = 0; Tier = 2; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("priest:kindled_hope").F("perRank", 0.05f);
            DisplayName = "Kindled Hope"; Description = "+{0}% healing received per rank."; IconName = "heptagram";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.HealingEffectiveness, "canrpgtalent_renewedhope", perRank * rank);
    }


    // Hook talent: while an absorb shield is active on you, reflect a fraction of melee damage taken.
    [TalentRegistration("priest:mirror_shield")]
    public class MirrorShieldTalent : Talent
    {
        private readonly float perRank;
        public MirrorShieldTalent()
        {
            ClassId = "priest"; TreeIndex = 0; Tier = 3; Column = 0; MaxRank = 3;
            RequiresTalent = "priest:learn_suppress_pain";
            perRank = BalanceConfig.Talent("priest:mirror_shield").F("perRank", 0.05f);
            DisplayName = "Mirror Shield"; Description = "While shielded, reflect {0}% of melee damage taken back at the attacker per rank."; IconName = "shield-echoes";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override DamageModifier? DamageModifier => (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
        {
            // Only real melee swings reflect, and only while an absorb shield is up on the victim; the reflected hit
            // is a CanrpgDamageSource (Source == Entity), so it can't re-trigger this hook on the attacker.
            if (damage <= 0f || attacker == null || source.Source != EnumDamageSource.Player
                || source is CanrpgDamageSource) return;
            int rank = TalentState.Rank(victim, Id);
            if (rank <= 0) return;
            var wa = victim.WatchedAttributes;
            if (victim.World.ElapsedMilliseconds > wa.GetLong(CombatFlags.AbsorbUntil, 0) || wa.GetFloat(CombatFlags.Absorb, 0f) <= 0f) return;
            float reflect = damage * perRank * rank;
            if (reflect <= 0f) return;
            attacker.ReceiveDamage(new CanrpgDamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = victim,
                CauseEntity = victim,
                School = SpellSchool.Holy,
                Type = DamageSchools.For(SpellSchool.Holy).EngineType
            }, reflect);
        };
    }

    [TalentRegistration("priest:meditation")]
    public class MeditationTalent : Talent
    {
        private readonly float perRank;
        public MeditationTalent()
        {
            ClassId = "priest"; TreeIndex = 0; Tier = 3; Column = 2; MaxRank = 3;
            perRank = BalanceConfig.Talent("priest:meditation").F("perRank", 0.15f);
            DisplayName = "Meditation"; Description = "+{0}% mana regeneration per rank."; IconName = "prayer";
            DescArgs = new object[] { (int)System.Math.Round(perRank * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.ResourceRegen, "canrpgtalent_meditation", perRank * rank);
    }

    [TalentRegistration("priest:bulwark")]
    public class BulwarkTalent : Talent
    {
        private readonly float perRank;
        public BulwarkTalent()
        {
            ClassId = "priest"; TreeIndex = 0; Tier = 3; Column = 1; MaxRank = 3;
            perRank = BalanceConfig.Talent("priest:bulwark").F("perRank", 3f);
            DisplayName = "Bulwark"; Description = "+{0} max health per rank."; IconName = "cross-shield";
            DescArgs = new object[] { (int)System.Math.Round(perRank) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(StatKeys.MaxHealthExtraPoints, "canrpgtalent_bulwark", perRank * rank);
    }

    [TalentRegistration("priest:learn_warding_barrier")]
    public class LearnWardingBarrierTalent : Talent
    {
        public LearnWardingBarrierTalent()
        {
            ClassId = "priest"; TreeIndex = 0; Tier = 4; Column = 1; MaxRank = 1;
            GrantsSpellId = "canrpgclasses:warding_barrier";
            DisplayName = "Warding Barrier"; Description = "Unlocks Warding Barrier (shield your whole group at once).";
        }
    }

    // The splash rider on Piercing Light / Sacred Flame stays inert until this talent sets the fraction stat.
    // Read 0-based via ReductionStat, so unlearned = 0.
    [TalentRegistration("priest:penitence")]
    public class AtonementTalent : Talent
    {
        /// <summary>Read by Piercing Light and Sacred Flame as their DamageSplashHealStat.</summary>
        public const string AtonementStat = "atonementHealFraction";

        private readonly float fraction;
        public AtonementTalent()
        {
            ClassId = "priest"; TreeIndex = 0; Tier = 4; Column = 0; MaxRank = 1;
            fraction = BalanceConfig.Talent("priest:penitence").F("fraction", 0.5f);
            DisplayName = "Penitence"; IconName = "prayer";
            Description = "Your Piercing Light and Sacred Flame heal your shielded allies for {0}% of the damage dealt.";
            DescArgs = new object[] { (int)System.Math.Round(fraction * 100f) };
        }
        public override void ApplyStats(EntityAgent e, int rank)
            => e.Stats.Set(AtonementStat, "canrpgtalent_penitence", fraction * rank);
    }
}
