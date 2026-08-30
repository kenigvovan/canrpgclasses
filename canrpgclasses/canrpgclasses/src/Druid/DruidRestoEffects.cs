using effectshud.src;
using Vintagestory.API.Common;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Effects;

namespace canrpgclasses.Druid
{
    /// <summary>Castable in ANY form - the spell bypasses the form-lock and has no RequiresForm, so a caster or
    /// healing druid keeps a panic button.</summary>
    [EffectRegistration(DruidEffectIds.BarkHide)]
    public class BarkHideEffect : Effect
    {
        private readonly float dr;
        public BarkHideEffect() { dr = BalanceConfig.Spell("bark_hide").F("damageReduction", 0.20f); }
        public override void OnStart()  => entity.Stats.Set(StatKeys.DamageReduction, DruidEffectIds.BarkHide, dr);
        public override void OnExpire() => entity.Stats.Remove(StatKeys.DamageReduction, DruidEffectIds.BarkHide);
        public override bool OnDeath()  { entity.Stats.Remove(StatKeys.DamageReduction, DruidEffectIds.BarkHide); return base.OnDeath(); }
    }

    /// <summary>Base for a druid heal-over-time. Snapshots the caster's power at cast, so it keeps healing across relog.</summary>
    public abstract class DruidHotEffectBase : SnapshotEffect
    {
        /// <summary>Baked heal per tick. Public so it survives relog (OnStart doesn't re-run on deserialize).</summary>
        public float perTick;
        protected abstract float CoeffPerTick { get; }

        protected override void Recompute()
        {
            float healMul = 1f, improved = 0f;
            var caster = Caster;
            if (caster != null)
            {
                healMul = caster.Stats.GetBlended(StatKeys.HealingPower);
                // Empowered Renewal (Restoration talent): boosts every druid HoT's per-tick heal (baked at cast).
                int rank = canrpgclasses.Core.Talents.TalentState.Rank(caster, "druid:empowered_renewal");
                if (rank > 0) improved = BalanceConfig.Talent("druid:empowered_renewal").F("perRank", 0.06f) * rank;
            }
            perTick = casterSpellPower * CoeffPerTick * healMul * (1f + improved);
        }

        public override float DisplayMagnitude() => perTick;

        public override void OnTick()
        {
            if (perTick <= 0f) return;
            entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, perTick);
        }
    }

    [EffectRegistration(DruidEffectIds.VerdantRenewal)]
    public class VerdantRenewalEffect : DruidHotEffectBase
    {
        protected override float CoeffPerTick => BalanceConfig.Spell("verdant_renewal").F("coeffPerTick", 1.2f);
    }

    [EffectRegistration(DruidEffectIds.RestorativeBloom)]
    public class RestorativeBloomHotEffect : DruidHotEffectBase
    {
        protected override float CoeffPerTick => BalanceConfig.Spell("restorative_bloom").F("coeffPerTick", 1.0f);
    }

    [EffectRegistration(DruidEffectIds.SpreadingBloom)]
    public class SpreadingBloomEffect : DruidHotEffectBase
    {
        protected override float CoeffPerTick => BalanceConfig.Spell("spreading_bloom").F("coeffPerTick", 0.8f);
    }

    [EffectRegistration(DruidEffectIds.LivingBlossom)]
    public class LivingBlossomEffect : DruidHotEffectBase
    {
        protected override float CoeffPerTick => BalanceConfig.Spell("living_blossom").F("coeffPerTick", 0.9f);
    }

    /// <summary>Cosmetic HUD marker only - the instant-cast window itself lives in the GrantInstantCast impact.</summary>
    [EffectRegistration(DruidEffectIds.Quickening)]
    public class QuickeningEffect : Effect { }
}
