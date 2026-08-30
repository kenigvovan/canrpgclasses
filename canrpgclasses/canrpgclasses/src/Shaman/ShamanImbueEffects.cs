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
    /// <summary>
    /// The Enhancement on-hit layer: weapon imbues, the Thunder Cleave marker, and Storm Charge. An imbue is
    /// just a long self-buff riding <see cref="Effect.DidAttack"/>, spell power snapshotted at cast time.
    /// </summary>
    public static class ShamanImbueIds
    {
        public const string FlameEdge = "canrpg_imbue_flametongue";
        public const string FrostEdge = "canrpg_imbue_frostbrand";
        public const string GaleEdge = "canrpg_imbue_windfury";

        /// <summary>The three imbues are mutually exclusive - applying one strips the others.</summary>
        public static readonly string[] All = { FlameEdge, FrostEdge, GaleEdge };
    }

    /// <summary>Base for a weapon imbue: a long self-buff whose rider fires on every landed melee swing, scaled by
    /// the snapshotted spell power and <see cref="ShamanStatKeys.ImbuePower"/>.</summary>
    public abstract class ImbueEffectBase : SnapshotEffect
    {
        /// <summary>Baked rider magnitude. Public so it survives relog (OnStart doesn't re-run on deserialize).</summary>
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

        /// <summary>Guards the swing before the rider runs: server-side, a living non-ally target, and not our own
        /// spell damage looping back round.</summary>
        protected bool ShouldRide(DamageSource source, EntityAgent target)
            => perHit > 0f && target != null && target != entity && target.Alive
               && entity?.World?.Side == EnumAppSide.Server
               && source is not CanrpgDamageSource;

        /// <summary>Lands the imbue's elemental damage as its own hit of its own school (so the weapon still swings
        /// physical and the imbue burns/chills on top). Pierces the swing's i-frames without arming new ones -
        /// exactly how an empowered magical strike lands (see EBSpellCaster.TakeEmpoweredMeleeBonus).</summary>
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
            // A generic walkslow (not the mage's own chill marker) - this shouldn't enable a mage's Ice Shard shatter.
            SpellExecutor.ApplyEffect(targetEntity, "walkslow", 1, BalanceConfig.Spell("frost_edge").F("slowDuration", 2f));
        }
    }

    /// <summary>The extra swing is a fraction of the shaman's melee power, not a re-read of the weapon's own damage
    /// (unreachable from inside an effect), so it can't double-dip weapon riders.</summary>
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

    /// <summary>Carries only the marker: the strike's damage rides the EmpowerNextMelee bundle, which can't apply a
    /// status effect to the target it lands on.</summary>
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

    /// <summary>A pure flag with no stats of its own - the shaman's lightning nominates it via
    /// DamageVsControlEffectId.</summary>
    [EffectRegistration(ShamanEffectIds.ThunderCleave, positive: false)]
    public class ThunderCleaveDebuffEffect : Effect { }

    /// <summary>Each stack shaves the cast time of the three staples below; at full stacks they are instant. Built by
    /// the Storm Charge talent's on-hit roll and consumed WHOLE by any of them (Spell.ConsumesCasterEffectId).</summary>
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

        /// <summary>Stacks needed for a free instant cast - read by the HUD pips and the talent's grant.</summary>
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
