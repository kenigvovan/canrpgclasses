using System;
using System.Collections.Generic;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Entities;
using canrpgclasses.Core.Net;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

using EffectsHud = effectshud.src.effectshud;
using EffectBase = effectshud.src.Effect;
using EBEffects = effectshud.src.EBEffectsAffected;
using InvisEffect = effectshud.src.DefaultEffects.InvisibilityEffect;

namespace canrpgclasses.Core.Execution
{
    public delegate List<Entity> TargetResolver(SpellContext ctx);
    public delegate void DeliveryHandler(SpellContext ctx);
    public delegate void ImpactHandler(SpellContext ctx, Entity target, SpellImpact impact);

    /// <summary>
    /// Server-side pipeline that turns a <see cref="Spell"/> into effects on the world:
    /// targeting → delivery → impacts. Each step dispatches to a registered handler, so a content mod can add
    /// new target, delivery or impact kinds without touching the core.
    /// </summary>
    public static class SpellExecutor
    {
        private static readonly Dictionary<TargetType, TargetResolver> targeting = new Dictionary<TargetType, TargetResolver>();
        private static readonly Dictionary<DeliveryType, DeliveryHandler> deliveries = new Dictionary<DeliveryType, DeliveryHandler>();
        private static readonly Dictionary<ImpactAction, ImpactHandler> impacts = new Dictionary<ImpactAction, ImpactHandler>();
        private static bool defaultsRegistered;

        public static void RegisterTargeting(TargetType type, TargetResolver resolver) => targeting[type] = resolver;
        public static void RegisterDelivery(DeliveryType type, DeliveryHandler handler) => deliveries[type] = handler;
        public static void RegisterImpact(ImpactAction action, ImpactHandler handler) => impacts[action] = handler;

        public static void RegisterDefaults()
        {
            if (defaultsRegistered) return;
            defaultsRegistered = true;

            RegisterTargeting(TargetType.None, _ => new List<Entity>());
            RegisterTargeting(TargetType.Caster, ctx => new List<Entity> { ctx.Caster });
            RegisterTargeting(TargetType.Aim, ResolveAim);
            RegisterTargeting(TargetType.Area, ResolveArea);

            RegisterDelivery(DeliveryType.Direct, DirectDelivery);
            RegisterDelivery(DeliveryType.Projectile, ProjectileDelivery);

            RegisterImpact(ImpactAction.Damage, ApplyDamage);
            RegisterImpact(ImpactAction.Heal, ApplyHeal);
            RegisterImpact(ImpactAction.StatusEffect, ApplyStatusEffect);
            RegisterImpact(ImpactAction.Teleport, ApplyTeleport);
            RegisterImpact(ImpactAction.CoatWeapon, ApplyCoatWeapon);
            RegisterImpact(ImpactAction.Dash, ApplyDash);
            RegisterImpact(ImpactAction.Stun, ApplyStun);
            RegisterImpact(ImpactAction.EmpowerNextMelee, ApplyEmpowerNextMelee);
            RegisterImpact(ImpactAction.Disarm, ApplyDisarm);
            RegisterImpact(ImpactAction.Shield, ApplyShield);
            RegisterImpact(ImpactAction.Cleanse, ApplyCleanse);
            RegisterImpact(ImpactAction.Aggro, ApplyAggro);
            RegisterImpact(ImpactAction.DamageZone, ApplyDamageZone);
            RegisterImpact(ImpactAction.ResetCooldowns, ApplyResetCooldowns);
            RegisterImpact(ImpactAction.Evasion, ApplyEvasion);
            RegisterImpact(ImpactAction.ToggleAura, ApplyToggleAura);
            RegisterImpact(ImpactAction.Reveal, ApplyReveal);
            RegisterImpact(ImpactAction.Fear, ApplyFear);
            RegisterImpact(ImpactAction.Silence, ApplySilence);
            RegisterImpact(ImpactAction.Root, ApplyRoot);
            RegisterImpact(ImpactAction.Transmute, ApplyTransmute);
            RegisterImpact(ImpactAction.GrantInstantCast, ApplyGrantInstantCast);
        }

        // Damage-absorb shield: a pool soaked by Entity.ReceiveDamage (StunPatches), with an expiry.
        public const string AbsorbKey = CombatFlags.Absorb;
        public const string AbsorbUntilKey = CombatFlags.AbsorbUntil;

        // "Empowered next strike" (sinister_strike): stored on the caster's WatchedAttributes and consumed
        // by EBSpellCaster.DidAttack on the next melee hit. Keys shared with that behavior.
        public const string EmpowerDamageKey = AttrKeys.EmpowerDamage;
        public const string EmpowerMissingDamageKey = AttrKeys.EmpowerMissingDamage;
        public const string EmpowerKnockbackKey = AttrKeys.EmpowerKnockback;
        public const string EmpowerComboKey = AttrKeys.EmpowerCombo;
        public const string EmpowerUntilKey = AttrKeys.EmpowerUntilMs;
        public const string EmpowerSpellKey = AttrKeys.EmpowerSpell;

        // Our own weapon-coating tag, not simplealchemy's "simplepoisoned": we apply the coating on every hit
        // ourselves, so it behaves the same with or without that mod and nothing double-drains our charges.
        public const string WeaponCoatKey = AttrKeys.WeaponCoat;

        // The empowered strike's bonus school: a physical one folds into the melee swing, a magical one lands as
        // its own armor-piercing hit.
        public const string EmpowerSchoolKey = AttrKeys.EmpowerSchool;

        /// <summary>Entry point. Server-authoritative; no-op on the client.</summary>
        public static void Execute(EntityAgent caster, Spell spell, AimContext aim)
        {
            if (caster?.Api?.Side != EnumAppSide.Server) return;

            var ctx = new SpellContext
            {
                Caster = caster,
                Spell = spell,
                Aim = aim ?? AimContext.None,
                SpellPower = caster.GetBehavior<EBRpgStats>()?.GetSpellPower(spell.School) ?? EBRpgStats.BaseSpellPower
            };

            ctx.Targets = ResolveTargets(ctx);

            // Combo: a finisher reads (and scales by) the caster's accumulated combo points before delivery.
            if (spell.ComboFinisher) ctx.ComboPoints = ResourceState.Combo(caster);

            Deliver(ctx);

            // Combo bookkeeping after the impacts land: builders add a point (only if they hit an enemy),
            // finishers consume all accumulated points.
            if (spell.ComboBuilder && HasEnemyTarget(ctx))
                ResourceState.AddCombo(caster, spell.ComboPointsGenerated);
            else if (spell.ComboFinisher)
                ResourceState.ResetCombo(caster);
        }

        private static bool HasEnemyTarget(SpellContext ctx)
        {
            foreach (var t in ctx.Targets)
                if (t != null && t != ctx.Caster && t.Alive) return true;
            return false;
        }

        public static List<Entity> ResolveTargets(SpellContext ctx)
            => targeting.TryGetValue(ctx.Spell.Target.Type, out var resolver) ? resolver(ctx) : new List<Entity>();

        public static void Deliver(SpellContext ctx)
        {
            if (deliveries.TryGetValue(ctx.Spell.Deliver.Type, out var handler)) handler(ctx);
        }

        public static void DirectDelivery(SpellContext ctx)
        {
            foreach (var target in ctx.Targets)
            {
                if (target != null) ApplyImpacts(ctx, target);
            }
        }

        /// <summary>Spawns the cast's projectile(s). One in the look direction by default; with ProjectileCount>1 it
        /// fires a flat (horizontal) spread - a frontal fan over ProjectileSpreadDegrees, or a full ring at 360° (Fan
        /// of Knives). Each projectile resolves the spell's impacts on whatever it hits.</summary>
        public static void ProjectileDelivery(SpellContext ctx)
        {
            var caster = ctx.Caster;
            var world = caster.World;
            var from = caster.Pos;
            Vec3d position = from.XYZ.Add(caster.LocalEyePos);
            double speed = ctx.Spell.Deliver.ProjectileVelocity;

            int count = Math.Max(1, ctx.Spell.Deliver.ProjectileCount);
            float spreadDeg = ctx.Spell.Deliver.ProjectileSpreadDegrees;
            bool ring = count > 1 && spreadDeg >= 359f;

            for (int i = 0; i < count; i++)
            {
                double yawOffsetRad =
                    count == 1 ? 0.0 :
                    ring       ? i * (Math.PI * 2.0 / count) :                                   // even full circle
                                 (-spreadDeg / 2.0 + spreadDeg * i / (count - 1)) * Math.PI / 180.0; // frontal fan
                double pitch = count == 1 ? from.Pitch : 0.0; // multi-projectile casts fire flat
                Vec3d dir = (position.AheadCopy(1.0, pitch, from.Yaw + yawOffsetRad) - position).Normalize();
                SpawnProjectile(ctx, world, position, dir, speed);
            }
        }

