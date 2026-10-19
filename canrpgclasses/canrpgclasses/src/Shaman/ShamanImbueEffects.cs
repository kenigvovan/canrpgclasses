using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using effectshud.src;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Spells;

using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Shaman
{
    /// <summary>Weapon imbues: long self-buffs riding <see cref="Effect.DidAttack"/> with snapshotted spell power.</summary>
    public static class ShamanImbueIds
    {
        public const string FlameEdge = "canrpg_imbue_flametongue";
        public const string FrostEdge = "canrpg_imbue_frostbrand";
        public const string GaleEdge = "canrpg_imbue_windfury";

        /// <summary>Mutually exclusive: applying one strips the others.</summary>
        public static readonly string[] All = { FlameEdge, FrostEdge, GaleEdge };
    }

    /// <summary>Rider scales with snapshotted spell power and <see cref="ShamanStatKeys.ImbuePower"/>.</summary>
    public abstract class ImbueEffectBase : SnapshotEffect
    {
        /// <summary>Public so it survives relog (OnStart doesn't re-run on deserialize).</summary>
        public float perHit;

        protected abstract float Coeff { get; }

        protected override void Recompute()
            => perHit = casterSpellPower * Coeff * (1f + entity.ReductionStat(ShamanStatKeys.ImbuePower));

        public override float DisplayMagnitude() => perHit;

        public override void OnStart()
        {
            base.OnStart(); // bakes perHit
            foreach (var id in ShamanImbueIds.All)
                if (id != effectTypeId) SpellExecutor.RemoveEffect(entity, id);
        }

        /// <summary>Rejects our own spell damage so the rider can't loop on itself.</summary>
        protected bool ShouldRide(DamageSource source, EntityAgent target)
            => perHit > 0f && target != null && target != entity && target.Alive
               && entity?.World?.Side == EnumAppSide.Server
               && source is not CanrpgDamageSource;

        /// <summary>Separate hit of the imbue's school on top of the physical swing; pierces the swing's i-frames
        /// like EBSpellCaster.TakeEmpoweredMeleeBonus.</summary>
        protected void StrikeElemental(EntityAgent target, SpellSchool school)
        {
            var def = DamageSchools.For(school);
            target.ReceiveDamage(new CanrpgDamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = entity,
                CauseEntity = entity,
                School = school,
                Type = def.EngineType,
                IgnoreInvFrames = true,
                TicksPerDuration = 2
            }, perHit);
            ParticleSpec.ForSchool(school).Emit(entity.World, target);
        }
    }

    [EffectRegistration(ShamanImbueIds.FlameEdge)]
    public class FlameEdgeImbueEffect : ImbueEffectBase
    {
        protected override float Coeff => BalanceConfig.Spell("flame_edge").F("coeff", 0.35f);

        public override void DidAttack(DamageSource source, EntityAgent targetEntity, ref EnumHandling handled)
        {
            if (!ShouldRide(source, targetEntity)) return;
            StrikeElemental(targetEntity, SpellSchool.Fire);
        }
    }

    [EffectRegistration(ShamanImbueIds.FrostEdge)]
    public class FrostEdgeImbueEffect : ImbueEffectBase
    {
        protected override float Coeff => BalanceConfig.Spell("frost_edge").F("coeff", 0.20f);

        public override void DidAttack(DamageSource source, EntityAgent targetEntity, ref EnumHandling handled)
        {
            if (!ShouldRide(source, targetEntity)) return;
            StrikeElemental(targetEntity, SpellSchool.Frost);
            // Generic walkslow, not the mage chill, so it doesn't enable Ice Shard shatter.
            SpellExecutor.ApplyEffect(targetEntity, "walkslow", 1, BalanceConfig.Spell("frost_edge").F("slowDuration", 2f));
        }
    }

    /// <summary>The extra swing uses spell power, since the weapon's damage is unreachable from an effect.</summary>
    [EffectRegistration(ShamanImbueIds.GaleEdge)]
    public class GaleEdgeImbueEffect : ImbueEffectBase
    {
        protected override float Coeff => BalanceConfig.Spell("gale_edge").F("coeff", 0.75f);

        public override void DidAttack(DamageSource source, EntityAgent targetEntity, ref EnumHandling handled)
        {
            if (!ShouldRide(source, targetEntity)) return;
            float chance = BalanceConfig.Spell("gale_edge").F("chance", 0.20f);
            if (entity.World.Rand.NextDouble() >= chance) return;
            StrikeElemental(targetEntity, SpellSchool.PhysicalMelee);
        }
    }

    /// <summary>Applies the marker; the EmpowerNextMelee bundle carrying the damage can't debuff the target.</summary>
    [EffectRegistration(ShamanEffectIds.ThunderCleaveArmed)]
    public class ThunderCleaveArmedEffect : Effect
    {
        public override void DidAttack(DamageSource source, EntityAgent targetEntity, ref EnumHandling handled)
        {
            if (targetEntity == null || targetEntity == entity || !targetEntity.Alive) return;
            if (entity?.World?.Side != EnumAppSide.Server || source is CanrpgDamageSource) return;

            SpellExecutor.ApplyEffect(targetEntity, ShamanEffectIds.ThunderCleave, 1,
                BalanceConfig.Spell("thunder_cleave").F("markerDuration", 12f));
            SetExpiryImmediately(); // one strike, one mark
        }
    }

    /// <summary>Flag read by the shaman's lightning via DamageVsControlEffectId.</summary>
    [EffectRegistration(ShamanEffectIds.ThunderCleave, positive: false)]
    public class ThunderCleaveDebuffEffect : Effect { }

    /// <summary>Each stack cuts the spenders' cast time (instant at cap); any spender consumes all stacks.</summary>
    [EffectRegistration(ShamanEffectIds.Maelstrom)]
    public class MaelstromEffect : Effect
    {
        private const string Src = "canrpg_maelstrom";
        private readonly float perStack;
        private readonly int cap;

        public MaelstromEffect()
        {
            var b = BalanceConfig.Talent("shaman:storm_charge");
            perStack = b.F("castReductionPerStack", 0.2f);
            cap = (int)b.F("cap", 5f);
        }

        /// <summary>Stacks needed for an instant cast; also read by the HUD pips.</summary>
        public static int Cap => (int)BalanceConfig.Talent("shaman:storm_charge").F("cap", 5f);
        public static float Duration => BalanceConfig.Talent("shaman:storm_charge").F("duration", 15f);

        private static readonly string[] Spenders = { "arc_bolt", "forked_lightning", "mending_wave" };

        private void Apply()
        {
            float cut = System.Math.Min(1f, perStack * System.Math.Max(1, tier));
            foreach (var s in Spenders) entity.Stats.Set(StatKeys.CastReductionFor(s), Src, cut);
        }

        private void Clear()
        {
            foreach (var s in Spenders) entity.Stats.Remove(StatKeys.CastReductionFor(s), Src);
        }

        public override float DisplayMagnitude() => System.Math.Max(1, tier);
        public override void OnStart() => Apply();
        public override void OnStack(Effect otherEffect) { base.OnStack(otherEffect); Apply(); }
        public override void OnExpire() => Clear();
        public override bool OnDeath() { Clear(); return base.OnDeath(); }

        public static void Grant(Entity shaman)
        {
            if (shaman == null) return;
            int current = shaman.GetBehavior<EBEffects>()?.GetEffectTier(ShamanEffectIds.Maelstrom) ?? 0;
            if (current < 0) current = 0;
            SpellExecutor.ApplyEffect(shaman, ShamanEffectIds.Maelstrom, System.Math.Min(Cap, current + 1), Duration);
        }
    }

    [EffectRegistration(ShamanEffectIds.WarChant)]
    public class WarChantEffect : Effect
    {
        private const string Src = "canrpg_war_chant";
        private readonly float melee, speed;
        public WarChantEffect()
        {
            var b = BalanceConfig.Spell("war_chant");
            melee = b.F("meleeDamage", 0.15f);
            speed = b.F("walkSpeed", 0.15f);
        }
        public override float DisplayMagnitude() => melee;
        public override void OnStart()
        {
            entity.Stats.Set(StatKeys.MeleeWeaponsDamage, Src, melee);
            entity.Stats.Set(StatKeys.WalkSpeed, Src, speed);
        }
        public override void OnExpire() => Clear();
        public override bool OnDeath() { Clear(); return base.OnDeath(); }
        private void Clear()
        {
            entity.Stats.Remove(StatKeys.MeleeWeaponsDamage, Src);
            entity.Stats.Remove(StatKeys.WalkSpeed, Src);
        }
    }
}
