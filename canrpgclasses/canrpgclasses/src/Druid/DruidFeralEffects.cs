using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using effectshud.src;

namespace canrpgclasses.Druid
{
    /// <summary>Cosmetic HUD marker only - the instant-cast window itself lives in EBSpellCaster.GrantInstantCast.</summary>
    [EffectRegistration(DruidEffectIds.FallingStars)]
    public class FallingStarsEffect : Effect { }

    /// <summary>Rolled on every Stinging Swarm / Lunar Flare tick. Talent-gated - no rank, no proc.</summary>
    internal static class DruidBalanceProcs
    {
        public static void RollFallingStars(Entity? caster)
        {
            if (caster?.World == null) return;
            int rank = TalentState.Rank(caster, "druid:falling_stars");
            if (rank <= 0) return;
            var t = BalanceConfig.Talent("druid:falling_stars");
            if (caster.World.Rand.NextDouble() >= t.F("procChancePerRank", 0.02f) * rank) return;
            float window = t.F("window", 8f);
            caster.GetBehavior<EBSpellCaster>()?.GrantInstantCast("*", window, DruidEffectIds.FallingStars);
            SpellExecutor.ApplyEffect(caster, DruidEffectIds.FallingStars, 1, window);
        }
    }
    [EffectRegistration(DruidEffectIds.ClawSlash, positive: false)]
    public class ClawSlashBleedEffect : SchoolDamageDotEffect
    {
        protected override SpellSchool School => SpellSchool.Nature;
        protected override float CoeffPerTick => BalanceConfig.Spell("claw_slash").F("coeffPerTick", 0.25f);
    }

    /// <summary>Per-tick matches a builder bleed; the finisher payoff is DURATION, scaled by the combo points spent
    /// (DurationPerComboPoint on the impact).</summary>
    [EffectRegistration(DruidEffectIds.DeepRend, positive: false)]
    public class DeepRendBleedEffect : SchoolDamageDotEffect
    {
        protected override SpellSchool School => SpellSchool.Nature;
        protected override float CoeffPerTick => BalanceConfig.Spell("deep_rend").F("coeffPerTick", 0.3f);
    }

    [EffectRegistration(DruidEffectIds.Gash, positive: false)]
    public class GashBleedEffect : SchoolDamageDotEffect
    {
        protected override SpellSchool School => SpellSchool.Nature;
        protected override float CoeffPerTick => BalanceConfig.Spell("gash").F("coeffPerTick", 0.2f);
    }

    [EffectRegistration(DruidEffectIds.LunarFlare, positive: false)]
    public class LunarFlareDotEffect : SchoolDamageDotEffect
    {
        protected override SpellSchool School => SpellSchool.Nature;
        protected override float CoeffPerTick => BalanceConfig.Spell("lunar_flare").F("coeffPerTick", 0.25f);
        protected override void OnDamaged(Entity? caster, float dealtThisTick) => DruidBalanceProcs.RollFallingStars(caster);
    }

    [EffectRegistration(DruidEffectIds.StingingSwarm, positive: false)]
    public class StingingSwarmDotEffect : SchoolDamageDotEffect
    {
        protected override SpellSchool School => SpellSchool.Nature;
        protected override float CoeffPerTick => BalanceConfig.Spell("stinging_swarm").F("coeffPerTick", 0.3f);
        protected override void OnDamaged(Entity? caster, float dealtThisTick) => DruidBalanceProcs.RollFallingStars(caster);
    }
}