        private static void SpawnProjectile(SpellContext ctx, IWorldAccessor world, Vec3d position, Vec3d dir, double speed)
        {
            var type = world.GetEntityType(new AssetLocation(canrpgclassesModSystem.ModId, ctx.Spell.Deliver.ProjectileEntity));
            if (type == null) return;
            if (world.ClassRegistry.CreateEntity(type) is not EntitySpellProjectile proj) return;

            proj.FiredBy = ctx.Caster;
            proj.SpellId = ctx.Spell.Id;
            proj.CasterSpellPower = ctx.SpellPower;
            // Per-school trail colour (read by the orb's client-side EmitTrail). Packed A,R,G,B like ParticleSpec.
            var tc = Spells.ParticleSpec.ForSchool(ctx.Spell.School);
            proj.WatchedAttributes.SetInt("trailColor", ColorUtil.ToRgba(tc.ColorA, tc.ColorR, tc.ColorG, tc.ColorB));
            proj.ComboPoints = ctx.ComboPoints; // finisher: the hit scales off the combo spent at launch
            proj.MaxRange = ctx.Spell.Range > 0 ? ctx.Spell.Range : 32f;
            proj.Damage = 0f; // damage comes from the spell's impacts on hit

            proj.Pos.SetPos(position);
            proj.Pos.Motion.Set(dir.X * speed, dir.Y * speed, dir.Z * speed);
            proj.Pos.SetFrom(proj.Pos);
            proj.World = world;

            (proj as IProjectile)?.PreInitialize();
            world.SpawnPriorityEntity(proj);
        }

        public static void ApplyImpacts(SpellContext ctx, Entity target)
        {
            var rand = ctx.Caster.World.Rand;
            foreach (var imp in ctx.Spell.Impacts)
            {
                if (imp.Chance < 1f && rand.NextDouble() > imp.Chance) continue;
                // Talent-gated rider (Charge's Warbringer stun): skip unless the caster has the named talent.
                if (!string.IsNullOrEmpty(imp.RequiresTalentId)
                    && canrpgclasses.Core.Talents.TalentState.Rank(ctx.Caster, imp.RequiresTalentId!) <= 0) continue;
                if (!impacts.TryGetValue(imp.Action, out var handler)) continue;

                Entity? tgt;
                if (imp.AffectCaster) tgt = ctx.Caster;
                else if (imp.AffectAimedEnemy)
                {
                    tgt = GetAimedEntity(ctx.Caster, ctx.Aim, ctx.Spell.Range > 0 ? ctx.Spell.Range : 16f);
                    if (tgt != null && IsAlly(ctx.Caster, tgt)) tgt = null; // an enemy-only rider never lands on an ally
                }
                else tgt = target;
                if (tgt == null) continue; // e.g. an aimed-enemy rider with nothing hostile under the crosshair
                handler(ctx, tgt, imp);
                EmitImpactFx(ctx, tgt, imp);
            }
        }

        /// <summary>Fires this impact's opt-in SkillFx accents (Trail/Pillar/Nova/Spark/Rune/Bloom) on the resolved
        /// target. Lives here, not in the per-action handlers, so an accent can ride ANY action - a Nova on Frost
        /// Nova's Root, a Bloom on a Heal, a Spark on a DoT's StatusEffect - by just setting the flag on that impact.
        /// (BoltFx/LandFxRing stay in ApplyDamage/ApplyHeal: they're inherently damage/heal moments.)</summary>
        private static void EmitImpactFx(SpellContext ctx, Entity tgt, SpellImpact imp)
        {
            var world = ctx.Caster.World;
            var school = ctx.Spell.School;
            if (imp.TrailFx)  Visuals.SkillFx.Trail(world, ctx.Caster, tgt, school);
            if (imp.PillarFx) Visuals.SkillFx.Pillar(world, tgt, school);
            if (imp.NovaFx)   Visuals.SkillFx.Nova(world, tgt, 2.5f, school);
            if (imp.SparkFx)  Visuals.SkillFx.Spark(world, tgt, ctx.Caster, school);
            if (imp.RuneFx)   Visuals.SkillFx.Rune(world, tgt, 1.2f, school);
            if (imp.BloomFx)  Visuals.SkillFx.Bloom(world, tgt, school);
        }

        // ---- Targeting ----
        private static List<Entity> ResolveAim(SpellContext ctx)
        {
            var result = new List<Entity>();
            var aimed = GetAimedEntity(ctx.Caster, ctx.Aim, ctx.Spell.Range > 0 ? ctx.Spell.Range : 16f);

            switch (ctx.Spell.Target.Affinity)
            {
                case TargetAffinity.Ally:
                    // Smart-cast heal/buff: an allied player under the crosshair, else fall back to the caster.
                    result.Add(aimed != null && IsAlly(ctx.Caster, aimed) ? aimed : ctx.Caster);
                    break;
                case TargetAffinity.Enemy:
                    // Offensive: ignore a friendly pick (don't damage an ally you happened to aim at).
                    if (aimed != null && !IsAlly(ctx.Caster, aimed)) result.Add(aimed);
                    break;
                default:
                    if (aimed != null) result.Add(aimed);
                    break;
            }
            return result;
        }

        /// <summary>Allies = members of the same canparty group (self always included). A player-owned creature
        /// (the hunter's wolf) counts as its owner for friend/foe - so it's friendly to its owner and their party
        /// (a trap/AoE/zone never trips on or hits your own pet). An ownerless mob is never an ally. Side-aware
        /// (no static hook - safe in singleplayer): server uses <c>Manager.AreAllied</c>, client its party mirror.</summary>
        public static bool IsAlly(Entity caster, Entity target)
        {
            if (target == caster) return true;

            var cp = EffectivePlayer(caster);
            var tp = EffectivePlayer(target);
            if (cp == null || tp == null) return false;
            if (cp == tp || cp.PlayerUID == tp.PlayerUID) return true;

            var mod = caster.Api?.ModLoader?.GetModSystem<canparty.canpartyModSystem>();
            if (mod == null) return false;
            if (caster.Api.Side == EnumAppSide.Server)
                return mod.Manager?.AreAllied(cp.PlayerUID, tp.PlayerUID) ?? false;
            return mod.Client != null && mod.Client.IsInMyParty(cp.PlayerUID) && mod.Client.IsInMyParty(tp.PlayerUID);
        }

        /// <summary>The player an entity counts as for friend/foe: itself if it's a player, else its owner (a
        /// player-guarded pet - WA <see cref="AttrKeys.GuardedByPlayerUid"/>), else null (an ownerless mob).</summary>
        private static EntityPlayer? EffectivePlayer(Entity? e)
        {
            if (e is EntityPlayer p) return p;
            string? uid = e?.WatchedAttributes?.GetString(AttrKeys.GuardedByPlayerUid, null);
            if (string.IsNullOrEmpty(uid)) return null;
            return e!.World?.PlayerByUid(uid)?.Entity as EntityPlayer;
        }

        /// <summary>The entity the caster is aiming at: the client-picked target (close range), else a server look-ray pick.</summary>
        public static Entity? GetAimedEntity(EntityAgent caster, AimContext aim, float range)
        {
            var picked = aim?.TargetEntity;
            // Server authority: the entity id comes from the client - verify range (+2 blocks of latency/movement slack),
            // otherwise fall back to the server-side look-ray pick.
            if (picked != null && picked.Alive && caster.Pos.DistanceTo(picked.Pos.XYZ) <= range + 2f)
                return picked;
            return LookedAtAgent(caster, range);
        }

        private static List<Entity> ResolveArea(SpellContext ctx)
        {
            var caster = ctx.Caster;
            var t = ctx.Spell.Target;
            float range = ctx.Spell.Range > 0 ? ctx.Spell.Range : 6f;
            float hor = range * (t.AreaHorizontalRangeMultiplier > 0 ? t.AreaHorizontalRangeMultiplier : 1f);
            float ver = range * (t.AreaVerticalRangeMultiplier > 0 ? t.AreaVerticalRangeMultiplier : 1f);
            Vec3d pos = caster.Pos.XYZ;

            // Zone visual (e.g. Hallowed Ground's hallowed ground): a circular particle carpet over the affected disc,
            // emitted once per cast at floor level - independent of whether any enemies are present.
            t.AreaParticles?.EmitDisc(caster.World, pos, hor);

            var result = new List<Entity>();
            foreach (var e in caster.World.GetEntitiesAround(pos, hor, ver,
                         e => e.Alive && e is EntityAgent && (t.AreaIncludeCaster || e != caster)))
            {
                if (!WithinHorizontalRadius(pos, e, hor) && e != caster) continue; // round AoE, not the box GetEntitiesAround returns
                if (t.Affinity == TargetAffinity.Enemy && IsAlly(caster, e) && e != caster) continue;
                if (t.Affinity == TargetAffinity.Ally && !IsAlly(caster, e)) continue;
                result.Add(e);
            }
            return result;
        }

