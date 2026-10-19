using effectshud.src;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Core.Effects
{
    /// <summary>DoT/HoT with per-tick magnitude fixed from caster spell power at apply time. Snapshot fields must be
    /// public: effectshud serializes public fields and OnStart doesn't re-run on deserialize.</summary>
    public abstract class SnapshotEffect : Effect, ISnapshotEffect
    {
        public float casterSpellPower;
        public long casterId;

        public void CaptureCaster(long casterEntityId, float casterSpellPowerSnapshot)
        {
            casterId = casterEntityId;
            casterSpellPower = casterSpellPowerSnapshot;
        }

        /// <summary>Null if the caster logged out or is gone.</summary>
        protected Entity? Caster => entity?.World?.GetEntityById(casterId);

        // Re-cast: take the incoming snapshot, refresh via base, then re-bake.
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

    /// <summary>Snapshot DoT dealing spell power x coeffPerTick as its <see cref="School"/>, attributed to the
    /// caster (combat text, XP, resists). Keeps ticking after the caster logs out.</summary>
    public abstract class SchoolDamageDotEffect : SnapshotEffect
    {
        /// <summary>Baked damage per tick; public to survive relog.</summary>
        public float perTick;

        protected abstract SpellSchool School { get; }
        protected abstract float CoeffPerTick { get; }

        protected override void Recompute() => perTick = casterSpellPower * CoeffPerTick;

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

        /// <summary>Per-tick rider hook, e.g. leech or spread.</summary>
        protected virtual void OnDamaged(Entity? caster, float dealtThisTick) { }
    }
}
