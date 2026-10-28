using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Core.Control
{
    public enum ControlType { Stun, Fear, Silence, Root, Transmute }

    /// <summary>Single CC layer. <see cref="Apply"/> does bracket DR, the timed flag and side effects; gates query
    /// <see cref="PreventsMovement"/> and friends, not individual flags.</summary>
    public static class ControlState
    {
        private readonly struct Props
        {
            public readonly bool PreventsMovement, PreventsActions, PreventsCasting, RootsMotion, InterruptsHandUse;
            public readonly string Bracket, Flag, Until;
            public readonly string? HudEffect; // HUD/target-frame marker effect, null = none
            public Props(bool move, bool act, bool cast, bool root, bool hand, string bracket, string flag, string until, string? hudEffect = null)
            { PreventsMovement = move; PreventsActions = act; PreventsCasting = cast; RootsMotion = root; InterruptsHandUse = hand; Bracket = bracket; Flag = flag; Until = until; HudEffect = hudEffect; }
        }

        // Stun and Fear share the "incap" DR bracket. Fear must not root - its wander needs knockback to move the player.
        private static readonly Dictionary<ControlType, Props> Table = new()
        {
            [ControlType.Stun]      = new Props(true,  true,  true,  root: true,  hand: true,  "incap",   CombatFlags.Stunned,     CombatFlags.StunUntil,     Effects.ControlEffectIds.Stunned),
            [ControlType.Fear]      = new Props(true,  true,  true,  root: false, hand: true,  "incap",   CombatFlags.Feared,      CombatFlags.FearUntil,     Effects.ControlEffectIds.Feared),
            [ControlType.Silence]   = new Props(false, false, true,  root: false, hand: false, "silence", CombatFlags.Silenced,    CombatFlags.SilenceUntil,  Effects.ControlEffectIds.Silenced),
            // Root: can't move but can still cast and attack.
            [ControlType.Root]      = new Props(true,  false, false, root: true,  hand: false, "root",    CombatFlags.Rooted,      CombatFlags.RootUntil,     Effects.ControlEffectIds.Rooted),
            // Transmute: full incap plus a sheep model on players; break-on-damage is set by the impact.
            [ControlType.Transmute] = new Props(true,  true,  true,  root: true,  hand: true,  "poly",    CombatFlags.Polymorphed, CombatFlags.PolymorphUntil, Effects.ControlEffectIds.Polymorphed),
        };

        // Hot path (physics prefixes, several times per entity per tick): read one aggregate CcMask attribute
        // against masks precomputed from the Table.
        public static bool IsActive(Entity? e, ControlType type)
            => e?.WatchedAttributes?.GetBool(Table[type].Flag) ?? false;

        private static int BuildQueryMask(System.Func<Props, bool> pred)
        {
            int m = 0;
            foreach (var kv in Table) if (pred(kv.Value)) m |= 1 << (int)kv.Key;
            return m;
        }

        private static readonly int MoveMask = BuildQueryMask(p => p.PreventsMovement);
        private static readonly int ActMask  = BuildQueryMask(p => p.PreventsActions);
        private static readonly int CastMask = BuildQueryMask(p => p.PreventsCasting);
        private static readonly int RootMask = BuildQueryMask(p => p.RootsMotion);
        private static readonly int HandMask = BuildQueryMask(p => p.InterruptsHandUse);

        private static int CcMask(Entity? e) => e?.WatchedAttributes?.GetInt(CombatFlags.CcMask, 0) ?? 0;

        public static bool PreventsMovement(Entity? e)  => (CcMask(e) & MoveMask) != 0;
        public static bool PreventsActions(Entity? e)   => (CcMask(e) & ActMask) != 0;
        public static bool PreventsCasting(Entity? e)   => (CcMask(e) & CastMask) != 0;
        public static bool RootsMotion(Entity? e)       => (CcMask(e) & RootMask) != 0;
        public static bool InterruptsHandUse(Entity? e) => (CcMask(e) & HandMask) != 0;

        /// <summary>Recomputes <see cref="CombatFlags.CcMask"/> from the per-type flags (removed when empty). Every
        /// path that sets or clears a CC flag must call this.</summary>
        public static void RefreshCcGate(ITreeAttribute? wa)
        {
            if (wa == null) return;
            int mask = 0;
            foreach (var kv in Table)
                if (wa.GetBool(kv.Value.Flag)) mask |= 1 << (int)kv.Key;
            if (mask == wa.GetInt(CombatFlags.CcMask, 0)) return;
            if (mask != 0) wa.SetInt(CombatFlags.CcMask, mask);
            else wa.RemoveAttribute(CombatFlags.CcMask);
        }

        /// <summary>Returns false if the target was immune, so the caller can skip particles.</summary>
        public static bool Apply(Entity target, ControlType type, float seconds, Entity? source,
                                 bool breakOnDamage = false, bool blindsVision = false, float visionIntensity = 1f)
        {
            if (target == null || seconds <= 0f) return false;
            var p = Table[type];

            // Add-on modifiers (boss immunity etc.) first, so an immune target doesn't build up DR.
            seconds = canrpgclassesModSystem.For(target.Api)?.ModifyControlDuration(target, type, seconds, source) ?? seconds;
            if (!(seconds > 0f)) return false;

            seconds = DiminishingReturns(target, p.Bracket, seconds);
            if (seconds <= 0f) return false;

            long now = target.World.ElapsedMilliseconds;
            long until = now + (long)(seconds * 1000f);
            var wa = target.WatchedAttributes;
            if (until > wa.GetLong(p.Until, 0)) wa.SetLong(p.Until, until);
            wa.SetBool(p.Flag, true);

            // Break-on-damage: one bit per type, read by OnDamage.
            int mask = wa.GetInt(CombatFlags.BreakOnDamageMask, 0);
            if (breakOnDamage) mask |= 1 << (int)type; else mask &= ~(1 << (int)type);
            if (mask != 0) wa.SetInt(CombatFlags.BreakOnDamageMask, mask); else wa.RemoveAttribute(CombatFlags.BreakOnDamageMask);

            if (p.HudEffect != null) // mobs too: the marker is what target frames show
                Execution.SpellExecutor.ApplyEffect(target, p.HudEffect, 1, seconds);

            switch (type)
            {
                case ControlType.Stun: ApplyStunEffects(target, seconds, blindsVision, visionIntensity); break;
                case ControlType.Fear: ApplyFearEffects(target, source, seconds); break;
                case ControlType.Root: ApplyRootEffects(target, seconds); break;
                case ControlType.Transmute: ApplyTransmuteEffects(target, seconds); break;
                case ControlType.Silence: break;
            }

            // Mobs have no EBCombatState to expire the flag, so schedule a cleanup at the deadline.
            if (target is not EntityPlayer && wa.GetBool(p.Flag))
                target.World.RegisterCallback(_ =>
                {
                    if (target.World.ElapsedMilliseconds >= target.WatchedAttributes.GetLong(p.Until, 0))
                    {
                        target.WatchedAttributes.SetBool(p.Flag, false);
                        target.WatchedAttributes.RemoveAttribute(p.Until);
                        RefreshCcGate(target.WatchedAttributes);
                    }
                }, (int)(seconds * 1000f));

            // Last: the mob-fear path removes the Feared flag again.
            RefreshCcGate(wa);
            return true;
        }

        // ---- Stun side effects (root + blind + mob AI freeze) ----
        private static void ApplyStunEffects(Entity target, float seconds, bool blindsVision, float visionIntensity)
        {
            var wa = target.WatchedAttributes;
            if (blindsVision && target is EntityPlayer)
            {
                if (!wa.HasAttribute(CombatFlags.BlindVisionRestore))
                    wa.SetFloat(CombatFlags.BlindVisionRestore, wa.GetFloat("psychedelic", 0f));
                wa.SetFloat("psychedelic", visionIntensity);
            }

            target.Pos.Motion.X = 0;
            target.Pos.Motion.Z = 0;

            // Mob AI block is tied to the live flag, so a break-on-damage stun frees it immediately.
            var mgr = target.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (mgr != null)
            {
                mgr.StopTasks();
                ActionBoolReturn<IAiTask> block = _ => !target.WatchedAttributes.GetBool(CombatFlags.Stunned, false);
                mgr.OnShouldExecuteTask += block;
                target.World.RegisterCallback(_ => mgr.OnShouldExecuteTask -= block, (int)(seconds * 1000f));
            }
        }

        /// <summary>Restores the "psychedelic" value a blind stun overwrote. No-op if no blind is active.</summary>
        public static void RestoreBlindVision(Entity? target)
        {
            var wa = target?.WatchedAttributes;
            if (wa == null || !wa.HasAttribute(CombatFlags.BlindVisionRestore)) return;
            wa.SetFloat("psychedelic", wa.GetFloat(CombatFlags.BlindVisionRestore, 0f));
            wa.RemoveAttribute(CombatFlags.BlindVisionRestore);
        }

        // ---- Fear side effects (mob flee / player wander) ----
        private static void ApplyFearEffects(Entity target, Entity? source, float seconds)
        {
            if (target is EntityPlayer)
            {
                // Players: gates take control away, WanderTick moves them around this anchor.
                var wa = target.WatchedAttributes;
                wa.SetDouble(CombatFlags.FearSrcX, target.Pos.X);
                wa.SetDouble(CombatFlags.FearSrcZ, target.Pos.Z);
                wa.SetFloat(CombatFlags.FearHeading, target.Pos.Yaw);
                return;
            }

            // Mobs: flee task if present, plus blocked aggressive tasks. The Feared flag is player-only and nothing
            // would expire it on a mob.
            target.WatchedAttributes.RemoveAttribute(CombatFlags.Feared);
            target.WatchedAttributes.RemoveAttribute(CombatFlags.FearUntil);
            var mgr = target.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (mgr == null) return;
            mgr.StopTasks();
            var flee = mgr.GetTask<AiTaskFleeEntity>();
            if (flee != null && source != null) flee.InstaFleeFrom(source);

            // Add-on tasks opt in via marker interfaces; an NPC caster's cast runs in its own slot, so fleeing alone won't stop it.
            ActionBoolReturn<IAiTask> block = t => t is not (AiTaskSeekEntity or AiTaskMeleeAttack or IOffensiveAiTask or IMovementAiTask);
            mgr.OnShouldExecuteTask += block;
            target.World.RegisterCallback(_ => mgr.OnShouldExecuteTask -= block, (int)(seconds * 1000f));
        }

        // ---- Root side effects ----
        private static void ApplyRootEffects(Entity target, float seconds)
        {
            target.Pos.Motion.X = 0;
            target.Pos.Motion.Z = 0;
            // Mobs: block only movement tasks, so they can still melee if already in range.
            var mgr = target.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (mgr != null)
            {
                mgr.StopTasks();
                ActionBoolReturn<IAiTask> block = t => !((t is AiTaskSeekEntity || t is IMovementAiTask) && target.WatchedAttributes.GetBool(CombatFlags.Rooted, false));
                mgr.OnShouldExecuteTask += block;
                target.World.RegisterCallback(_ => mgr.OnShouldExecuteTask -= block, (int)(seconds * 1000f));
            }
        }

        // ---- Transmute side effects ----
        private static void ApplyTransmuteEffects(Entity target, float seconds)
        {
            target.Pos.Motion.X = 0;
            target.Pos.Motion.Z = 0;

            if (target is EntityPlayer pl)
            {
                // ToModel no-ops while another swap is live (e.g. Spirit Wolf), so free the slot first.
                Effects.ModelSwapEffects.CancelActive(pl);
                Integration.PlayerModelSwap.ToModel(pl, Integration.PlayerModelSwap.TransmuteModelCode);
                return;
            }

            var mgr = target.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (mgr != null)
            {
                mgr.StopTasks();
                ActionBoolReturn<IAiTask> block = _ => !target.WatchedAttributes.GetBool(CombatFlags.Polymorphed, false);
                mgr.OnShouldExecuteTask += block;
                target.World.RegisterCallback(_ => mgr.OnShouldExecuteTask -= block, (int)(seconds * 1000f));
            }
        }

        // ---- Player-fear wander tick ----
        private static bool inited;
        private static long wanderTickId;
        private static float wanderRadius = 6f, wanderStrength = 0.13f;

        public static void Init()
        {
            if (inited) return;
            inited = true;
            wanderRadius = BalanceConfig.Global("fearWanderRadius", 6f);
            wanderStrength = BalanceConfig.Global("fearWanderStrength", 0.13f);
            wanderTickId = canrpgclassesModSystem.ServerApi?.Event.RegisterGameTickListener(WanderTick, 300) ?? 0;
        }

        /// <summary>Resets the static guard so a second world in the same process re-registers on the new ServerApi.</summary>
        public static void Stop()
        {
            var api = canrpgclassesModSystem.ServerApi;
            if (api != null && wanderTickId != 0) api.Event.UnregisterGameTickListener(wanderTickId);
            wanderTickId = 0;
            inited = false;
        }

        private static void WanderTick(float dt)
        {
            var api = canrpgclassesModSystem.ServerApi;
            var players = api?.World?.AllOnlinePlayers;
            if (players == null) return;
            long now = api!.World.ElapsedMilliseconds;

            foreach (var plr in players)
            {
                var e = plr.Entity;
                if (e == null || !e.Alive) continue;
                var wa = e.WatchedAttributes;
                if (!wa.GetBool(CombatFlags.Feared)) continue;
                if (now >= wa.GetLong(CombatFlags.FearUntil, 0)) { ClearFear(wa); continue; }

                double ax = wa.GetDouble(CombatFlags.FearSrcX), az = wa.GetDouble(CombatFlags.FearSrcZ);
                double dxA = e.Pos.X - ax, dzA = e.Pos.Z - az;
                double distA = Math.Sqrt(dxA * dxA + dzA * dzA);

                float heading = wa.GetFloat(CombatFlags.FearHeading, e.Pos.Yaw);
                if (distA > wanderRadius) heading = (float)Math.Atan2(ax - e.Pos.X, az - e.Pos.Z); // steer back to the anchor
                else heading += (float)((e.World.Rand.NextDouble() * 2.0 - 1.0) * 0.7);            // erratic stumble
                wa.SetFloat(CombatFlags.FearHeading, heading);

                PushImpulse(e, Math.Sin(heading) * wanderStrength, Math.Cos(heading) * wanderStrength);
            }
        }

        /// <summary>Knockback via the onHurt channel, the only server-side way to move a player (Pos.Motion is
        /// ignored). Stays under the damage-flash threshold.</summary>
        private static void PushImpulse(Entity e, double dx, double dz)
        {
            var wa = e.WatchedAttributes;
            wa.SetDouble("kbdirX", dx);
            wa.SetDouble("kbdirY", 0.06);
            wa.SetDouble("kbdirZ", dz);
            wa.SetFloat("onHurtDir", (float)Math.Atan2(dx, dz));
            wa.SetInt("onHurtCounter", wa.GetInt("onHurtCounter", 0) + 1);
            wa.SetFloat("onHurt", wa.GetFloat("onHurt", 0f) > 0.0015f ? 0.001f : 0.002f);
        }

        /// <summary>Ends a fear including wander state, which EBCombatState's generic expiry doesn't know about.</summary>
        public static void ClearFear(ITreeAttribute wa)
        {
            wa.RemoveAttribute(CombatFlags.Feared);
            wa.RemoveAttribute(CombatFlags.FearUntil);
            wa.RemoveAttribute(CombatFlags.FearSrcX);
            wa.RemoveAttribute(CombatFlags.FearSrcZ);
            wa.RemoveAttribute(CombatFlags.FearHeading);
            RefreshCcGate(wa);
        }

        /// <summary>Drops every control (death/respawn).</summary>
        public static void ClearAll(Entity e)
        {
            var wa = e?.WatchedAttributes;
            if (wa == null) return;
            foreach (var p in Table.Values)
            {
                wa.RemoveAttribute(p.Flag);
                wa.RemoveAttribute(p.Until);
                if (p.HudEffect != null) Execution.SpellExecutor.RemoveEffect(e, p.HudEffect);
            }
            wa.RemoveAttribute(CombatFlags.BreakOnDamageMask);
            ClearFear(wa); // also refreshes the CC gate
            RestoreBlindVision(e);
            if (e is EntityPlayer pl) Integration.PlayerModelSwap.Restore(pl);
        }

        /// <summary>Ends break-on-damage controls after a non-absorbed hit (called from StunPatches post-mitigation).</summary>
        public static void OnDamage(Entity victim)
        {
            var wa = victim?.WatchedAttributes;
            if (wa == null) return;
            int mask = wa.GetInt(CombatFlags.BreakOnDamageMask, 0);
            if (mask == 0) return;
            foreach (var kv in Table)
                if ((mask & (1 << (int)kv.Key)) != 0 && wa.GetBool(kv.Value.Flag)) ClearControl(victim, kv.Key);
        }

        /// <summary>Ends one control plus its type cleanup. Mob AI blocks free themselves once the flag is gone.</summary>
        private static void ClearControl(Entity e, ControlType t)
        {
            var wa = e.WatchedAttributes;
            var p = Table[t];
            wa.SetBool(p.Flag, false);
            wa.RemoveAttribute(p.Until);
            int mask = wa.GetInt(CombatFlags.BreakOnDamageMask, 0) & ~(1 << (int)t);
            if (mask != 0) wa.SetInt(CombatFlags.BreakOnDamageMask, mask); else wa.RemoveAttribute(CombatFlags.BreakOnDamageMask);
            RefreshCcGate(wa);

            if (p.HudEffect != null) Execution.SpellExecutor.RemoveEffect(e, p.HudEffect);
            if (t == ControlType.Stun || t == ControlType.Transmute) RestoreBlindVision(e);
            if (t == ControlType.Fear) ClearFear(wa);
            if (t == ControlType.Transmute && e is EntityPlayer pl) Integration.PlayerModelSwap.Restore(pl);
        }

        public static void Clear(Entity e, ControlType t) => ClearControl(e, t);

        public static void BreakStun(Entity target) => ClearControl(target, ControlType.Stun);

        // 15s window per bracket: full, x0.5, x0.25, then immune. An immune attempt doesn't extend the window.
        private static float DiminishingReturns(Entity target, string bracket, float seconds)
        {
            var wa = target.WatchedAttributes;
            string countKey = "canrpgDr_" + bracket + "_count";
            string resetKey = "canrpgDr_" + bracket + "_reset";
            long now = target.World.ElapsedMilliseconds;
            long resetUntil = wa.GetLong(resetKey, 0);
            // A gap over 15s is stale from before a reload (ElapsedMilliseconds resets each session).
            bool windowValid = now <= resetUntil && resetUntil - now <= 15000L;
            int count = windowValid ? wa.GetInt(countKey, 0) : 0;

            float scale = count switch { 0 => 1f, 1 => 0.5f, 2 => 0.25f, _ => 0f };
            if (scale <= 0f) return 0f;

            wa.SetInt(countKey, count + 1);
            wa.SetLong(resetKey, now + 15000L);
            return seconds * scale;
        }
    }
}
