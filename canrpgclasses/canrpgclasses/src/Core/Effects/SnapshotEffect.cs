using effectshud.src;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Core.Effects
{
    /// <summary>
    /// Base for a DoT or HoT whose per-tick magnitude is fixed at application time from the caster's spell power.
    /// The snapshot fields must stay public: effectshud serializes public fields and OnStart doesn't re-run on
    /// deserialize, so anything baked has to survive that way.
    /// </summary>
    public abstract class SnapshotEffect : Effect, ISnapshotEffect
    {
        public float casterSpellPower;
        public long casterId;

        public void CaptureCaster(long casterEntityId, float casterSpellPowerSnapshot)
        {
            casterId = casterEntityId;
            casterSpellPower = casterSpellPowerSnapshot;
        }

        /// <summary>The caster entity, if still present in the world (may be null if they logged out / died).</summary>
        protected Entity? Caster => entity?.World?.GetEntityById(casterId);

        // Re-cast onto a target that already has this effect: take the fresh snapshot from the incoming instance,
        // then let the base handle the tier/expiry refresh, then re-bake our per-tick value.
        public override void OnStack(Effect otherEffect)
        {
            if (otherEffect is SnapshotEffect o)
            {
                casterSpellPower = o.casterSpellPower;
                casterId = o.casterId;
            }
            base.OnStack(otherEffect);
            Recompute();
        }

        public override void OnStart() => Recompute();

        /// <summary>Bakes <see cref="casterSpellPower"/> (+ any caster stats/talents) into the per-tick magnitude.</summary>
        protected abstract void Recompute();
    }

    /// <summary>A snapshot DoT: each tick deals spell power × coeffPerTick as its <see cref="School"/>, attributed
    /// to the caster so combat text, XP credit and school resists all work. Keeps ticking after the caster logs
    /// out. <see cref="OnDamaged"/> is the per-tick rider hook for leech or spread.</summary>
    public abstract class SchoolDamageDotEffect : SnapshotEffect
    {
        /// <summary>Baked damage per tick. Public so it survives relog (OnStart doesn't re-run on deserialize).</summary>
        public float perTick;

        protected abstract SpellSchool School { get; }
        protected abstract float CoeffPerTick { get; }

        protected override void Recompute() => perTick = casterSpellPower * CoeffPerTick;

        /// <summary>Surface the baked per-tick damage in the RPG character sheet (via effectshud's DisplayMagnitude sync).</summary>
        public override float DisplayMagnitude() => perTick;

        public override void OnTick()
        {
            if (perTick <= 0f) return;
            var caster = Caster;
            entity.ReceiveDamage(new CanrpgDamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = caster,
                CauseEntity = caster,
                School = School,
                Type = DamageSchools.For(School).EngineType
            }, perTick);
            OnDamaged(caster, perTick);
        }

        /// <summary>Hook for a rider on each damaging tick (Consuming Plague heals the caster). No-op by default.</summary>
        protected virtual void OnDamaged(Entity? caster, float dealtThisTick) { }
    }
}
