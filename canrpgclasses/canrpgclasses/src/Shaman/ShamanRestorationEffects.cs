using Vintagestory.API.Common;
using effectshud.src;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Spells;

using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Shaman
{
    /// <summary>Effect ids for the Restoration tree.</summary>
    public static class ShamanRestoIds
    {
        public const string TideSurge = "canrpg_tide_surge";           // instant heal + short HoT
        public const string StoneWard = "canrpg_stone_ward";  // reactive charges: heals the bearer when hit
        public const string RisingTide = "canrpg_rising_tide";    // timed +healing power
    }

    /// <summary>Snapshotted at cast, so it keeps healing at full strength even if the shaman is dropped or swaps gear
    /// mid-HoT.</summary>
    [EffectRegistration(ShamanRestoIds.TideSurge)]
    public class TideSurgeEffect : SnapshotEffect
    {
        private readonly float coeffPerTick;
        /// <summary>Baked heal applied each tick. Public so it survives relog (OnStart doesn't re-run on deserialize).</summary>
        public float perTick;

        public TideSurgeEffect() { coeffPerTick = BalanceConfig.Spell("tide_surge").F("coeffPerTick", 0.25f); }

        protected override void Recompute()
            => perTick = casterSpellPower * coeffPerTick * (Caster?.Stats.GetBlended(StatKeys.HealingPower) ?? 1f);

        public override float DisplayMagnitude() => perTick;

        public override void OnTick()
        {
            if (perTick <= 0f) return;
            entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, perTick);
        }
    }

    /// <summary>
    /// Stone Ward: heals the bearer on each hit taken, one charge per proc (charges = effect tier, shown as
    /// HUD stacks). Caster's power is snapshotted since the effect runs on someone else's body.
    /// </summary>
    [EffectRegistration(ShamanRestoIds.StoneWard)]
    public class StoneWardEffect : SnapshotEffect
    {
        /// <summary>Baked heal per proc. Public so it survives relog (OnStart doesn't re-run on deserialize).</summary>
        public float perProc;

        private readonly float coeff;
        public StoneWardEffect() { coeff = BalanceConfig.Spell("stone_ward").F("coeff", 0.3f); }

        protected override void Recompute()
        {
            var caster = Caster;
            float healMul = caster?.Stats.GetBlended(StatKeys.HealingPower) ?? 1f;
            float shieldPower = 1f + (caster ?? entity).ReductionStat(ShamanStatKeys.ShieldPower); // Improved Shields
            perProc = casterSpellPower * coeff * healMul * shieldPower;
        }

        public override float DisplayMagnitude() => perProc;

        // Any real damage taken triggers the heal - unlike Static Shield this includes ranged and spell hits (the
        // point is to keep a focused target alive), but never a heal ticking in, and never our own proc damage.
        public override void OnShouldEntityReceiveDamage(ref float damage, DamageSource dmgSource)
        {
            if (perProc <= 0f || damage <= 0f || dmgSource == null || dmgSource.Type == EnumDamageType.Heal) return;
            if (entity?.World?.Side != EnumAppSide.Server || !entity.Alive) return;

            entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, perProc);
            ParticleSpec.Heal().Emit(entity.World, entity);
            Core.Visuals.SkillFx.RingWave(entity.World, entity.Pos.XYZ, 1.2f, SpellSchool.Nature);

            Tier--;
            var beh = entity.GetBehavior<EBEffects>();
            if (beh != null) beh.needUpdate = true; // push the new charge count to the HUD
            if (Tier <= 0) SetExpiryImmediately();
        }
    }

    [EffectRegistration(ShamanRestoIds.RisingTide)]
    public class RisingTideEffect : Effect
    {
        private readonly float boost;
        public RisingTideEffect() { boost = BalanceConfig.Spell("rising_tide").F("healingPower", 0.30f); }
        public override float DisplayMagnitude() => boost;
        public override void OnStart() => entity.Stats.Set(StatKeys.HealingPower, ShamanRestoIds.RisingTide, boost);
        public override void OnExpire() => entity.Stats.Remove(StatKeys.HealingPower, ShamanRestoIds.RisingTide);
        public override bool OnDeath() { entity.Stats.Remove(StatKeys.HealingPower, ShamanRestoIds.RisingTide); return base.OnDeath(); }
    }
}
