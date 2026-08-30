using System.Collections.Generic;
using canrpgclasses.Core.Execution;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace canrpgclasses.Core.Entities
{
    /// <summary>
    /// A spell-launched projectile. Reuses VS's <see cref="EntityProjectile"/> physics and collision but
    /// replaces the weapon-damage impact with the spell's own impact pipeline. Flies straight, applies its
    /// impacts to the first entity it touches, then despawns.
    /// </summary>
    public class EntitySpellProjectile : EntityProjectile
    {
        public string SpellId
        {
            get => WatchedAttributes.GetString("spellId", "");
            set => WatchedAttributes.SetString("spellId", value);
        }

        /// <summary>Snapshot of the caster's spell power at launch, used by the impacts on hit.</summary>
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

        /// <summary>Snapshot of the combo points a finisher SPENT at launch. The caster's live combo is reset
        /// right after the cast (SpellExecutor.Death Blow), so the hit must scale off this snapshot - otherwise a
        /// projectile finisher (kill_shot) would always land at 0 combo.</summary>
        public int ComboPoints
        {
            get => WatchedAttributes.GetInt("comboPoints", 0);
            set => WatchedAttributes.SetInt("comboPoints", value);
        }

        // Hard backstop in case range never trips (e.g. weird motion). Real time, server only.
        private const float MaxLifetimeSeconds = 12f;
        private const float SpinRevolutionsPerSecond = 2f;
        private Vec3d? launchPos;
        private float aliveSeconds;
        private float spinAngle;

        private SimpleParticleProperties? trail;

        /// <summary>Client-side per-school particle trail for the mage's "spellorb" bolt (fire/frost/arcane colour from
        /// the "trailColor" attribute). Only the orb trails; the knife projectile (Whirling Knives) is left untouched.</summary>
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
                Die(EnumDespawnReason.Death);
                return;
            }

            // Cosmetic spin, set server-side so it syncs + interpolates to clients - a client-only Pos
            // write was ignored because the render follows the synced rotation. The angle accumulates
            // and is never wrapped to 0..2π, so interpolation never crosses a 360°→0° seam (that seam
            // looked random). Pitch is the axis that actually renders for this entity; with the blade
            // along model X it spins around its long axis - re-orient the blade in the shape to taste.
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
            var mod = canrpgclassesModSystem.For(Api); // server side here - resolves the SERVER instance
            if (mod == null || string.IsNullOrEmpty(SpellId) || !mod.Spells.TryGet(SpellId, out var spell)) return;
            if (FiredBy is not EntityAgent caster) return;

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

            // A projectile builder (steady_shot) grants its combo point on a LANDED hit, not on cast -
            // Death Blow's own builder bookkeeping never fires for projectiles (no targets at cast time).
            if (spell.ComboBuilder && target != caster && !SpellExecutor.IsAlly(caster, target))
                canrpgclasses.Core.Resources.ResourceState.AddCombo(caster, spell.ComboPointsGenerated);
        }
    }
}
