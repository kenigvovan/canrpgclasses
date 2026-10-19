using System;
using System.Collections.Generic;
using canrpgclasses.Core.Spells;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace canrpgclasses.Core.Execution
{
    /// <summary>Ground zones (ticking carpets and one-shot traps) on a single server tick listener. A zone dies with
    /// its caster. Main thread only, no locking.</summary>
    public static class ZoneManager
    {
        private sealed class Zone
        {
            public EntityAgent Caster = null!;
            public Vec3d Center = null!;
            public float Hor, Ver, SpellPower;
            public SpellImpact Impact = null!;
            public SpellSchool School;
            public ParticleSpec? Carpet;

            // Ticking-zone budgets (unused for traps).
            public int DamageTicksLeft;
            public int FxTicksLeft;
            public long DamageEveryMs, FxEveryMs;
            public long NextDamageAtMs, NextFxAtMs;

            // Trap mode.
            public bool IsTrap;
            public float TriggerRadius;
            public long ArmExpiryMs;

            // Lives only while the caster keeps channeling.
            public bool ChannelBound;
        }

        private static readonly List<Zone> zones = new List<Zone>();
        private static ICoreServerAPI? sapi;
        private static long tickId;

        public static void Start(ICoreServerAPI api)
        {
            sapi = api;
            tickId = api.Event.RegisterGameTickListener(OnTick, 100);
        }

        public static void Stop()
        {
            if (sapi != null && tickId != 0) sapi.Event.UnregisterGameTickListener(tickId);
            tickId = 0;
            sapi = null;
            zones.Clear();
        }

        /// <summary>A ticking zone pulses immediately; a trap arms and waits.</summary>
        public static void Add(SpellContext ctx, SpellImpact imp)
        {
            var caster = ctx.Caster;
            var t = ctx.Spell.Target;
            // Range doubles as the zone radius unless ZoneRadius is set.
            float placeRange = ctx.Spell.Range > 0 ? ctx.Spell.Range : 6f;
            float radius = imp.ZoneRadius > 0 ? imp.ZoneRadius : placeRange;
            Vec3d center = imp.ZoneAtAimPoint ? AimGroundPoint(caster, placeRange, ctx.Aim) : caster.Pos.XYZ.Clone();

            float duration = imp.ZoneDurationSeconds > 0 ? imp.ZoneDurationSeconds : 1f;
            float fxEvery = imp.ZoneParticleSeconds > 0 ? imp.ZoneParticleSeconds : 0.4f;

            var zone = new Zone
            {
                Caster = caster,
                Center = center,
                Hor = radius * (t.AreaHorizontalRangeMultiplier > 0 ? t.AreaHorizontalRangeMultiplier : 1f),
                Ver = radius * (t.AreaVerticalRangeMultiplier > 0 ? t.AreaVerticalRangeMultiplier : 1f),
                SpellPower = ctx.SpellPower,
                Impact = imp,
                School = ctx.Spell.School,
                Carpet = t.AreaParticles,
                FxEveryMs = (long)(fxEvery * 1000f),
                ChannelBound = ctx.Spell.CastMode == CastMode.Channel
            };

            // Placement accent sized to the zone's real footprint.
            if (imp.ZoneLandRune) Visuals.SkillFx.Rune(caster.World, center, zone.Hor, ctx.Spell.School);
            if (imp.ZoneLandRing) Visuals.SkillFx.RingWave(caster.World, center, zone.Hor, ctx.Spell.School);

            long now = caster.World.ElapsedMilliseconds;

            if (imp.ZoneIsTrap)
            {
                zone.IsTrap = true;
                zone.TriggerRadius = imp.ZoneTriggerRadius > 0 ? imp.ZoneTriggerRadius : 1.5f;
                zone.ArmExpiryMs = now + (long)(duration * 1000f);
                // One puff at placement, then hidden until it fires.
                zone.Carpet?.EmitDisc(caster.World, zone.Center, zone.TriggerRadius);
                zones.Add(zone);
                return;
            }

            float dmgEvery = imp.ZoneTickSeconds > 0 ? imp.ZoneTickSeconds : 1f;
            zone.DamageEveryMs = (long)(dmgEvery * 1000f);
            zone.DamageTicksLeft = Math.Max(1, (int)Math.Round(duration / dmgEvery));
            zone.FxTicksLeft = zone.Carpet != null ? Math.Max(1, (int)Math.Round(duration / fxEvery)) : 0;

            DamageTick(zone);
            zone.DamageTicksLeft--;
            zone.NextDamageAtMs = now + zone.DamageEveryMs;

            if (zone.FxTicksLeft > 0)
            {
                zone.Carpet!.EmitDisc(caster.World, zone.Center, zone.Hor);
                zone.FxTicksLeft--;
                zone.NextFxAtMs = now + zone.FxEveryMs;
            }

            if (zone.DamageTicksLeft > 0 || zone.FxTicksLeft > 0) zones.Add(zone);
        }

        /// <summary>First block hit by the view ray within <paramref name="maxDist"/>; on a miss, the ray end dropped
        /// onto the ground.</summary>
        internal static Vec3d AimGroundPoint(EntityAgent caster, float maxDist, AimContext? aim = null)
        {
            var from = caster.Pos;
            Vec3d eye = from.XYZ.Add(caster.LocalEyePos);
            Vec3d to = eye.AheadCopy(maxDist, from.Pitch, from.Yaw);

            BlockSelection bsel = null!;
            EntitySelection esel = null!;

            // AI casters: target position clamped to range and dropped to the ground.
            if (aim != null && aim.AimAtTarget && (aim.TargetEntity?.Pos.XYZ ?? aim.AimPosition) is Vec3d point)
            {
                Vec3d origin = from.XYZ;
                if (point.DistanceTo(origin) > maxDist) point = origin + (point - origin).Normalize() * maxDist;
                caster.World.RayTraceForSelection(point.AddCopy(0, 1, 0), point.AddCopy(0, -40, 0), ref bsel, ref esel);
                return bsel != null ? bsel.FullPosition : point;
            }

            caster.World.RayTraceForSelection(eye, to, ref bsel, ref esel);
            if (bsel != null) return bsel.FullPosition;

            caster.World.RayTraceForSelection(to, to.AddCopy(0, -40, 0), ref bsel, ref esel);
            return bsel != null ? bsel.FullPosition : to;
        }

        /// <summary>Called when a channel ends early.</summary>
        public static void CancelChannelZones(Entity caster)
        {
            for (int i = zones.Count - 1; i >= 0; i--)
                if (zones[i].ChannelBound && zones[i].Caster == caster) zones.RemoveAt(i);
        }

        private static void OnTick(float dt)
        {
            if (zones.Count == 0) return;
            var world = sapi!.World;
            long now = world.ElapsedMilliseconds;

            for (int i = zones.Count - 1; i >= 0; i--)
            {
                var z = zones[i];

                if (!z.Caster.Alive || z.Caster.State == EnumEntityState.Despawned)
                {
                    zones.RemoveAt(i);
                    continue;
                }

                if (z.IsTrap)
                {
                    if (now >= z.ArmExpiryMs) { zones.RemoveAt(i); continue; }
                    if (AnEnemyIsOn(z))
                    {
                        Trigger(z);
                        zones.RemoveAt(i);
                    }
                    continue;
                }

                if (z.DamageTicksLeft > 0 && now >= z.NextDamageAtMs)
                {
                    DamageTick(z);
                    z.DamageTicksLeft--;
                    z.NextDamageAtMs += z.DamageEveryMs;
                }

                if (z.FxTicksLeft > 0 && now >= z.NextFxAtMs)
                {
                    z.Carpet!.EmitDisc(world, z.Center, z.Hor);
                    z.FxTicksLeft--;
                    z.NextFxAtMs += z.FxEveryMs;
                }

                if (z.DamageTicksLeft <= 0 && z.FxTicksLeft <= 0) zones.RemoveAt(i);
            }
        }

        private static bool AnEnemyIsOn(Zone z)
        {
            var world = z.Caster.World;
            foreach (var e in world.GetEntitiesAround(z.Center, z.TriggerRadius, z.TriggerRadius + 1f,
                         en => en.Alive && en is EntityAgent && en != z.Caster))
            {
                if (!SpellExecutor.WithinHorizontalRadius(z.Center, e, z.TriggerRadius)) continue;
                if (SpellExecutor.IsAlly(z.Caster, e)) continue;
                return true;
            }
            return false;
        }

        /// <summary>One AoE burst over the blast disc; no block destruction.</summary>
        private static void Trigger(Zone z)
        {
            var world = z.Caster.World;
            // 1.0-based multiplier on damage and control duration.
            float trapPower = z.Caster.Stats.GetBlended(canrpgclasses.Core.StatKeys.TrapPower);
            if (trapPower < 0.01f) trapPower = 1f;
            float amount = z.SpellPower * z.Impact.DamageSpellPowerCoefficient * trapPower;
            float statusSecs = z.Impact.ZoneStatusSeconds * trapPower;
            string? statusId = z.Impact.ZoneStatusEffectId;
            // 1.0-based flag stat: unset = 1.0, the talent adds +1, so "on" above 1.5.
            bool ignite = amount > 0 && z.Caster.Stats.GetBlended(canrpgclasses.Core.StatKeys.TrapIgnite) > 1.5f;

            foreach (var e in world.GetEntitiesAround(z.Center, z.Hor, z.Ver, en => en.Alive && en is EntityAgent && en != z.Caster))
            {
                if (!SpellExecutor.WithinHorizontalRadius(z.Center, e, z.Hor)) continue;
                if (SpellExecutor.IsAlly(z.Caster, e)) continue;
                if (amount > 0)
                {
                    e.ReceiveDamage(new CanrpgDamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = z.Caster,
                        CauseEntity = z.Caster,
                        School = z.School,
                        Type = DamageSchools.For(z.School).EngineType,
                        KnockbackStrength = z.Impact.Knockback
                    }, amount);
                }
                if (statusId != null)
                    SpellExecutor.ApplyEffect(e, statusId, z.Impact.ZoneStatusTier, statusSecs);
                if (ignite) e.IsOnFire = true;
                z.Impact.Particles?.Emit(world, e);
            }

            // A missing sound asset is silent, not a crash.
            z.Carpet?.EmitDisc(world, z.Center, z.Hor);
            if (!string.IsNullOrEmpty(z.Impact.ZoneTriggerSound))
                world.PlaySoundAt(new AssetLocation(z.Impact.ZoneTriggerSound), z.Center.X, z.Center.Y, z.Center.Z, null, false, 32f);
        }

        private static void DamageTick(Zone z)
        {
            float amount = z.SpellPower * z.Impact.DamageSpellPowerCoefficient;
            string? statusId = z.Impact.ZoneStatusEffectId;
            if (amount <= 0 && statusId == null) return;
            var world = z.Caster.World;

            foreach (var e in world.GetEntitiesAround(z.Center, z.Hor, z.Ver, en => en.Alive && en is EntityAgent && en != z.Caster))
            {
                if (!SpellExecutor.WithinHorizontalRadius(z.Center, e, z.Hor)) continue;
                if (SpellExecutor.IsAlly(z.Caster, e)) continue;
                if (amount > 0)
                {
                    e.ReceiveDamage(new CanrpgDamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = z.Caster,
                        CauseEntity = z.Caster,
                        School = z.School,
                        Type = DamageSchools.For(z.School).EngineType,
                        KnockbackStrength = z.Impact.Knockback
                    }, amount);
                }
                if (statusId != null)
                    SpellExecutor.ApplyEffect(e, statusId, z.Impact.ZoneStatusTier, z.Impact.ZoneStatusSeconds);
                z.Impact.Particles?.Emit(world, e);
            }
        }
    }
}