        private static Entity? LookedAtAgent(EntityAgent caster, float range)
        {
            Vec3d eye = caster.Pos.XYZ.Add(caster.LocalEyePos);
            Vec3f vf = caster.Pos.GetViewVector();
            Vec3d view = new Vec3d(vf.X, vf.Y, vf.Z);
            if (view.Length() < 1e-6) return null;
            view.Normalize();

            Entity? best = null;
            double bestAlong = double.MaxValue;
            foreach (var e in caster.World.GetEntitiesAround(eye, range, range,
                         e => e != caster && e.Alive && e is EntityAgent))
            {
                Vec3d toE = e.Pos.XYZ.Add(0, e.SelectionBox.Y2 * 0.5, 0) - eye;
                double along = toE.X * view.X + toE.Y * view.Y + toE.Z * view.Z; // distance along the look ray
                if (along <= 0 || along > range) continue;

                double lenSq = toE.X * toE.X + toE.Y * toE.Y + toE.Z * toE.Z;
                double perpSq = lenSq - along * along;                           // squared distance from the ray
                double radius = Math.Max(e.SelectionBox.XSize, e.SelectionBox.ZSize) * 0.5 + 0.4;
                if (perpSq > radius * radius) continue;                          // ray misses this entity

                if (along < bestAlong) { bestAlong = along; best = e; }
            }
            return best;
        }

        // ---- Impacts ----
        /// <summary>The 1.0-based damage multiplier a spell layers on top of its coefficient (its
        /// <see cref="Spell.DamageMultiplierStat"/>, or finisherDamage for a finisher). Single source for
        /// <see cref="FinisherAmount"/> and the empower path, so Death Blow's deferred missing-health part gets the same
        /// factor. 1 when the spell declares none; <paramref name="mulName"/> reports the stat used (for logging).</summary>
        private static float DamageMultiplier(SpellContext ctx, out string? mulName)
        {
            mulName = ctx.Spell.DamageMultiplierStat ?? (ctx.Spell.ComboFinisher ? StatKeys.FinisherDamage : null);
            if (mulName == null) return 1f;
            float statMul = ctx.Caster.Stats.GetBlended(mulName);
            return statMul > 0.01f ? statMul : 1f;
        }

        /// <summary>Fraction of the target's health that is missing (0..1); 0 when it has no health tree. Single
        /// source for Death Blow's scaling - used at cast time by <see cref="FinisherAmount"/> (direct hits) and at
        /// swing-landing time by EBSpellCaster.TakeEmpoweredMeleeBonus (empowered strikes).</summary>
        public static float MissingHealthFraction(Entity target)
        {
            var htree = target?.WatchedAttributes.GetTreeAttribute("health");
            float max = htree?.GetFloat("maxhealth") ?? 0f;
            return max > 0f ? System.Math.Clamp(1f - htree!.GetFloat("currenthealth") / max, 0f, 1f) : 0f;
        }

        /// <summary>SpellPower damage for an impact, including combo-finisher scaling: the impact's coeff plus its
        /// per-combo-point bonus (ctx.ComboPoints is 0 for non-finishers) × SpellPower, then × the spell's declared
        /// 1.0-based damage-multiplier stat (<see cref="Spell.DamageMultiplierStat"/>; finishers default to
        /// finisherDamage, e.g. Deadly Precision). The stat is seeded in EBRpgStats; the guard keeps a zeroed baseline
        /// from wiping the hit. Shared by ApplyDamage and ApplyEmpowerNextMelee.</summary>
        private static float FinisherAmount(SpellContext ctx, SpellImpact imp, Entity? target = null)
        {
            float coeff = imp.DamageSpellPowerCoefficient + imp.DamagePerComboPoint * ctx.ComboPoints;
            // "Death Blow" bonus (warrior Death Blow): fold the target's missing-health fraction into the coefficient, so
            // the whole hit - including the DamageMultiplier below (Headsman) - scales with how hurt it is.
            if (target != null && imp.DamageMissingHealthCoefficient > 0f)
                coeff += imp.DamageMissingHealthCoefficient * MissingHealthFraction(target);
            float mul = DamageMultiplier(ctx, out string? mulName);
            float amount = ctx.SpellPower * coeff * mul;

            // Balance troubleshooting: set the "debugDamage" global to 1 (config/core.json or the admin balance
            // editor - takes effect live) to log every term that feeds a reported hit, instead of
            // reverse-engineering it from the final number. Gated so the per-hit (and per-AoE-target) stat-term
            // iteration and format-arg boxing cost nothing in normal play.
            if (canrpgclasses.Core.Config.BalanceConfig.Global("debugDamage", 0f) > 0f)
            {
                var logger = ctx.Caster.Api.Logger;
                logger.VerboseDebug(
                    "[canrpgclasses] FinisherAmount spell={0} spellPower={1:0.###} coeff={2:0.###} combo={3} mul({4})={5:0.###} => amount={6:0.###}",
                    ctx.Spell.Id, ctx.SpellPower, coeff, ctx.ComboPoints, mulName ?? "-", mul, amount);
                if (mulName != null)
                    foreach (var kv in ctx.Caster.Stats[mulName].ValuesByKey)
                        logger.VerboseDebug("[canrpgclasses]   {0} term {1} = {2:0.###}", mulName, kv.Key, kv.Value.Value);
            }

            return amount;
        }

        private static void ApplyDamage(SpellContext ctx, Entity target, SpellImpact imp)
        {
            float baseAmount = FinisherAmount(ctx, imp, target);
            // "Icebreaker": hit harder when the target is in the control state this impact nominates (Ice Shard: rooted/chilled).
            bool shattered = imp.DamageVsControlledMultiplier > 1f && IsControlled(target, imp);
            float amount = shattered ? baseAmount * imp.DamageVsControlledMultiplier : baseAmount;
            if (amount <= 0) return;
            target.ReceiveDamage(new CanrpgDamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = ctx.Caster,
                CauseEntity = ctx.Caster,
                School = ctx.Spell.School,
                Type = DamageSchools.For(ctx.Spell.School).EngineType,
                KnockbackStrength = imp.Knockback
            }, amount);

            imp.Particles?.Emit(ctx.Caster.World, target);

            // Penitence (priest Discipline): a fraction of the hit also heals shielded party allies.
            if (imp.DamageSplashHealStat != null) ApplyDamageSplashHeal(ctx, amount, imp);

            // Ice Shard consumes the chill it just shattered - a frozen target can only be shattered once.
            if (shattered && imp.ConsumeControlOnHit && imp.DamageVsControlEffectId != null)
                RemoveEffect(target, imp.DamageVsControlEffectId);

            // The delivery bolt: caster → target, so the shot itself is visible (Arc Bolt).
            if (imp.BoltFx) Visuals.SkillFx.Arc(ctx.Caster.World, ctx.Caster, target, ctx.Spell.School);
            // (Trail/Pillar/Nova/Spark/Rune/Bloom accents fire generically in ApplyImpacts, so they also work on
            // non-Damage impacts like Freezing Burst's Root and Terrifying Scream's Fear.)

            // Forked Lightning: hop on from the UNSHATTERED base, so a chain can't multiply a one-target rider.
            if (imp.ChainJumps > 0) ChainDamage(ctx, imp, target, baseAmount);

            BreakCasterInvisibility(ctx.Caster); // spell damage doesn't go through DidAttack, so reveal here
        }

        /// <summary>Forked Lightning's jumps: from each link, find the nearest not-yet-hit ENEMY within
        /// <see cref="SpellImpact.ChainRadius"/> and hit it for the previous amplitude × falloff, up to ChainJumps
        /// times. The damage is the spell's own school (so attribution, resists and combat text match the direct
        /// hit); riders are deliberately not re-run (see SpellImpact.ChainJumps).</summary>
        private static void ChainDamage(SpellContext ctx, SpellImpact imp, Entity from, float baseAmount)
        {
            var caster = ctx.Caster;
            float radius = imp.ChainRadius > 0f ? imp.ChainRadius : 6f;
            var visited = new HashSet<long> { from.EntityId, caster.EntityId };
            var current = from;
            float amount = baseAmount;

            for (int i = 0; i < imp.ChainJumps; i++)
            {
                amount *= imp.ChainFalloff;
                if (amount <= 0f) return;

                var next = NearestChainLink(caster, current, radius, visited,
                    e => !IsAlly(caster, e)); // enemies only: lightning never arcs to your own party
                if (next == null) return;

                visited.Add(next.EntityId);
                Visuals.SkillFx.Arc(caster.World, current, next, ctx.Spell.School);
                next.ReceiveDamage(new CanrpgDamageSource
                {
                    Source = EnumDamageSource.Entity,
                    SourceEntity = caster,
                    CauseEntity = caster,
                    School = ctx.Spell.School,
                    Type = DamageSchools.For(ctx.Spell.School).EngineType
                }, amount);
                imp.Particles?.Emit(caster.World, next);
                current = next;
            }
        }

