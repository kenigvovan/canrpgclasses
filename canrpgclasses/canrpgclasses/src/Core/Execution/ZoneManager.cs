using System;
using System.Collections.Generic;
using canrpgclasses.Core.Spells;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace canrpgclasses.Core.Execution
{
    /// <summary>
    /// Server-side driver for lingering ground zones - both ticking AoE carpets and one-shot traps. One tick
    /// listener drives them all and a zone dies with its caster. Server main thread only, no locking.
    /// </summary>
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
            public long ArmExpiryMs; // remove the trap if nothing trips it by this time

            // Channel mode: the zone lives only while its caster keeps channeling.
            public bool ChannelBound;
        }

        private static readonly List<Zone> zones = new List<Zone>();
        private static ICoreServerAPI? sapi;
        private static long tickId;

        public static void Start(ICoreServerAPI api)
        {
            sapi = api;
            // 100ms resolution - fine enough for a trap trigger check and the finest carpet cadence.
            tickId = api.Event.RegisterGameTickListener(OnTick, 100);
        }

        public static void Stop()
        {
            if (sapi != null && tickId != 0) sapi.Event.UnregisterGameTickListener(tickId);
            tickId = 0;
            sapi = null;
            zones.Clear();
        }

        /// <summary>Places a zone at the caster's current position. A ticking zone lands its first pulse and
        /// carpet immediately; a trap arms and waits for an enemy to step onto it.</summary>
        public static void Add(SpellContext ctx, SpellImpact imp)
        {
            var caster = ctx.Caster;
            var t = ctx.Spell.Target;
            // Range is the max cast DISTANCE (used as the zone radius too, unless ZoneRadius decouples them).
            float placeRange = ctx.Spell.Range > 0 ? ctx.Spell.Range : 6f;
            float radius = imp.ZoneRadius > 0 ? imp.ZoneRadius : placeRange;
            Vec3d center = imp.ZoneAtAimPoint ? AimGroundPoint(caster, placeRange) : caster.Pos.XYZ.Clone();

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

            // One-shot placement accent at the zone centre (once per cast - a zone has no single struck target for the
            // per-impact SkillFx flags to ride). Uses the zone's real footprint so the ring/sigil matches the reach.
            if (imp.ZoneLandRune) Visuals.SkillFx.Rune(caster.World, center, zone.Hor, ctx.Spell.School);
            if (imp.ZoneLandRing) Visuals.SkillFx.RingWave(caster.World, center, zone.Hor, ctx.Spell.School);

            long now = caster.World.ElapsedMilliseconds;

            if (imp.ZoneIsTrap)
            {
                zone.IsTrap = true;
                zone.TriggerRadius = imp.ZoneTriggerRadius > 0 ? imp.ZoneTriggerRadius : 1.5f;
                zone.ArmExpiryMs = now + (long)(duration * 1000f);
                // A single puff at placement so the caster sees where it landed; then the trap stays hidden while
                // it waits (no continuous marker) and shows again only when it fires (Trigger).
                zone.Carpet?.EmitDisc(caster.World, zone.Center, zone.TriggerRadius);
                zones.Add(zone);
                return;
            }

            // Ticking zone (Hallowed Ground).
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

        /// <summary>The ground point the caster is aiming at, up to <paramref name="maxDist"/> blocks along the look
        /// direction: the first block the view ray hits; if it hits nothing (aiming over a ledge / above terrain),
        /// the ray end dropped straight down onto the surface. Server-side placement for aim-cast zones (Ice Storm).</summary>
        private static Vec3d AimGroundPoint(EntityAgent caster, float maxDist)
        {
            var from = caster.Pos;
            Vec3d eye = from.XYZ.Add(caster.LocalEyePos);
            Vec3d to = eye.AheadCopy(maxDist, from.Pitch, from.Yaw);

            BlockSelection bsel = null!;
            EntitySelection esel = null!;
            caster.World.RayTraceForSelection(eye, to, ref bsel, ref esel);
            if (bsel != null) return bsel.FullPosition;

            caster.World.RayTraceForSelection(to, to.AddCopy(0, -40, 0), ref bsel, ref esel);
            return bsel != null ? bsel.FullPosition : to;
        }

        /// <summary>Removes every channel-bound zone belonging to <paramref name="caster"/> - called when their
        /// channel (Ice Storm) ends early (moved / interrupted). No-op for non-channel zones.</summary>
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
                    if (now >= z.ArmExpiryMs) { zones.RemoveAt(i); continue; } // never tripped
                    if (AnEnemyIsOn(z))
                    {
                        Trigger(z);
                        zones.RemoveAt(i); // one-shot
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

        /// <summary>Fires a trap: a single AoE burst of damage + status over the blast disc, plus an explosion
        /// visual/sound at the centre. No block destruction.</summary>
        private static void Trigger(Zone z)
        {
            var world = z.Caster.World;
            // Trap Mastery (hunter): a 1.0-based multiplier on the burst damage and the control duration.
            float trapPower = z.Caster.Stats.GetBlended(canrpgclasses.Core.StatKeys.TrapPower);
            if (trapPower < 0.01f) trapPower = 1f;
            float amount = z.SpellPower * z.Impact.DamageSpellPowerCoefficient * trapPower;
            float statusSecs = z.Impact.ZoneStatusSeconds * trapPower;
            string? statusId = z.Impact.ZoneStatusEffectId;
            // Napalm Traps (hunter): a damaging trap also ignites what it catches. Generic stat, so the core
            // stays class-agnostic (same pattern as trapPower above). GetBlended is 1.0-based: an UNSET stat
            // returns 1.0, and the Napalm talent adds +1 (→ 2.0), so the flag is "on" only above 1.5.
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

            // Blast visual over the whole disc + optional boom (missing sound asset = silent, no crash).
            z.Carpet?.EmitDisc(world, z.Center, z.Hor);
            if (!string.IsNullOrEmpty(z.Impact.ZoneTriggerSound))
                world.PlaySoundAt(new AssetLocation(z.Impact.ZoneTriggerSound), z.Center.X, z.Center.Y, z.Center.Z, null, false, 32f);
        }

        /// <summary>One damage pulse for a TICKING zone: enemies in the disc take damage and/or the status.</summary>
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
