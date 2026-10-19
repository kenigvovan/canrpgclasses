using System.Collections.Generic;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Spells;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace canrpgclasses.Core.Entities
{
    /// <summary>Spell projectile: vanilla <see cref="EntityProjectile"/> physics, but the hit runs the spell's
    /// impacts instead of weapon damage, then despawns.</summary>
    public class EntitySpellProjectile : EntityProjectile
    {
        public string SpellId
        {
            get => WatchedAttributes.GetString("spellId", "");
            set => WatchedAttributes.SetString("spellId", value);
        }

        /// <summary>Caster spell power snapshotted at launch.</summary>
        public float CasterSpellPower
        {
            get => WatchedAttributes.GetFloat("spellPower", 1f);
            set => WatchedAttributes.SetFloat("spellPower", value);
        }

        public float MaxRange
        {
            get => WatchedAttributes.GetFloat("maxRange", 32f);
            set => WatchedAttributes.SetFloat("maxRange", value);
        }

        /// <summary>Combo points spent at launch; the live combo is already reset by the time the projectile hits.</summary>
        public int ComboPoints
        {
            get => WatchedAttributes.GetInt("comboPoints", 0);
            set => WatchedAttributes.SetInt("comboPoints", value);
        }

        // Backstop in case the range check never trips. Server only.
        private const float MaxLifetimeSeconds = 12f;
        private const float SpinRevolutionsPerSecond = 2f;
        private Vec3d? launchPos;
        private float aliveSeconds;
        private float spinAngle;

        private SimpleParticleProperties? trail;

        /// <summary>Client trail for the "spellorb" bolt only, coloured by the "trailColor" attribute.</summary>
        private void EmitTrail()
        {
            if (Code?.Path != "spellorb") return;
            int color = WatchedAttributes.GetInt("trailColor", ColorUtil.ToRgba(220, 255, 255, 255));
            trail ??= new SimpleParticleProperties(
                2, 3, color,
                new Vec3d(), new Vec3d(),
                new Vec3f(-0.1f, -0.1f, -0.1f), new Vec3f(0.1f, 0.1f, 0.1f),
                0.4f, 0f, 0.15f, 0.4f, EnumParticleModel.Quad)
            { VertexFlags = 255 };
            trail.Color = color;
            var p = Pos.XYZ;
            trail.MinPos.Set(p.X - 0.08, p.Y - 0.08, p.Z - 0.08);
            trail.AddPos.Set(0.16, 0.16, 0.16);
            World.SpawnParticles(trail);
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (Api.Side == EnumAppSide.Client)
            {
                if (Alive) EmitTrail();
                return;
            }
            if (!Alive) return;

            // Despawn on a terrain hit instead of sticking around like an arrow.
            if (Stuck)
            {
                if (ResolveSpell() is Spell spell) SpellSounds.Play(World, spell, SpellSounds.Slot.Impact, Pos.XYZ);
                Die(EnumDespawnReason.Death);
                return;
            }

            // Spin is set server-side because the render follows the synced rotation. Never wrapped, so
            // interpolation doesn't cross a 360-to-0 seam.
            spinAngle += dt * SpinRevolutionsPerSecond * GameMath.TWOPI;
            Pos.Roll = spinAngle;

            launchPos ??= ServerPos.XYZ;
            aliveSeconds += dt;

            float range = MaxRange;
            bool outOfRange = range > 0 && ServerPos.XYZ.DistanceTo(launchPos) > range;
            if (aliveSeconds > MaxLifetimeSeconds || outOfRange)
            {
                Die(EnumDespawnReason.Death);
            }
        }

        /// <summary>NPC offensive bolts pass through allies; players keep the vanilla rule. Runs on both sides so
        /// the client agrees with the server.</summary>
        protected override bool CanHitTarget(Entity target)
        {
            if (!base.CanHitTarget(target)) return false;
            if (FiredBy is not EntityAgent caster || caster is EntityPlayer) return true;
            return !(IsOffensive() && SpellExecutor.IsAlly(caster, target));
        }

        private bool? offensive;

        // Not cached while the spell id is still missing: the attribute may be set after the entity spawns.
        private bool IsOffensive()
        {
            if (offensive is bool known) return known;
            if (ResolveSpell() is not Spell spell) return false;
            offensive = spell.Target.Affinity == TargetAffinity.Enemy;
            return offensive.Value;
        }

        // Resolves through this side's mod instance, so the server gets the server registry.
        private Spell? ResolveSpell()
        {
            var mod = canrpgclassesModSystem.For(Api);
            return mod != null && !string.IsNullOrEmpty(SpellId) && mod.Spells.TryGet(SpellId, out var spell) ? spell : null;
        }

        protected override void ImpactOnEntity(Entity target)
        {
            if (World.Side == EnumAppSide.Server && CanDealDamage(target))
            {
                RunSpellImpacts(target);
            }

            EntityHit = true;
            if (Alive) Die(EnumDespawnReason.Death);
        }

        private void RunSpellImpacts(Entity target)
        {
            if (ResolveSpell() is not Spell spell || FiredBy is not EntityAgent caster) return;

            var ctx = new SpellContext
            {
                Caster = caster,
                Spell = spell,
                Aim = AimContext.None,
                SpellPower = CasterSpellPower,
                ComboPoints = ComboPoints,
                Targets = new List<Entity> { target }
            };
            SpellExecutor.ApplyImpacts(ctx, target);

            // Projectile builders grant combo on hit; the cast-time bookkeeping sees no targets for projectiles.
            if (spell.ComboBuilder && target != caster && !SpellExecutor.IsAlly(caster, target))
                canrpgclasses.Core.Resources.ResourceState.AddCombo(caster, spell.ComboPointsGenerated);
        }
    }
}