        /// <summary>Cascading Heal's jumps: from each link, find the most WOUNDED not-yet-healed party ally within
        /// ChainRadius and heal it for the previous amplitude × falloff. Amounts already carry the caster's
        /// healingPower (baked by <see cref="ApplyHeal"/> before the chain starts).</summary>
        private static void ChainHeal(SpellContext ctx, SpellImpact imp, Entity from, float baseHeal)
        {
            var caster = ctx.Caster;
            float radius = imp.ChainRadius > 0f ? imp.ChainRadius : 6f;
            var visited = new HashSet<long> { from.EntityId };
            var current = from;
            float heal = baseHeal;

            for (int i = 0; i < imp.ChainJumps; i++)
            {
                heal *= imp.ChainFalloff;
                if (heal <= 0f) return;

                // "Smart" Cascading Heal: pick who needs it most, not who stands closest.
                var next = MostWoundedChainLink(caster, current, radius, visited);
                if (next == null) return;

                visited.Add(next.EntityId);
                Visuals.SkillFx.Ribbon(caster.World, current, next, ctx.Spell.School);
                HealAlly(caster, next, heal);
                imp.Particles?.Emit(caster.World, next);
                current = next;
            }
        }

        private static Entity? NearestChainLink(Entity caster, Entity from, float radius, HashSet<long> visited,
            System.Func<Entity, bool> accept)
        {
            var center = from.Pos.XYZ;
            Entity? best = null;
            double bestDist = double.MaxValue;
            foreach (var e in from.World.GetEntitiesAround(center, radius, radius,
                         en => en.Alive && en is EntityAgent && !visited.Contains(en.EntityId)))
            {
                if (!WithinHorizontalRadius(center, e, radius) || !accept(e)) continue;
                double d = e.Pos.SquareDistanceTo(center);
                if (d < bestDist) { bestDist = d; best = e; }
            }
            return best;
        }

        /// <summary>Most wounded party ally (lowest health fraction) near <paramref name="from"/>, ignoring anyone
        /// already healed by this chain and anyone at full health. Players (and the caster) only - a chain heal
        /// shouldn't dump itself into a passing sheep.</summary>
        private static Entity? MostWoundedChainLink(Entity caster, Entity from, float radius, HashSet<long> visited)
        {
            var center = from.Pos.XYZ;
            Entity? best = null;
            float bestMissing = 0f;
            foreach (var e in from.World.GetEntitiesAround(center, radius, radius,
                         en => en.Alive && en is EntityPlayer && !visited.Contains(en.EntityId)))
            {
                if (!WithinHorizontalRadius(center, e, radius) || !IsAlly(caster, e)) continue;
                float missing = MissingHealthFraction(e);
                if (missing > bestMissing) { bestMissing = missing; best = e; }
            }
            return best;
        }

        /// <summary>Penitence: heals the caster's party allies (optionally only those carrying an absorb shield) within
        /// the impact's radius for <paramref name="damageDealt"/> × the caster's DamageSplashHealStat fraction (a
        /// 0-based ReductionStat - 0 until the Penitence talent sets it, so a spell can carry the rider harmlessly).
        /// The caster themselves count as an ally, so a shielded priest self-heals too.</summary>
        private static void ApplyDamageSplashHeal(SpellContext ctx, float damageDealt, SpellImpact imp)
        {
            if (damageDealt <= 0f) return;
            float frac = ctx.Caster.ReductionStat(imp.DamageSplashHealStat!);
            if (frac <= 0f) return;
            float heal = damageDealt * frac;
            if (heal <= 0f) return;

            var caster = ctx.Caster;
            float radius = imp.DamageSplashHealRadius > 0f ? imp.DamageSplashHealRadius : 30f;
            foreach (var e in caster.World.GetEntitiesAround(caster.Pos.XYZ, radius, radius,
                         en => en.Alive && en is EntityAgent && IsAlly(caster, en)))
            {
                if (imp.DamageSplashRequiresShield && !HasActiveShield(e)) continue;
                HealAlly(caster, e, heal);
            }
        }

        /// <summary>True if the entity currently carries an (unexpired) absorb shield - the AbsorbKey pool the shield
        /// impact / StunPatches maintain. Used by Penitence to gate its splash heal to shielded allies.</summary>
        private static bool HasActiveShield(Entity e)
        {
            var wa = e.WatchedAttributes;
            return e.World.ElapsedMilliseconds <= wa.GetLong(AbsorbUntilKey, 0) && wa.GetFloat(AbsorbKey, 0f) > 0f;
        }

