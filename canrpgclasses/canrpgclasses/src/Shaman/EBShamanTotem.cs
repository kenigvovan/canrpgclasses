using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman
{
    /// <summary>
    /// Server behavior on a planted totem: 1s tick runs the pulse and expires it on lifetime end or owner logoff.
    /// Everything it needs is snapshotted into WatchedAttributes at plant time, so the pulse is a pure read.
    /// </summary>
    public class EBShamanTotem : EntityBehavior
    {
        public const string Name = "canrpgshamantotem";

        // Owner link: the vanilla key SpellExecutor.IsAlly reads to treat the totem as its owner's ally (so a
        // healing totem never mistakes the party for enemies, and Forked Lightning never arcs into it).
        public const string OwnerUidKey = AttrKeys.GuardedByPlayerUid;

        public const string KindKey = "canrpgTotemKind";
        public const string ExpiryKey = "canrpgTotemExpiry";       // world ms
        public const string RadiusKey = "canrpgTotemRadius";
        public const string PowerKey = "canrpgTotemSpellPower";    // caster's spell power at plant time
        public const string HealPowerKey = "canrpgTotemHealPower"; // caster's healingPower multiplier at plant time

        private float sinceTick;
        private float sinceRing;

        public EBShamanTotem(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);
            if (entity.Api.Side != EnumAppSide.Server || !entity.Alive) return;

            sinceRing += deltaTime;
            if (sinceRing >= 2f) { sinceRing = 0f; EmitRadiusRing(); }

            sinceTick += deltaTime;
            if (sinceTick < 1f) return;
            sinceTick = 0f;

            var wa = entity.WatchedAttributes;
            if (entity.World.ElapsedMilliseconds > wa.GetLong(ExpiryKey, 0)) { entity.Die(EnumDespawnReason.Removed); return; }

            var owner = Owner;
            if (owner == null || !owner.Alive) { entity.Die(EnumDespawnReason.Removed); return; }

            float radius = wa.GetFloat(RadiusKey, 8f);
            float power = wa.GetFloat(PowerKey, 0f);
            if (power <= 0f) return;

            switch (wa.GetString(KindKey, ""))
            {
                case ShamanTotemSystem.KindSearing: PulseSearing(owner, radius, power); break;
                case ShamanTotemSystem.KindStream: PulseStream(owner, radius, power); break;
                case ShamanTotemSystem.KindEarth: PulseEarth(owner, radius); break;
                case ShamanTotemSystem.KindEarthbind: PulseEarthbind(owner, radius); break;
                case ShamanTotemSystem.KindManaSpring: PulseManaSpring(owner, radius); break;
            }
        }

        /// <summary>Burns the nearest enemy in reach, attributed to the owner so kills credit the shaman's XP and
        /// the hit takes fire resists like any of their spells.</summary>
        private void PulseSearing(EntityAgent owner, float radius, float power)
        {
            var foe = NearestEnemy(owner, radius);
            if (foe == null) return;

            float dmg = power * BalanceConfig.Global("totemSearingCoeff", 0.4f);
            if (dmg <= 0f) return;
            foe.ReceiveDamage(new CanrpgDamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = owner,
                CauseEntity = owner,
                School = SpellSchool.Fire,
                Type = DamageSchools.For(SpellSchool.Fire).EngineType
            }, dmg);
            ParticleSpec.ForSchool(SpellSchool.Fire).Emit(entity.World, foe);
        }

        private void PulseStream(EntityAgent owner, float radius, float power)
        {
            float heal = power * BalanceConfig.Global("totemStreamCoeff", 0.3f)
                       * entity.WatchedAttributes.GetFloat(HealPowerKey, 1f);
            if (heal <= 0f) return;

            foreach (var e in NearbyAllies(owner, radius))
            {
                e.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, heal);
                ParticleSpec.Heal().Emit(entity.World, e);
            }
        }

        /// <summary>The buff outlasts one pulse but not many, so leaving the reach drops it without any of the range
        /// bookkeeping the paladin auras need.</summary>
        private void PulseEarth(EntityAgent owner, float radius)
        {
            float dur = BalanceConfig.Global("totemEarthBuffSeconds", 3f);
            foreach (var e in NearbyAllies(owner, radius))
                SpellExecutor.ApplyEffect(e, ShamanEffectIds.TotemEarth, 1, dur);
        }

        /// <summary>Same trick as <see cref="PulseEarth"/>: the slow is shorter than the refresh cadence.</summary>
        private void PulseEarthbind(EntityAgent owner, float radius)
        {
            float dur = BalanceConfig.Global("totemEarthbindSlowSeconds", 1.5f);
            int tier = (int)BalanceConfig.Global("totemEarthbindSlowTier", 2f);
            foreach (var e in NearbyEnemies(owner, radius))
                SpellExecutor.ApplyEffect(e, "walkslow", tier, dur);
        }

        private void PulseManaSpring(EntityAgent owner, float radius)
        {
            float mana = BalanceConfig.Global("totemManaSpringPerTick", 4f);
            if (mana <= 0f) return;
            foreach (var e in NearbyAllies(owner, radius))
            {
                var pool = Core.Resources.ResourceState.PrimaryPool(e);
                if (pool == null || pool.Id != "mana") continue; // only mana users benefit
                float max = Core.Resources.ResourceState.EffectiveMax(e, pool);
                Core.Resources.ResourceState.Set(e, pool, Core.Resources.ResourceState.Get(e, pool) + mana, max);
            }
        }

        private System.Collections.Generic.List<Entity> NearbyEnemies(EntityAgent owner, float radius)
        {
            var center = entity.Pos.XYZ;
            var list = new System.Collections.Generic.List<Entity>();
            foreach (var e in entity.World.GetEntitiesAround(center, radius, radius,
                         en => en.Alive && en is EntityAgent && en != entity && en != owner))
                if (SpellExecutor.WithinHorizontalRadius(center, e, radius) && !SpellExecutor.IsAlly(owner, e))
                    list.Add(e);
            return list;
        }

        private Entity? NearestEnemy(EntityAgent owner, float radius)
        {
            var center = entity.Pos.XYZ;
            Entity? best = null;
            double bestDist = double.MaxValue;
            foreach (var e in entity.World.GetEntitiesAround(center, radius, radius,
                         en => en.Alive && en is EntityAgent && en != entity && en != owner))
            {
                if (!SpellExecutor.WithinHorizontalRadius(center, e, radius)) continue;
                if (SpellExecutor.IsAlly(owner, e)) continue;
                double d = e.Pos.SquareDistanceTo(center);
                if (d < bestDist) { bestDist = d; best = e; }
            }
            return best;
        }

        private System.Collections.Generic.List<Entity> NearbyAllies(EntityAgent owner, float radius)
        {
            var center = entity.Pos.XYZ;
            var list = new System.Collections.Generic.List<Entity>();
            foreach (var e in entity.World.GetEntitiesAround(center, radius, radius,
                         en => en.Alive && en is EntityPlayer))
                if (SpellExecutor.WithinHorizontalRadius(center, e, radius) && SpellExecutor.IsAlly(owner, e))
                    list.Add(e);
            return list;
        }

        private void EmitRadiusRing()
        {
            var wa = entity.WatchedAttributes;
            ParticleSpec.ForSchool(wa.GetString(KindKey, "") == ShamanTotemSystem.KindSearing ? SpellSchool.Fire : SpellSchool.Nature)
                .EmitDisc(entity.World, entity.Pos.XYZ, wa.GetFloat(RadiusKey, 8f));
        }

        private EntityAgent? Owner
        {
            get
            {
                string uid = entity.WatchedAttributes.GetString(OwnerUidKey, null);
                return uid == null ? null : entity.World.PlayerByUid(uid)?.Entity;
            }
        }
    }
}
