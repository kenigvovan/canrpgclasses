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
    /// <summary>Checked before a cast is committed. Returns a localized refusal, or null if the impact can land.</summary>
    public delegate string? ImpactPrecheck(EntityAgent caster, Spell spell, SpellImpact impact, AimContext aim);

    /// <summary>Server pipeline: targeting, delivery, impacts. Each step dispatches to a registered handler so content
    /// mods can add new kinds.</summary>
    public static class SpellExecutor
    {
        private static readonly Dictionary<TargetType, TargetResolver> targeting = new Dictionary<TargetType, TargetResolver>();
        private static readonly Dictionary<DeliveryType, DeliveryHandler> deliveries = new Dictionary<DeliveryType, DeliveryHandler>();
        private static readonly Dictionary<ImpactAction, ImpactHandler> impacts = new Dictionary<ImpactAction, ImpactHandler>();
        private static bool defaultsRegistered;

        public static void RegisterTargeting(TargetType type, TargetResolver resolver) => targeting[type] = resolver;
        public static void RegisterDelivery(DeliveryType type, DeliveryHandler handler) => deliveries[type] = handler;
        public static void RegisterImpact(ImpactAction action, ImpactHandler handler) => impacts[action] = handler;

        private static readonly Dictionary<ImpactAction, ImpactPrecheck> prechecks = new Dictionary<ImpactAction, ImpactPrecheck>();
        public static void RegisterPrecheck(ImpactAction action, ImpactPrecheck check) => prechecks[action] = check;

        /// <summary>First refusal from the spell's impact prechecks, or null.</summary>
        public static string? Precheck(EntityAgent caster, Spell spell, AimContext aim)
        {
            if (prechecks.Count == 0) return null;
            foreach (var imp in spell.Impacts)
                if (prechecks.TryGetValue(imp.Action, out var check) && check(caster, spell, imp, aim) is string msg)
                    return msg;
            return null;
        }

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

        // Absorb pool soaked in StunPatches.
        public const string AbsorbKey = CombatFlags.Absorb;
        public const string AbsorbUntilKey = CombatFlags.AbsorbUntil;

        // Empowered next strike, stored on the caster and consumed by EBSpellCaster on the next melee hit.
        public const string EmpowerDamageKey = AttrKeys.EmpowerDamage;
        public const string EmpowerMissingDamageKey = AttrKeys.EmpowerMissingDamage;
        public const string EmpowerKnockbackKey = AttrKeys.EmpowerKnockback;
        public const string EmpowerComboKey = AttrKeys.EmpowerCombo;
        public const string EmpowerUntilKey = AttrKeys.EmpowerUntilMs;
        public const string EmpowerSpellKey = AttrKeys.EmpowerSpell;

        // Own tag, not simplealchemy's "simplepoisoned", so that mod never double-drains our charges.
        public const string WeaponCoatKey = AttrKeys.WeaponCoat;

        public const string EmpowerSchoolKey = AttrKeys.EmpowerSchool;

        /// <summary>No-op on the client.</summary>
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

            if (spell.ComboFinisher) ctx.ComboPoints = ResourceState.Combo(caster);

            SpellSounds.Play(caster.World, spell, SpellSounds.Slot.Release, caster.Pos.XYZ);
            Deliver(ctx);

            // Builders add a point only if they hit an enemy.
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

        /// <summary>Multiple projectiles fire flat: a frontal fan over ProjectileSpreadDegrees, or a full ring at 360.</summary>
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

            // AI casters fire at their target; players along the look direction.
            Vec3d? aimDir = AimDirection(ctx.Aim, position);
            double baseYaw = aimDir != null ? Math.Atan2(aimDir.X, aimDir.Z) : from.Yaw;

            for (int i = 0; i < count; i++)
            {
                double yawOffsetRad =
                    count == 1 ? 0.0 :
                    ring       ? i * (Math.PI * 2.0 / count) :
                                 (-spreadDeg / 2.0 + spreadDeg * i / (count - 1)) * Math.PI / 180.0;
                double pitch = count == 1 ? from.Pitch : 0.0;
                Vec3d dir = count == 1 && aimDir != null
                    ? aimDir
                    : (position.AheadCopy(1.0, pitch, baseYaw + yawOffsetRad) - position).Normalize();
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
            var tc = Spells.ParticleSpec.ForSchool(ctx.Spell.School);
            proj.WatchedAttributes.SetInt("trailColor", ColorUtil.ToRgba(tc.ColorA, tc.ColorR, tc.ColorG, tc.ColorB));
            proj.ComboPoints = ctx.ComboPoints;
            proj.MaxRange = ctx.Spell.Range > 0 ? ctx.Spell.Range : 32f;
            proj.Damage = 0f; // damage comes from the spell's impacts

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
            bool struck = false;
            foreach (var imp in ctx.Spell.Impacts)
            {
                if (imp.Chance < 1f && rand.NextDouble() > imp.Chance) continue;
                if (!string.IsNullOrEmpty(imp.RequiresTalentId)
                    && canrpgclasses.Core.Talents.TalentState.Rank(ctx.Caster, imp.RequiresTalentId!) <= 0) continue;
                if (!impacts.TryGetValue(imp.Action, out var handler)) continue;

                Entity? tgt;
                if (imp.AffectCaster) tgt = ctx.Caster;
                else if (imp.AffectAimedEnemy)
                {
                    tgt = GetAimedEntity(ctx.Caster, ctx.Aim, ctx.Spell.Range > 0 ? ctx.Spell.Range : 16f);
                    if (tgt != null && IsAlly(ctx.Caster, tgt)) tgt = null;
                }
                else tgt = target;
                if (tgt == null) continue;
                handler(ctx, tgt, imp);
                EmitImpactFx(ctx, tgt, imp);
                if (tgt != ctx.Caster) struck = true;
            }

            // Capped so a big AoE doesn't stack into noise.
            if (struck && ctx.ImpactSounds++ < MaxImpactSounds)
                SpellSounds.Play(ctx.Caster.World, ctx.Spell, SpellSounds.Slot.Impact, target.Pos.XYZ);
        }

        private const int MaxImpactSounds = 3;

        /// <summary>Opt-in SkillFx accents, kept out of the per-action handlers so they can ride any action.</summary>
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
                    // Smart-cast: aimed ally, else the caster.
                    result.Add(aimed != null && IsAlly(ctx.Caster, aimed) ? aimed : ctx.Caster);
                    break;
                case TargetAffinity.Enemy:
                    if (aimed != null && !IsAlly(ctx.Caster, aimed)) result.Add(aimed);
                    break;
                default:
                    if (aimed != null) result.Add(aimed);
                    break;
            }
            return result;
        }

        /// <summary>Same canparty group; a player-owned pet counts as its owner. Add-on resolvers
        /// (<see cref="RpgCombatApi.RegisterAllyResolver"/>) are asked first. Side-aware, safe in singleplayer.</summary>
        public static bool IsAlly(Entity caster, Entity target)
        {
            if (target == caster) return true;
            if (canrpgclassesModSystem.For(caster.Api)?.ResolveAllyOverride(caster, target) is bool decided) return decided;

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

        /// <summary>The player itself, or a pet's owner via <see cref="AttrKeys.GuardedByPlayerUid"/>, else null.</summary>
        private static EntityPlayer? EffectivePlayer(Entity? e)
        {
            if (e is EntityPlayer p) return p;
            string? uid = e?.WatchedAttributes?.GetString(AttrKeys.GuardedByPlayerUid, null);
            if (string.IsNullOrEmpty(uid)) return null;
            return e!.World?.PlayerByUid(uid)?.Entity as EntityPlayer;
        }

        /// <summary>Client-picked target if valid, else a server look-ray pick.</summary>
        public static Entity? GetAimedEntity(EntityAgent caster, AimContext aim, float range)
        {
            var picked = aim?.TargetEntity;
            // Client-supplied id: verify range with 2 blocks of latency slack.
            if (picked != null && picked.Alive && caster.Pos.DistanceTo(picked.Pos.XYZ) <= range + 2f)
                return picked;
            // An AI caster aims at an explicit target; its body facing is not a crosshair.
            if (aim != null && aim.AimAtTarget) return null;
            return LookedAtAgent(caster, range);
        }

        /// <summary>Direction to the AI aim target, or null to keep the look direction.</summary>
        public static Vec3d? AimDirection(AimContext? aim, Vec3d from)
        {
            if (aim == null || !aim.AimAtTarget) return null;
            var te = aim.TargetEntity;
            Vec3d? to = te != null ? te.Pos.XYZ.Add(0, te.SelectionBox.Y2 * 0.5, 0) : aim.AimPosition;
            if (to == null) return null;
            Vec3d d = to - from;
            return d.Length() < 1e-6 ? null : d.Normalize();
        }

        private static List<Entity> ResolveArea(SpellContext ctx)
        {
            var caster = ctx.Caster;
            var t = ctx.Spell.Target;
            float range = ctx.Spell.Range > 0 ? ctx.Spell.Range : 6f;
            float hor = range * (t.AreaHorizontalRangeMultiplier > 0 ? t.AreaHorizontalRangeMultiplier : 1f);
            float ver = range * (t.AreaVerticalRangeMultiplier > 0 ? t.AreaVerticalRangeMultiplier : 1f);
            Vec3d pos = caster.Pos.XYZ;

            // Carpet is emitted once per cast, even with no targets present.
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
        /// <summary>1.0-based <see cref="Spell.DamageMultiplierStat"/> (finisherDamage for finishers), shared by
        /// <see cref="FinisherAmount"/> and the empower path.</summary>
        private static float DamageMultiplier(SpellContext ctx, out string? mulName)
        {
            mulName = ctx.Spell.DamageMultiplierStat ?? (ctx.Spell.ComboFinisher ? StatKeys.FinisherDamage : null);
            if (mulName == null) return 1f;
            float statMul = ctx.Caster.Stats.GetBlended(mulName);
            return statMul > 0.01f ? statMul : 1f;
        }

        /// <summary>Missing health fraction 0..1; 0 without a health tree.</summary>
        public static float MissingHealthFraction(Entity target)
        {
            var htree = target?.WatchedAttributes.GetTreeAttribute("health");
            float max = htree?.GetFloat("maxhealth") ?? 0f;
            return max > 0f ? System.Math.Clamp(1f - htree!.GetFloat("currenthealth") / max, 0f, 1f) : 0f;
        }

        /// <summary>SpellPower x (coeff + per-combo bonus + missing-health part) x damage multiplier.</summary>
        private static float FinisherAmount(SpellContext ctx, SpellImpact imp, Entity? target = null)
        {
            float coeff = imp.DamageSpellPowerCoefficient + imp.DamagePerComboPoint * ctx.ComboPoints;
            // Missing health goes into the coefficient so the multiplier scales it too.
            if (target != null && imp.DamageMissingHealthCoefficient > 0f)
                coeff += imp.DamageMissingHealthCoefficient * MissingHealthFraction(target);
            float mul = DamageMultiplier(ctx, out string? mulName);
            float amount = ctx.SpellPower * coeff * mul;

            // "debugDamage" global > 0 logs every term of the hit; gated so it costs nothing in normal play.
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
            // Bonus vs the control state this impact nominates.
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

            if (imp.DamageSplashHealStat != null) ApplyDamageSplashHeal(ctx, amount, imp);

            // A controlled target can only be shattered once.
            if (shattered && imp.ConsumeControlOnHit && imp.DamageVsControlEffectId != null)
                RemoveEffect(target, imp.DamageVsControlEffectId);

            if (imp.BoltFx) Visuals.SkillFx.Arc(ctx.Caster.World, ctx.Caster, target, ctx.Spell.School);

            // Chain from the unshattered base, so a chain can't multiply a one-target bonus.
            if (imp.ChainJumps > 0) ChainDamage(ctx, imp, target, baseAmount);

            BreakCasterInvisibility(ctx.Caster); // spell damage doesn't go through DidAttack
        }

        /// <summary>Hops to the nearest unhit enemy within <see cref="SpellImpact.ChainRadius"/>, amount x falloff per
        /// jump. Riders are deliberately not re-run.</summary>
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
                    e => !IsAlly(caster, e));
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

        /// <summary>Hops to the most wounded unhealed ally; amounts already include healingPower.</summary>
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

        /// <summary>Players only, so a chain heal doesn't land on a passing animal.</summary>
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

        /// <summary>Heals allies in radius (optionally shielded only, caster included) for a fraction of the damage.
        /// The stat is 0 until a talent sets it, so the rider is harmless by default.</summary>
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

        private static bool HasActiveShield(Entity e)
        {
            var wa = e.WatchedAttributes;
            return e.World.ElapsedMilliseconds <= wa.GetLong(AbsorbUntilKey, 0) && wa.GetFloat(AbsorbKey, 0f) > 0f;
        }

        /// <summary>Sends the heal number manually: heals carry no CauseEntity, so the combat-text hook can't.</summary>
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

        /// <summary>Use with Target=Caster; the zone scans its own area each tick via <see cref="ZoneManager"/>.</summary>
        private static void ApplyDamageZone(SpellContext ctx, Entity target, SpellImpact imp)
            => ZoneManager.Add(ctx, imp);

        /// <summary>Matches the impact's DamageVsControlFlag (WA bool) or DamageVsControlEffectId (effectshud effect).</summary>
        private static bool IsControlled(Entity target, SpellImpact imp)
        {
            if (imp.DamageVsControlFlag != null && target?.WatchedAttributes?.GetBool(imp.DamageVsControlFlag) == true) return true;
            if (imp.DamageVsControlEffectId != null &&
                (target?.GetBehavior<EBEffects>()?.GetEffectTier(imp.DamageVsControlEffectId) ?? -1) >= 0) return true;
            return false;
        }

        /// <summary>Ends an effect now, mirroring effectshud's expiry path (OnExpire, remove, sync).</summary>
        public static void RemoveEffect(Entity target, string effectId)
        {
            var ebea = target?.GetBehavior<EBEffects>();
            if (ebea == null || !ebea.TryGetEffect(effectId, out var eff)) return;
            eff.OnExpire();
            ebea.activeEffects.Remove(effectId);
            ebea.SendEffectToClient(null, new System.Collections.Generic.HashSet<string> { effectId });
        }

        /// <summary>Gate for <see cref="Spell.RequiresTargetEffectIds"/>; null/empty = true.</summary>
        public static bool HasAnyEffect(Entity? target, string[]? effectIds)
        {
            if (effectIds == null || effectIds.Length == 0) return true;
            var ebea = target?.GetBehavior<EBEffects>();
            if (ebea == null) return false;
            foreach (var id in effectIds) if (ebea.GetEffectTier(id) >= 0) return true;
            return false;
        }

        /// <summary>Removes only the first matching effect; array order is the priority.</summary>
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

        /// <summary>Turns GetEntitiesAround's square box into a round AoE.</summary>
        internal static bool WithinHorizontalRadius(Vec3d center, Entity e, float hor)
        {
            double dx = e.Pos.X - center.X, dz = e.Pos.Z - center.Z;
            return dx * dx + dz * dz <= hor * hor;
        }

        /// <summary>Keys may be local or full spell ids.</summary>
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

        /// <summary>Ends break-on-attack invisibility on revealing actions; melee is handled by InvisibilityEffect itself.</summary>
        public static void BreakCasterInvisibility(Entity caster)
        {
            var ce = caster.GetBehavior<EBEffects>();
            if (ce != null && ce.TryGetEffect("invisibility", out var eff)
                && eff is InvisEffect inv && inv.BreakOnAttack)
                eff.SetExpiryImmediately();

            caster.GetBehavior<canrpgclasses.Core.EB.EBSpellCaster>()?.NotifyStealthBrokenByAttack();
        }

        /// <summary>Arms a bonus for the next melee hit instead of dealing damage now; consumed in EBSpellCaster.</summary>
        private static void ApplyEmpowerNextMelee(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var wa = ctx.Caster.WatchedAttributes;
            // Same formula as ApplyDamage, so a finisher's burst can ride a real swing instead of an i-frame-dropped hit.
            float bonus = FinisherAmount(ctx, imp);
            wa.SetFloat(EmpowerDamageKey, bonus);
            // No target yet, so store the max missing-health part for the landing swing to scale. Written
            // unconditionally so a fresh arm never leaks a stale part.
            float missingMax = imp.DamageMissingHealthCoefficient > 0f
                ? ctx.SpellPower * imp.DamageMissingHealthCoefficient * DamageMultiplier(ctx, out _)
                : 0f;
            wa.SetFloat(EmpowerMissingDamageKey, missingMax);
            wa.SetFloat(EmpowerKnockbackKey, imp.Knockback);
            wa.SetInt(EmpowerSchoolKey, (int)ctx.Spell.School);
            wa.SetInt(EmpowerComboKey, ctx.Spell.ComboBuilder ? ctx.Spell.ComboPointsGenerated : 0);
            wa.SetString(EmpowerSpellKey, ctx.Spell.Id); // lets the consuming strike find this spell's particles

            var caster = ctx.Caster.GetBehavior<canrpgclasses.Core.EB.EBSpellCaster>();
            caster?.ClearEmpowerHudBuffs();
            caster?.ArmEmpowerWindow(imp.EmpowerWindowSeconds);

            // HUD icon per arming spell; unregistered ids no-op.
            ApplyEffect(ctx.Caster, canrpgclasses.Core.Effects.EmpowerEffectIds.For(ctx.Spell.LocalId), 1, imp.EmpowerWindowSeconds);
        }

        /// <summary>Players: weapon moves to another hotbar slot (no ground loot). Mobs: weakmelee debuff.</summary>
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
            float coeff = imp.HealSpellPowerCoefficient + imp.HealPerComboPoint * ctx.ComboPoints;
            // Healing-done multiplier, 1.0-based.
            float healMul = ctx.Caster.Stats.GetBlended(StatKeys.HealingPower);
            float heal = ctx.SpellPower * coeff * healMul;
            // Plus a fraction of the target's missing health.
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

            // Heals carry no CauseEntity, so CombatTextPatches can't report them.
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

            // After the heal, and only when it landed, so a fizzled cast never eats a HoT.
            ConsumeTargetEffects(target, imp.ConsumeTargetEffectIds);

            // Chains off the spell-power heal only; the missing-health part stays single-target.
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

            // An effect can't reach the caster itself, so hand it the snapshot now.
            if (eff is canrpgclasses.Core.Effects.ISnapshotEffect snap)
                snap.CaptureCaster(ctx.Caster.EntityId, ctx.SpellPower);

            EffectsHud.ApplyEffectOnEntity(target, eff);

            imp.Particles?.Emit(ctx.Caster.World, target);
        }

        private static void ApplyTeleport(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var caster = ctx.Caster;
            Vec3d dest;

            // Puff at the departure spot, before moving.
            imp.Particles?.Emit(caster.World, caster);

            if (imp.TeleportMode == TeleportMode.BehindTarget && target != null && target != caster)
            {
                Vec3d tp = target.Pos.XYZ;
                // Behind the target's own facing (as in DamageModifiers.IsBehind), not the caster's approach angle.
                float yaw = target.Pos.Yaw;
                Vec3d forward = new Vec3d(Math.Sin(yaw), 0, Math.Cos(yaw));
                dest = tp - forward * imp.TeleportDistance;
            }
            else
            {
                dest = ResolveForwardTeleport(caster, imp.TeleportDistance, ctx.Aim);
            }

            if (imp.TeleportFaceTarget && target != null && target != caster)
            {
                Vec3d face = target.Pos.XYZ - dest;
                float yaw = (float)Math.Atan2(face.X, face.Z);
                var epos = caster.Pos.Copy();
                epos.SetPos(dest.X, dest.Y, dest.Z);
                epos.Yaw = yaw;
                caster.TeleportTo(epos);
                // TeleportTo's yaw is unreliable for the owner's camera (async chunk load), so snap MouseYaw too.
                SendViewYaw(caster, yaw);
            }
            else
            {
                caster.TeleportToDouble(dest.X, dest.Y, dest.Z);
            }
        }

        /// <summary>Steps forward and stops at the last free body position, so a blink can't pass through walls.
        /// Climbs up to 2 blocks; descents settle by gravity.</summary>
        private static Vec3d ResolveForwardTeleport(EntityAgent caster, float distance, AimContext? aim = null)
        {
            var world = caster.World;
            var ba = world.BlockAccessor;
            var ct = world.CollisionTester;
            var box = caster.CollisionBox;

            Vec3f view = caster.Pos.GetViewVector();
            Vec3d fwd = new Vec3d(view.X, 0, view.Z);
            Vec3d? aimDir = AimDirection(aim, caster.Pos.XYZ);
            if (aimDir != null) fwd.Set(aimDir.X, 0, aimDir.Z);
            if (fwd.Length() < 1e-3) fwd.Set(0, 0, 1);
            fwd.Normalize();

            Vec3d start = caster.Pos.XYZ;
            if (box == null) return start + fwd * distance;

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
                if (free == null) break; // wall
                best = free;
            }
            return best;
        }

        /// <summary>The owner camera renders from MouseYaw, which the server can't set directly.</summary>
        internal static void SendViewYaw(EntityAgent caster, float yaw)
        {
            if ((caster as EntityPlayer)?.Player is not IServerPlayer sp) return;
            var mod = canrpgclassesModSystem.For(caster.Api);
            mod?.ServerChannel?.SendPacket(new SetViewYawPacket { Yaw = yaw }, sp);
        }

        /// <summary>Tags the held weapon; the effect is applied per hit in EBSpellCaster.ApplyOwnWeaponCoating.</summary>
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

        /// <summary>Gap-proportional impulse via the knockback channel, since server-side Pos.Motion doesn't move a player.</summary>
        private static void ApplyDash(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var caster = ctx.Caster;
            Vec3d cp = caster.Pos.XYZ;

            double dx, dy, dz;
            float range = ctx.Spell.Range > 0 ? ctx.Spell.Range : 16f;
            // Only a plain dash homes onto the aimed enemy, never an ally.
            var aimed = (imp.DashReverse || imp.DashForwardStrength > 0f) ? null : GetAimedEntity(caster, ctx.Aim, range);
            if (aimed != null && aimed != caster && IsAlly(caster, aimed)) aimed = null;

            if (aimed != null && aimed != caster)
            {
                Vec3d tp = aimed.Pos.XYZ;
                dx = (tp.X - cp.X) / 30.0;
                dz = (tp.Z - cp.Z) / 30.0;

                double h = Math.Sqrt(dx * dx + dz * dz);
                const double maxH = 0.8;
                if (h > maxH) { dx *= maxH / h; dz *= maxH / h; }

                // Keep the hop low: a big upward kick removes ground friction and overshoots.
                dy = GameMath.Clamp((tp.Y - cp.Y) / 30.0 + 0.1, 0.1, 0.35);
            }
            else
            {
                Vec3f view = caster.Pos.GetViewVector();
                Vec3d? aimDir = AimDirection(ctx.Aim, cp);
                if (aimDir != null) view = new Vec3f((float)aimDir.X, (float)aimDir.Y, (float)aimDir.Z);
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

        private static void ApplyStun(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (target == null || target == ctx.Caster) return;
            float dur = (imp.StatusEffectDuration > 0 ? imp.StatusEffectDuration : 2f) + imp.DurationPerComboPoint * ctx.ComboPoints;
            if (canrpgclasses.Core.Control.ControlState.Apply(target, canrpgclasses.Core.Control.ControlType.Stun, dur,
                    ctx.Caster, imp.StunBreaksOnDamage, imp.StunBlindsVision, imp.StunVisionIntensity))
                imp.Particles?.Emit(ctx.Caster.World, target);
        }

        private static void ApplyFear(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (target == null || target == ctx.Caster) return;
            float dur = imp.StatusEffectDuration > 0 ? imp.StatusEffectDuration : 5f;
            if (canrpgclasses.Core.Control.ControlState.Apply(target, canrpgclasses.Core.Control.ControlType.Fear, dur, ctx.Caster))
                imp.Particles?.Emit(ctx.Caster.World, target);
        }

        private static void ApplySilence(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (target == null || target == ctx.Caster) return;
            float dur = imp.StatusEffectDuration > 0 ? imp.StatusEffectDuration : 4f;
            if (canrpgclasses.Core.Control.ControlState.Apply(target, canrpgclasses.Core.Control.ControlType.Silence, dur, ctx.Caster))
                imp.Particles?.Emit(ctx.Caster.World, target);
        }

        private static void ApplyRoot(SpellContext ctx, Entity target, SpellImpact imp)
        {
            // A self-root opts in via AffectCaster.
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

        /// <summary>StatusEffectId, if set, is a HUD marker cleared when the free cast is used.</summary>
        private static void ApplyGrantInstantCast(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var caster = ctx.Caster;
            float window = imp.StatusEffectDuration > 0 ? imp.StatusEffectDuration : 15f;
            string? marker = string.IsNullOrEmpty(imp.StatusEffectId) ? null : imp.StatusEffectId;
            caster.GetBehavior<canrpgclasses.Core.EB.EBSpellCaster>()?.GrantInstantCast("*", window, marker);
            if (marker != null) ApplyEffect(caster, marker, 1, window);
        }

        /// <summary>Dodge chance in WatchedAttributes, rolled in StunPatches and expired by
        /// <see cref="canrpgclasses.Core.EB.EBCombatState"/> rather than a timer.</summary>
        private static void ApplyEvasion(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (target == null) return;
            float dur = imp.StatusEffectDuration > 0 ? imp.StatusEffectDuration : 5f;
            var wa = target.WatchedAttributes;
            long until = target.World.ElapsedMilliseconds + (long)(dur * 1000f);

            // Re-casting can't weaken an active Evasion.
            bool active = target.World.ElapsedMilliseconds <= wa.GetLong(CombatFlags.EvasionUntil, 0);
            float chance = Math.Max(imp.EvasionChance, active ? wa.GetFloat(CombatFlags.Evasion, 0f) : 0f);
            wa.SetFloat(CombatFlags.Evasion, chance);
            wa.SetLong(CombatFlags.EvasionUntil, Math.Max(until, active ? wa.GetLong(CombatFlags.EvasionUntil, 0) : 0));

            ApplyEffect(target, canrpgclasses.Core.Effects.EvasionEffectId.Id, 1, dur);

            imp.Particles?.Emit(ctx.Caster.World, target);
        }

        /// <summary>Ends all invisibility (not just break-on-attack) on enemies within Range.</summary>
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
                    imp.Particles?.Emit(caster.World, e);
                }
            }
            imp.Particles?.Emit(caster.World, caster);
        }

        /// <summary>One aura at a time; <see cref="canrpgclasses.Core.EB.EBAuras"/> does the actual buffing.</summary>
        private static void ApplyToggleAura(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var wa = ctx.Caster.WatchedAttributes;
            string id = ctx.Spell.Id;
            wa.SetString(AttrKeys.ActiveAura, wa.GetString(AttrKeys.ActiveAura, "") == id ? "" : id);
        }

        /// <summary>Plain timed effect without a SpellContext; no-op if the id isn't registered.</summary>
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

        /// <summary>Absorb pool soaked by the damage patch until depleted or expired.</summary>
        private static void ApplyShield(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (target == null) return;
            float amount = ctx.SpellPower * imp.ShieldSpellPowerCoefficient;
            if (amount <= 0) return;

            var wa = target.WatchedAttributes;
            // Larger pool and later deadline win, so a weaker re-cast can't shrink an existing shield.
            long existingUntil = wa.GetLong(AbsorbUntilKey, 0);
            long now = target.World.ElapsedMilliseconds;
            float existing = now <= existingUntil ? wa.GetFloat(AbsorbKey, 0f) : 0f;
            long newUntil = now + (long)(imp.ShieldDurationSeconds * 1000f);
            wa.SetFloat(AbsorbKey, Math.Max(existing, amount));
            wa.SetLong(AbsorbUntilKey, Math.Max(existingUntil, newUntil));
            // Client clock differs from server ElapsedMilliseconds, so send remaining seconds for the dome renderer.
            wa.SetFloat(CombatFlags.AbsorbDuration, (Math.Max(existingUntil, newUntil) - now) / 1000f);

            // Dome tint and style for ShieldDomeRenderer; style defaults to ice crystal for frost.
            var sc = Spells.ParticleSpec.ForSchool(ctx.Spell.School);
            wa.SetInt(CombatFlags.AbsorbColor, ColorUtil.ToRgba(sc.ColorA, sc.ColorR, sc.ColorG, sc.ColorB));
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
            foreach (var e in toRemove) e.SetExpiryImmediately();

            if (toRemove.Count > 0) imp.Particles?.Emit(ctx.Caster.World, target!);
        }

        /// <summary>Taunt: rewrites the mob's task target and fakes a 0-damage hurt from the caster, so vanilla's
        /// revenge priority holds through the next re-evaluation.</summary>
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

                // Sets attackedByEntity via the public hook, which is enough for hostile mobs.
                float zeroDamage = 0f;
                taskAI.OnEntityReceiveDamage(fakeHurt, ref zeroDamage);

                // Passive mobs only retaliate in the "aggressiveondamage" emotion state, which real damage sets.
                e.GetBehavior<EntityBehaviorEmotionStates>()?.TryTriggerState("aggressiveondamage", caster.EntityId);
            }

            imp.Particles?.Emit(ctx.Caster.World, caster);
        }
    }
}