        /// <summary>Applies a flat heal to an ally and shows the floating heal number on the caster's client (heals
        /// carry no CauseEntity, so the combat-text hook can't report them - mirror of <see cref="ApplyHeal"/>).</summary>
        private static void HealAlly(Entity caster, Entity target, float heal)
        {
            if (heal <= 0f) return;
            target.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, heal);
            if (caster is EntityPlayer hp && hp.Player is IServerPlayer sp)
            {
                var mod = canrpgclassesModSystem.For(caster.Api);
                var pos = target.Pos;
                mod?.ServerChannel?.SendPacket(new canrpgclasses.Core.Net.DamageNumberPacket
                {
                    X = pos.X,
                    Y = pos.Y + target.SelectionBox.Y2 * 0.6,
                    Z = pos.Z,
                    Amount = heal,
                    IsHeal = true
                }, sp);
            }
        }

        /// <summary>Lingering damage zone (Hallowed Ground): centred on the cast point, re-applies AoE damage every
        /// ZoneTickSeconds and refreshes the particle carpet on a finer ZoneParticleSeconds cadence (so it stays
        /// solid instead of pulsing once per damage tick), for ZoneDurationSeconds. Use with Target=Caster - the
        /// zone scans the area itself each tick, so it doesn't follow the caster after the cast. Driven by
        /// <see cref="ZoneManager"/>'s single shared tick listener (which also cancels it if the caster dies).</summary>
        private static void ApplyDamageZone(SpellContext ctx, Entity target, SpellImpact imp)
            => ZoneManager.Add(ctx, imp);

        /// <summary>True if the target is in the control state the <paramref name="imp"/> nominates for its shatter bonus:
        /// a WA bool flag it names (DamageVsControlFlag, e.g. Freezing Burst's Rooted) OR an effectshud effect it names
        /// (DamageVsControlEffectId, e.g. the mage's own "canrpg_chilled"). Either match qualifies; a warrior's generic
        /// walkslow does not trigger Ice Shard because Ice Shard nominates "canrpg_chilled", not "walkslow".</summary>
        private static bool IsControlled(Entity target, SpellImpact imp)
        {
            if (imp.DamageVsControlFlag != null && target?.WatchedAttributes?.GetBool(imp.DamageVsControlFlag) == true) return true;
            if (imp.DamageVsControlEffectId != null &&
                (target?.GetBehavior<EBEffects>()?.GetEffectTier(imp.DamageVsControlEffectId) ?? -1) >= 0) return true;
            return false;
        }

        /// <summary>Server-side: forcibly ends an effectshud effect on the target right now (mirrors the expiry path
        /// the tick loop uses - OnExpire to undo any stat it set, then remove + sync the removal to clients). Used to
        /// consume the chill an Ice Shard shatters. No-op if the effect isn't present.</summary>
        public static void RemoveEffect(Entity target, string effectId)
        {
            var ebea = target?.GetBehavior<EBEffects>();
            if (ebea == null || !ebea.TryGetEffect(effectId, out var eff)) return;
            eff.OnExpire();
            ebea.activeEffects.Remove(effectId);
            ebea.SendEffectToClient(null, new System.Collections.Generic.HashSet<string> { effectId });
        }

        /// <summary>True if the target carries at least one of the named effectshud effects. The cast-time half of the
        /// Instant Mend contract (<see cref="Spell.RequiresTargetEffectIds"/>) - shared with the caster's gate so the
        /// check that refuses the cast and the one that consumes the HoT can never disagree. Null/empty = true.</summary>
        public static bool HasAnyEffect(Entity? target, string[]? effectIds)
        {
            if (effectIds == null || effectIds.Length == 0) return true;
            var ebea = target?.GetBehavior<EBEffects>();
            if (ebea == null) return false;
            foreach (var id in effectIds) if (ebea.GetEffectTier(id) >= 0) return true;
            return false;
        }

        /// <summary>Removes the FIRST of the named effects the target carries (Instant Mend eating one HoT, not all of
        /// them) - the array's order is the consumption priority. No-op when the list is null or nothing matches.</summary>
        private static void ConsumeTargetEffects(Entity target, string[]? effectIds)
        {
            if (effectIds == null) return;
            var ebea = target.GetBehavior<EBEffects>();
            if (ebea == null) return;
            foreach (var id in effectIds)
            {
                if (ebea.GetEffectTier(id) < 0) continue;
                RemoveEffect(target, id);
                return;
            }
        }

        /// <summary>True if entity <paramref name="e"/> is within <paramref name="hor"/> blocks of <paramref name="center"/>
        /// on the horizontal plane - turns GetEntitiesAround's square box into a round AoE.</summary>
        internal static bool WithinHorizontalRadius(Vec3d center, Entity e, float hor)
        {
            double dx = e.Pos.X - center.X, dz = e.Pos.Z - center.Z;
            return dx * dx + dz * dz <= hor * hor;
        }

        /// <summary>Regroup: instantly clears the caster's cooldowns for the listed spells. Keys may be bare
        /// local ids or full ids; cooldown keys are the spell id (none of the targeted skills use a shared group).</summary>
        private static void ApplyResetCooldowns(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (imp.ResetCooldownKeys == null) return;
            var cd = ctx.Caster.GetBehavior<canrpgclasses.Core.EB.EBSpellCooldowns>();
            if (cd == null) return;
            foreach (var key in imp.ResetCooldownKeys)
            {
                string full = key.Contains(':') ? key : canrpgclassesModSystem.ModId + ":" + key;
                cd.ClearCooldown(full);
            }
            imp.Particles?.Emit(ctx.Caster.World, ctx.Caster);
        }

        /// <summary>Ends the caster's break-on-attack invisibility and triggers the Stealth combat gate. Called
        /// after dealing spell damage (melee is handled by InvisibilityEffect.DidAttack itself) and when the
        /// player breaks a block (revealing actions, hooked from the server's DidBreakBlock event).</summary>
        public static void BreakCasterInvisibility(Entity caster)
        {
            var ce = caster.GetBehavior<EBEffects>();
            if (ce != null && ce.TryGetEffect("invisibility", out var eff)
                && eff is InvisEffect inv && inv.BreakOnAttack)
                eff.SetExpiryImmediately();

            caster.GetBehavior<canrpgclasses.Core.EB.EBSpellCaster>()?.NotifyStealthBrokenByAttack();
        }

        /// <summary>Stores a bonus for the caster's next melee hit (sinister_strike) instead of dealing damage now.
        /// EBSpellCaster.DidAttack consumes it: extra damage on the struck target + combo if the spell is a builder.</summary>
        private static void ApplyEmpowerNextMelee(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var wa = ctx.Caster.WatchedAttributes;
            // Same damage formula as ApplyDamage (combo scaling + finisherDamage), but ARMED onto the next melee swing
            // instead of dealt now - so an EmpowerNextMelee impact can be a builder (sinister_strike, perCombo 0) OR a
            // combo finisher (eviscerate): the finisher's burst rides a real melee hit instead of a separate,
            // i-frame-dropped one. ctx.ComboPoints is the spent combo (captured before Deliver), 0 for non-finishers.
            float bonus = FinisherAmount(ctx, imp);
            wa.SetFloat(EmpowerDamageKey, bonus);
            // Death Blow-style deferred bonus: the missing-health part can't be evaluated here (no melee target yet), so
            // store its MAX magnitude (at 100% missing HP, including the DamageMultiplierStat) and let the landing
            // swing scale it by the struck target's actual missing fraction. Written UNCONDITIONALLY (0 for normal
            // empowers) so a fresh arm - e.g. Brutal Strike over a prior Death Blow - overwrites and never leaks a stale
            // missing part onto the next swing. Only one empower is ever armed (SetFloat overwrites, never stacks).
            float missingMax = imp.DamageMissingHealthCoefficient > 0f
                ? ctx.SpellPower * imp.DamageMissingHealthCoefficient * DamageMultiplier(ctx, out _)
                : 0f;
            wa.SetFloat(EmpowerMissingDamageKey, missingMax);
            wa.SetFloat(EmpowerKnockbackKey, imp.Knockback);
            wa.SetInt(EmpowerSchoolKey, (int)ctx.Spell.School); // physical school folds into the swing, magical lands as its own hit
            wa.SetInt(EmpowerComboKey, ctx.Spell.ComboBuilder ? ctx.Spell.ComboPointsGenerated : 0);
            wa.SetString(EmpowerSpellKey, ctx.Spell.Id); // so the consuming strike can find this spell's hit particles

            // The window deadline lives in a transient behavior field (not a persisted WA timestamp) so it can't go
            // stale across sessions and "expire" every hit. See EBSpellCaster.ArmEmpowerWindow / empowerUntilMs.
            var caster = ctx.Caster.GetBehavior<canrpgclasses.Core.EB.EBSpellCaster>();
            // Only one empower is armed at a time (the WA bundle above overwrote any prior one) - clear the previous
            // spell's HUD buff too so e.g. arming Death Blow over a pending Brutal Strike doesn't leave both icons up.
            caster?.ClearEmpowerHudBuffs();
            caster?.ArmEmpowerWindow(imp.EmpowerWindowSeconds);

            // Cosmetic HUD buff with a live countdown of the window (the bonus itself is the WA state above). Per-spell
            // id → each arming spell shows its own icon; unregistered ids no-op. Removed when the empowered hit lands
            // (EBSpellCaster.ApplyEmpoweredStrike); otherwise it self-expires with the window.
            ApplyEffect(ctx.Caster, canrpgclasses.Core.Effects.EmpowerEffectIds.For(ctx.Spell.LocalId), 1, imp.EmpowerWindowSeconds);
        }

        /// <summary>Disarm. Players: knock the active weapon out of hand into another hotbar slot (no ground
        /// loot - the player sees it move). Mobs (no hotbar): a weakmelee debuff for the duration.</summary>
        private static void ApplyDisarm(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (target == null || target == ctx.Caster) return;

            if ((target as EntityPlayer)?.Player is IPlayer plr && plr.InventoryManager != null)
            {
                var hotbar = plr.InventoryManager.GetHotbarInventory();
                int active = plr.InventoryManager.ActiveHotbarSlotNumber;
                ItemSlot? activeSlot = (hotbar != null && active >= 0 && active < hotbar.Count) ? hotbar[active] : null;
                if (activeSlot != null && !activeSlot.Empty)
                {
                    ItemSlot? dest = FindDisarmSlot(hotbar!, active);
                    if (dest != null && activeSlot.TryFlipWith(dest))
                    {
                        activeSlot.MarkDirty();
                        dest.MarkDirty();
                        if (plr is IServerPlayer sp)
                            canrpgclassesModSystem.For(ctx.Caster.Api)?.ServerChannel?
                                .SendPacket(new SpellMessagePacket { Message = Lang.Get("canrpgclasses:msg-disarmed") }, sp);
                    }
                }
                return;
            }

            int seconds = (int)Math.Ceiling(imp.StatusEffectDuration > 0 ? imp.StatusEffectDuration : 4f);
            var effMod = target.Api.ModLoader.GetModSystem<EffectsHud>();
            if (effMod?.effects != null && effMod.effects.TryGetValue("weakmelee", out var type)
                && Activator.CreateInstance(type) is EffectBase eff)
            {
                eff.Tier = 4;
                eff.SetExpiryInRealSeconds(seconds);
                eff.positive = false;
                EffectsHud.ApplyEffectOnEntity(target, eff);
            }
        }

        private static ItemSlot? FindDisarmSlot(IInventory hotbar, int active)
        {
            for (int i = 0; i < hotbar.Count; i++)
                if (i != active && hotbar[i].Empty) return hotbar[i];
            for (int i = 0; i < hotbar.Count; i++)
                if (i != active) return hotbar[i];
            return null;
        }

        private static void ApplyHeal(SpellContext ctx, Entity target, SpellImpact imp)
        {
            // Combo finishers (e.g. Radiant Word spending Holy Power) heal more per spent point.
            float coeff = imp.HealSpellPowerCoefficient + imp.HealPerComboPoint * ctx.ComboPoints;
            // healingPower: a HEALING-DONE multiplier on the caster (1.0 by default; raised by the healing tree's
            // mastery - see RpgClassDef.TreeMasteries), so a healing-specced player heals noticeably more.
            float healMul = ctx.Caster.Stats.GetBlended(StatKeys.HealingPower);
            float heal = ctx.SpellPower * coeff * healMul;
            // Death Blow-heal (Second Breath): add a fraction of the target's missing health - big when hurt, small at full.
            if (imp.HealMissingHealthFraction > 0f)
            {
                var htree = target.WatchedAttributes.GetTreeAttribute("health");
                if (htree != null)
                {
                    float missing = Math.Max(0f, htree.GetFloat("maxhealth") - htree.GetFloat("currenthealth"));
                    heal += missing * imp.HealMissingHealthFraction;
                }
            }
            if (heal <= 0) return;
            target.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Heal
            }, heal);

            imp.Particles?.Emit(ctx.Caster.World, target);
            if (imp.LandFxRing) Visuals.SkillFx.RingWave(ctx.Caster.World, target.Pos.XYZ, imp.LandFxRadius, ctx.Spell.School);

            // Floating heal number on the caster's client, over the healed target (heals carry no CauseEntity,
            // so the CombatTextPatches damage hook can't see them - report it here instead).
            if (ctx.Caster is EntityPlayer hp && hp.Player is Vintagestory.API.Server.IServerPlayer sp)
            {
                var mod = canrpgclassesModSystem.For(ctx.Caster.Api);
                var pos = target.Pos;
                mod?.ServerChannel?.SendPacket(new canrpgclasses.Core.Net.DamageNumberPacket
                {
                    X = pos.X,
                    Y = pos.Y + target.SelectionBox.Y2 * 0.6,
                    Z = pos.Z,
                    Amount = heal,
                    IsHeal = true
                }, sp);
            }

            // Instant Mend: eat the first heal-over-time the target carries (the array's order is the priority). Done
            // after the heal so the consumed HoT's own tick can't be lost to a race, and only on a heal that actually
            // landed - a fizzled cast never spends the druid's ramp-up.
            ConsumeTargetEffects(target, imp.ConsumeTargetEffectIds);

            // Cascading Heal: hop on to the most wounded allies nearby. Chains off the spell-power heal only - the
            // missing-health rider (Second Breath) stays a single-target effect.
            if (imp.ChainJumps > 0) ChainHeal(ctx, imp, target, ctx.SpellPower * coeff * healMul);
        }

        private static void ApplyStatusEffect(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (string.IsNullOrEmpty(imp.StatusEffectId)) return;

            var effMod = ctx.Caster.Api.ModLoader.GetModSystem<EffectsHud>();
            if (effMod?.effects == null || !effMod.effects.TryGetValue(imp.StatusEffectId!, out var effectType)) return;

            int tier;
            if (imp.StatusEffectApplyMode == StatusApplyMode.Add)
            {
                int current = target.GetBehavior<EBEffects>()?.GetEffectTier(imp.StatusEffectId!) ?? 0;
                if (current < 0) current = 0;
                tier = Math.Min(imp.StatusEffectAmplifierCap, current + imp.StatusEffectAmplifier);
            }
            else
            {
                tier = imp.StatusEffectAmplifier;
            }

            if (Activator.CreateInstance(effectType) is not EffectBase eff) return;
            eff.Tier = tier;
            float duration = imp.StatusEffectDuration + imp.DurationPerComboPoint * ctx.ComboPoints;
            if (duration > 0) eff.SetExpiryInRealSeconds((int)Math.Ceiling(duration));
            eff.positive = effMod.effectsPosNeg.TryGetValue(imp.StatusEffectId!, out var pos) ? pos : true;

            if (eff is InvisEffect inv) inv.BreakOnAttack = imp.InvisBreaksOnAttack;

            // DoT/HoT power snapshot (priest Soothing Prayer / Shadow Brand / ...): a ticking effect whose per-tick
            // heal/damage scales off the caster's spell power. The executor can't reach the caster from inside an
            // effect, so hand it the snapshot (caster id + spell power) now; the effect reads its own coeffPerTick
            // from config. Stored on the effect instance (survives relog). Harmless for all other effects.
            if (eff is canrpgclasses.Core.Effects.ISnapshotEffect snap)
                snap.CaptureCaster(ctx.Caster.EntityId, ctx.SpellPower);

            EffectsHud.ApplyEffectOnEntity(target, eff);

            imp.Particles?.Emit(ctx.Caster.World, target);
        }

        private static void ApplyTeleport(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var caster = ctx.Caster;
            Vec3d dest;

            // Puff at the spot the caster is leaving, before it moves - a "left the body" visual (Shadow Stride's
            // grey smoke). Reads the caster's box at its current (pre-teleport) position.
            imp.Particles?.Emit(caster.World, caster);

            if (imp.TeleportMode == TeleportMode.BehindTarget && target != null && target != caster)
            {
                Vec3d tp = target.Pos.XYZ;
                // Behind the TARGET's own facing (same forward convention as DamageModifiers.IsBehind), not the
                // caster's approach angle - otherwise landing only ends up behind the target when the caster
                // happened to already be roughly in front of them, and lands to the side/front otherwise.
                float yaw = target.Pos.Yaw;
                Vec3d forward = new Vec3d(Math.Sin(yaw), 0, Math.Cos(yaw));
                dest = tp - forward * imp.TeleportDistance;
            }
            else
            {
                dest = ResolveForwardTeleport(caster, imp.TeleportDistance);
            }

            if (imp.TeleportFaceTarget && target != null && target != caster)
            {
                // Turn to face the target from the landing spot. face points from the landing spot to the
                // target; Math.Atan2(face.X, face.Z) is the yaw that actually aims the player there once it
                // goes through TeleportTo. TeleportTo(epos with Yaw) also snaps the owner's own camera in
                // this VS build, so no separate view-yaw packet is needed.
                Vec3d face = target.Pos.XYZ - dest;
                float yaw = (float)Math.Atan2(face.X, face.Z);
                var epos = caster.Pos.Copy();
                epos.SetPos(dest.X, dest.Y, dest.Z);
                epos.Yaw = yaw;
                caster.TeleportTo(epos);
                // TeleportTo's yaw is unreliable for the owner's own camera (async chunk-load), so also tell
                // the owning client to snap its MouseYaw explicitly.
                SendViewYaw(caster, yaw);
            }
            else
            {
                caster.TeleportToDouble(dest.X, dest.Y, dest.Z);
            }
        }

        /// <summary>Collision-safe forward teleport (Flicker): walks from the caster toward its aim in small steps and
        /// stops at the last position whose body box is free - so you can't blink THROUGH a wall. Lets the blink climb
        /// up to 2 blocks to clear a low step/obstacle; descents settle via gravity after landing.</summary>
        private static Vec3d ResolveForwardTeleport(EntityAgent caster, float distance)
        {
            var world = caster.World;
            var ba = world.BlockAccessor;
            var ct = world.CollisionTester;
            var box = caster.CollisionBox;

            Vec3f view = caster.Pos.GetViewVector();
            Vec3d fwd = new Vec3d(view.X, 0, view.Z);
            if (fwd.Length() < 1e-3) fwd.Set(0, 0, 1);
            fwd.Normalize();

            Vec3d start = caster.Pos.XYZ;
            if (box == null) return start + fwd * distance; // no body box (shouldn't happen for players) - old behaviour

            Vec3d best = start;
            const double step = 0.2;
            for (double d = step; d <= distance + 1e-6; d += step)
            {
                Vec3d p = start + fwd * d;
                Vec3d? free = null;
                for (int dy = 0; dy <= 2; dy++)
                {
                    Vec3d cand = new Vec3d(p.X, start.Y + dy, p.Z);
                    if (!ct.IsColliding(ba, box, cand, false)) { free = cand; break; }
                }
                if (free == null) break; // solid at every height ahead -> a wall; stop at the last free spot
                best = free;
            }
            return best;
        }

        /// <summary>Asks the owning client to snap its view yaw (the owner camera is rendered from MouseYaw,
        /// which the server can't set directly; TeleportTo's yaw doesn't reliably reach it).</summary>
        private static void SendViewYaw(EntityAgent caster, float yaw)
        {
            if ((caster as EntityPlayer)?.Player is not IServerPlayer sp) return;
            var mod = canrpgclassesModSystem.For(caster.Api);
            mod?.ServerChannel?.SendPacket(new SetViewYawPacket { Yaw = yaw }, sp);
        }

        /// <summary>
        /// Coats the caster's held weapon with our own "canrpgWeaponCoat" tag. The matching effect
        /// (poison/walkslow/weakmelee) is applied by us on each melee hit (EBSpellCaster.DidAttack →
        /// ApplyOwnWeaponCoating), deterministically and independently of simplealchemy.
        /// </summary>
        private static void ApplyCoatWeapon(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (ctx.Caster is not EntityPlayer player) return;
            var slot = player.Player?.InventoryManager?.ActiveHotbarSlot;
            var stack = slot?.Itemstack;
            if (stack == null) return;

            var tree = new TreeAttribute();
            tree.SetString("potionId", string.IsNullOrEmpty(imp.StatusEffectId) ? "poison" : imp.StatusEffectId);
            tree.SetInt("tier", Math.Max(1, imp.StatusEffectAmplifier));
            tree.SetInt("charges", imp.CoatCharges > 0 ? imp.CoatCharges : 6);
            stack.Attributes[WeaponCoatKey] = tree;
            slot.MarkDirty();

            imp.Particles?.Emit(ctx.Caster.World, ctx.Caster);
        }

        /// <summary>
        /// Charge: launches the caster toward the target with a gap-proportional impulse, so a farther target
        /// gets a bigger hop. It goes through the engine's knockback channel because a server-side Pos.Motion
        /// add doesn't move a player. Real movement, not a teleport.
        /// </summary>
        private static void ApplyDash(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var caster = ctx.Caster;
            Vec3d cp = caster.Pos.XYZ;

            double dx, dy, dz;
            float range = ctx.Spell.Range > 0 ? ctx.Spell.Range : 16f;
            // A forward-strength dash (Leaping Charge) always leaps where you face; a disengage always leaps backward.
            // Only a plain dash (Charge) homes onto the aimed enemy - and never onto an ally (dash forward instead).
            var aimed = (imp.DashReverse || imp.DashForwardStrength > 0f) ? null : GetAimedEntity(caster, ctx.Aim, range);
            if (aimed != null && aimed != caster && IsAlly(caster, aimed)) aimed = null;

            if (aimed != null && aimed != caster)
            {
                Vec3d tp = aimed.Pos.XYZ;
                dx = (tp.X - cp.X) / 30.0;
                dz = (tp.Z - cp.Z) / 30.0;

                // Clamp horizontal magnitude so a very distant target doesn't fling the caster absurdly far.
                double h = Math.Sqrt(dx * dx + dz * dz);
                const double maxH = 0.8;
                if (h > maxH) { dx *= maxH / h; dz *= maxH / h; }

                // Low hop: a big upward kick keeps the caster airborne (no friction) and overshoots.
                dy = GameMath.Clamp((tp.Y - cp.Y) / 30.0 + 0.1, 0.1, 0.35);
            }
            else
            {
                // No target: dash along the view direction (or opposite, for a disengage leap). Leaping Charge raises
                // the horizontal strength + upward arc for a long, high repositioning jump (DashForwardStrength).
                Vec3f view = caster.Pos.GetViewVector();
                double sign = imp.DashReverse ? -1.0 : 1.0;
                double fwd = imp.DashForwardStrength > 0f ? imp.DashForwardStrength : 0.5;
                dx = view.X * fwd * sign;
                dz = view.Z * fwd * sign;
                dy = imp.DashUpward > 0f ? imp.DashUpward : (imp.DashReverse ? 0.22 : 0.12);
            }

            var wa = caster.WatchedAttributes;
            wa.SetDouble("kbdirX", dx);
            wa.SetDouble("kbdirY", dy);
            wa.SetDouble("kbdirZ", dz);
            wa.SetFloat("onHurtDir", (float)Math.Atan2(dx, dz));
            wa.SetInt("onHurtCounter", wa.GetInt("onHurtCounter", 0) + 1);
            // Tiny, alternating "hurt" so the knockback fires without a damage flash (flash needs > 0.05).
            wa.SetFloat("onHurt", wa.GetFloat("onHurt", 0f) > 0.0015f ? 0.001f : 0.002f);
        }

        /// <summary>Stun impact: routes to the unified control layer (shared incap DR with Fear, motion root, blind,
        /// mob AI freeze). See <see cref="canrpgclasses.Core.Control.ControlState"/>.</summary>
        private static void ApplyStun(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (target == null || target == ctx.Caster) return;
            float dur = (imp.StatusEffectDuration > 0 ? imp.StatusEffectDuration : 2f) + imp.DurationPerComboPoint * ctx.ComboPoints;
            if (canrpgclasses.Core.Control.ControlState.Apply(target, canrpgclasses.Core.Control.ControlType.Stun, dur,
                    ctx.Caster, imp.StunBreaksOnDamage, imp.StunBlindsVision, imp.StunVisionIntensity))
                imp.Particles?.Emit(ctx.Caster.World, target);
        }

        /// <summary>Fear impact (priest Terrifying Scream): mobs flee via their AI, a player loses control and wanders
        /// erratically. Shares the incap DR bracket with Stun. See <see cref="canrpgclasses.Core.Control.ControlState"/>.</summary>
        private static void ApplyFear(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (target == null || target == ctx.Caster) return;
            float dur = imp.StatusEffectDuration > 0 ? imp.StatusEffectDuration : 5f;
            if (canrpgclasses.Core.Control.ControlState.Apply(target, canrpgclasses.Core.Control.ControlType.Fear, dur, ctx.Caster))
                imp.Particles?.Emit(ctx.Caster.World, target);
        }

        /// <summary>Silence impact (priest Silence): blocks the target from casting for a duration (its own DR bracket).
        /// See <see cref="canrpgclasses.Core.Control.ControlState"/>.</summary>
        private static void ApplySilence(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (target == null || target == ctx.Caster) return;
            float dur = imp.StatusEffectDuration > 0 ? imp.StatusEffectDuration : 4f;
            if (canrpgclasses.Core.Control.ControlState.Apply(target, canrpgclasses.Core.Control.ControlType.Silence, dur, ctx.Caster))
                imp.Particles?.Emit(ctx.Caster.World, target);
        }

        /// <summary>Root impact (mage Freezing Burst): the target can't move but can still cast/attack. Own DR bracket.</summary>
        private static void ApplyRoot(SpellContext ctx, Entity target, SpellImpact imp)
        {
            // Area roots never catch the caster; a self-root (Frozen Shell) opts in explicitly via AffectCaster.
            if (target == null || (target == ctx.Caster && !imp.AffectCaster)) return;
            float dur = imp.StatusEffectDuration > 0 ? imp.StatusEffectDuration : 4f;
            if (canrpgclasses.Core.Control.ControlState.Apply(target, canrpgclasses.Core.Control.ControlType.Root, dur, ctx.Caster))
                imp.Particles?.Emit(ctx.Caster.World, target);
        }

        /// <summary>Full incapacitate that ends on the first hit, plus a sheep model on player targets.</summary>
        private static void ApplyTransmute(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (target == null || target == ctx.Caster) return;
            float dur = imp.StatusEffectDuration > 0 ? imp.StatusEffectDuration : 8f;
            if (canrpgclasses.Core.Control.ControlState.Apply(target, canrpgclasses.Core.Control.ControlType.Transmute, dur, ctx.Caster, breakOnDamage: true))
                imp.Particles?.Emit(ctx.Caster.World, target);
        }

        /// <summary>Grant-instant-cast impact (Clear Mind): opens an "instant next cast" window on the caster for
        /// StatusEffectDuration seconds, and applies StatusEffectId (if set) as a cosmetic HUD marker for that window
        /// (cleared automatically when the free cast is used - see EBSpellCaster.GrantInstantCast).</summary>
        private static void ApplyGrantInstantCast(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var caster = ctx.Caster;
            float window = imp.StatusEffectDuration > 0 ? imp.StatusEffectDuration : 15f;
            string? marker = string.IsNullOrEmpty(imp.StatusEffectId) ? null : imp.StatusEffectId;
            caster.GetBehavior<canrpgclasses.Core.EB.EBSpellCaster>()?.GrantInstantCast("*", window, marker);
            if (marker != null) ApplyEffect(caster, marker, 1, window);
        }

        /// <summary>Evasion: for a duration the target (self) gains a chance to fully dodge incoming physical
        /// attacks. Stores the chance + expiry in WatchedAttributes (synced, set once); the dodge roll happens in
        /// StunPatches.Prefix_ReceiveDamage, and <see cref="canrpgclasses.Core.EB.EBCombatState"/> clears the flag
        /// when it expires (and on (re)load) - no fire-and-forget timer to leak or to mis-fire across a restart.</summary>
        private static void ApplyEvasion(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (target == null) return;
            float dur = imp.StatusEffectDuration > 0 ? imp.StatusEffectDuration : 5f;
            var wa = target.WatchedAttributes;
            long until = target.World.ElapsedMilliseconds + (long)(dur * 1000f);

            // Refresh: keep the higher chance / later expiry so re-casting can't weaken an active Evasion.
            bool active = target.World.ElapsedMilliseconds <= wa.GetLong(CombatFlags.EvasionUntil, 0);
            float chance = Math.Max(imp.EvasionChance, active ? wa.GetFloat(CombatFlags.Evasion, 0f) : 0f);
            wa.SetFloat(CombatFlags.Evasion, chance);
            wa.SetLong(CombatFlags.EvasionUntil, Math.Max(until, active ? wa.GetLong(CombatFlags.EvasionUntil, 0) : 0));

            // Cosmetic HUD buff so the player sees Evasion is active + its countdown (the dodge logic is the flag above).
            ApplyEffect(target, canrpgclasses.Core.Effects.EvasionEffectId.Id, 1, dur);

            imp.Particles?.Emit(ctx.Caster.World, target);
        }

        /// <summary>Signal Flare: ends invisibility on every enemy within the spell's Range around the caster - flushes
        /// out stealthed foes. Allies/self are skipped. Reveals all invisibility (not just break-on-attack), so a
        /// hidden rogue is forced out. Pair with a rising-flare particle on the spell for the visual.</summary>
        private static void ApplyReveal(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var caster = ctx.Caster;
            float range = ctx.Spell.Range > 0 ? ctx.Spell.Range : 12f;
            foreach (var e in caster.World.GetEntitiesAround(caster.Pos.XYZ, range, range,
                         en => en.Alive && en is EntityAgent && en != caster))
            {
                if (IsAlly(caster, e)) continue;
                if (e.GetBehavior<EBEffects>() is { } ce && ce.TryGetEffect("invisibility", out var eff))
                {
                    eff.SetExpiryImmediately();
                    imp.Particles?.Emit(caster.World, e); // puff on whoever was revealed
                }
            }
            imp.Particles?.Emit(caster.World, caster); // flare burst at the caster
        }

        /// <summary>Toggle the caster's active group aura. Sets <c>canrpgActiveAura</c> to this spell's id, or clears
        /// it if this aura was already active (one aura at a time = a stance). The effect is kept refreshed on nearby
        /// party allies by <see cref="canrpgclasses.Core.EB.EBAuras"/>, which reads it back from this spell.</summary>
        private static void ApplyToggleAura(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var wa = ctx.Caster.WatchedAttributes;
            string id = ctx.Spell.Id;
            wa.SetString(AttrKeys.ActiveAura, wa.GetString(AttrKeys.ActiveAura, "") == id ? "" : id);
        }

        /// <summary>Applies an effectshud effect to an entity (id + tier + duration), Set-mode. Shared by auras
        /// (<see cref="canrpgclasses.Core.EB.EBAuras"/> refreshes a short one each tick) and any code that needs a
        /// plain timed buff without a full SpellContext. No-op if the effect id isn't registered.</summary>
        public static void ApplyEffect(Entity target, string effectId, int tier, float seconds)
        {
            if (target == null || string.IsNullOrEmpty(effectId)) return;
            var effMod = target.Api.ModLoader.GetModSystem<EffectsHud>();
            if (effMod?.effects == null || !effMod.effects.TryGetValue(effectId, out var effectType)) return;
            if (Activator.CreateInstance(effectType) is not EffectBase eff) return;
            eff.Tier = tier;
            if (seconds > 0) eff.SetExpiryInRealSeconds((int)Math.Ceiling(seconds));
            eff.positive = effMod.effectsPosNeg.TryGetValue(effectId, out var pos) ? pos : true;
            EffectsHud.ApplyEffectOnEntity(target, eff);
        }

        /// <summary>Grants a damage-absorb shield on the target (self or ally): a pool that the damage patch
        /// soaks incoming hits from until it's depleted or expires. Scales with (holy) spell power.</summary>
        private static void ApplyShield(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (target == null) return;
            float amount = ctx.SpellPower * imp.ShieldSpellPowerCoefficient;
            if (amount <= 0) return;

            var wa = target.WatchedAttributes;
            // Refresh: take the larger pool and the later deadline, so re-casting a shorter/weaker shield can't
            // shrink an existing one's remaining amount or duration (mirrors ApplyStun's Math.Max on StunUntil).
            long existingUntil = wa.GetLong(AbsorbUntilKey, 0);
            long now = target.World.ElapsedMilliseconds;
            float existing = now <= existingUntil ? wa.GetFloat(AbsorbKey, 0f) : 0f;
            long newUntil = now + (long)(imp.ShieldDurationSeconds * 1000f);
            wa.SetFloat(AbsorbKey, Math.Max(existing, amount));
            wa.SetLong(AbsorbUntilKey, Math.Max(existingUntil, newUntil));
            // Cosmetic: the dome renderer can't compare AbsorbUntil (server ElapsedMilliseconds) against the client's
            // own clock - different counters. Ship the remaining lifetime in seconds; the client arms its own deadline.
            wa.SetFloat(CombatFlags.AbsorbDuration, (Math.Max(existingUntil, newUntil) - now) / 1000f);

            // Cosmetic (read only by ShieldDomeRenderer): tint the dome by the spell's school (reusing the same
            // palette as particles/trails), and render frost shields (Frozen Shell / Ice Ward) as an angular ice
            // crystal instead of a smooth dome.
            var sc = Spells.ParticleSpec.ForSchool(ctx.Spell.School);
            wa.SetInt(CombatFlags.AbsorbColor, ColorUtil.ToRgba(sc.ColorA, sc.ColorR, sc.ColorG, sc.ColorB));
            // Explicit dome style if the impact set one (Frozen Shell vs Ice Ward must look different), else auto by school.
            int style = imp.ShieldDomeStyle >= 0 ? imp.ShieldDomeStyle : (ctx.Spell.School == Spells.SpellSchool.Frost ? 1 : 0);
            wa.SetInt(CombatFlags.AbsorbStyle, style);

            imp.Particles?.Emit(ctx.Caster.World, target);
        }

        /// <summary>Removes negative effectshud effects from the target (self or ally). CleanseMax 0 = all.</summary>
        private static void ApplyCleanse(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var eff = target?.GetBehavior<EBEffects>();
            if (eff?.activeEffects == null) return;

            var toRemove = new List<EffectBase>();
            foreach (var kv in eff.activeEffects)
            {
                if (kv.Value != null && !kv.Value.positive)
                {
                    toRemove.Add(kv.Value);
                    if (imp.CleanseMax > 0 && toRemove.Count >= imp.CleanseMax) break;
                }
            }
            foreach (var e in toRemove) e.SetExpiryImmediately(); // removed + client-synced on the next effects tick

            if (toRemove.Count > 0) imp.Particles?.Emit(ctx.Caster.World, target!);
        }

        /// <summary>Taunt: redirects nearby mob AI onto the caster (players are unaffected). It both rewrites the
        /// task's current target and fakes a 0-damage "hurt by the caster" ping, so vanilla's revenge priority
        /// holds the mob through its next re-evaluation instead of a proximity scan stealing it back.</summary>
        private static void ApplyAggro(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var caster = ctx.Caster;
            float range = imp.AggroRange > 0 ? imp.AggroRange : 8f;
            var fakeHurt = new CanrpgDamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = caster,
                CauseEntity = caster,
                School = ctx.Spell.School,
                Type = EnumDamageType.Injury
            };

            foreach (var e in caster.World.GetEntitiesAround(caster.Pos.XYZ, range, range,
                         e => e != caster && e.Alive && e is EntityAgent && (e as EntityPlayer)?.Player == null))
            {
                var taskAI = e.GetBehavior<EntityBehaviorTaskAI>();
                var mgr = taskAI?.TaskManager;
                if (taskAI == null || mgr == null) continue;

                var seek = mgr.GetTask<AiTaskSeekEntity>();
                if (seek != null) seek.targetEntity = caster;       // public field; the read-only property has no setter
                var melee = mgr.GetTask<AiTaskMeleeAttack>();
                if (melee != null) melee.targetEntity = caster;

                // EntityBehaviorTaskAI's public hook is OnEntityReceiveDamage (it forwards to TaskManager.OnEntityHurt
                // internally) - 0 damage, so nothing actually happens to the mob's HP. This records the caster as
                // attackedByEntity, which is enough for HOSTILE mobs.
                float zeroDamage = 0f;
                taskAI.OnEntityReceiveDamage(fakeHurt, ref zeroDamage);

                // PASSIVE mobs (villagers/traders that only retaliate when hurt) won't target off attackedByEntity
                // alone - their targeting is gated on the "aggressiveondamage" emotion state, which real damage sets.
                // Trigger it directly so a taunt provokes them the same way a hit would.
                e.GetBehavior<EntityBehaviorEmotionStates>()?.TryTriggerState("aggressiveondamage", caster.EntityId);
            }

            imp.Particles?.Emit(ctx.Caster.World, caster);
        }
    }
}
