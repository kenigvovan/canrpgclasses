using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Core.Control
{
    /// <summary>The kinds of crowd control the mod applies; each one's profile is a row in ControlState's table.</summary>
    public enum ControlType { Stun, Fear, Silence, Root, Transmute }

    /// <summary>
    /// The mod's single crowd-control layer. Every CC goes through <see cref="Apply"/>: per-bracket diminishing
    /// returns, the timed flag, then the type's side effects. Gates ask <see cref="PreventsMovement"/> and
    /// friends, not the individual flags.
    /// </summary>
    public static class ControlState
    {
        private readonly struct Props
        {
            public readonly bool PreventsMovement, PreventsActions, PreventsCasting, RootsMotion, InterruptsHandUse;
            public readonly string Bracket, Flag, Until;
            public readonly string? HudEffect; // cosmetic effectshud marker shown on a controlled player (null = none)
            public Props(bool move, bool act, bool cast, bool root, bool hand, string bracket, string flag, string until, string? hudEffect = null)
            { PreventsMovement = move; PreventsActions = act; PreventsCasting = cast; RootsMotion = root; InterruptsHandUse = hand; Bracket = bracket; Flag = flag; Until = until; HudEffect = hudEffect; }
        }

        // Each CC's behaviour, declared once. Stun and Fear share the "incap" DR bracket; Stun roots motion,
        // Fear must not - its wander needs the knockback to move the player.
        private static readonly Dictionary<ControlType, Props> Table = new()
        {
            [ControlType.Stun]      = new Props(true,  true,  true,  root: true,  hand: true,  "incap",   CombatFlags.Stunned,     CombatFlags.StunUntil,     Effects.ControlEffectIds.Stunned),
            [ControlType.Fear]      = new Props(true,  true,  true,  root: false, hand: true,  "incap",   CombatFlags.Feared,      CombatFlags.FearUntil,     Effects.ControlEffectIds.Feared),
            [ControlType.Silence]   = new Props(false, false, true,  root: false, hand: false, "silence", CombatFlags.Silenced,    CombatFlags.SilenceUntil,  Effects.ControlEffectIds.Silenced),
            // Root (Freezing Burst): can't move, but CAN still cast and attack. Own DR bracket.
            [ControlType.Root]      = new Props(true,  false, false, root: true,  hand: false, "root",    CombatFlags.Rooted,      CombatFlags.RootUntil,     Effects.ControlEffectIds.Rooted),
            // Transmute: full incapacitate + break-on-damage (set by the impact) + (players) a sheep model swap. Own bracket.
            [ControlType.Transmute] = new Props(true,  true,  true,  root: true,  hand: true,  "poly",    CombatFlags.Polymorphed, CombatFlags.PolymorphUntil, Effects.ControlEffectIds.Polymorphed),
        };

        // Hot path - the physics prefixes run these several times per entity per tick, so they read one
        // aggregate attribute (CcMask, kept in sync by RefreshCcGate) against masks precomputed from the Table.
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

        /// <summary>Recomputes the aggregate CC bitmask (<see cref="CombatFlags.CcMask"/>) from the individual
        /// per-type flags and writes it only on change (removed entirely when no CC is active, so the common case
        /// costs one failed attribute lookup). Must be called by every code path that sets or clears a CC flag -
        /// all of them live here or in EBCombatState's expiry loop.</summary>
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

        /// <summary>Applies a control to a target: shared DR by bracket, then the timed flag + the type's side
        /// effects. Returns false if the target was immune (DR) so the caller can skip particles.</summary>
        public static bool Apply(Entity target, ControlType type, float seconds, Entity? source,
                                 bool breakOnDamage = false, bool blindsVision = false, float visionIntensity = 1f)
        {
            if (target == null || seconds <= 0f) return false;
            var p = Table[type];

            seconds = DiminishingReturns(target, p.Bracket, seconds);
            if (seconds <= 0f) return false; // immune this bracket right now

            long now = target.World.ElapsedMilliseconds;
            long until = now + (long)(seconds * 1000f);
            var wa = target.WatchedAttributes;
            if (until > wa.GetLong(p.Until, 0)) wa.SetLong(p.Until, until);
            wa.SetBool(p.Flag, true);

            // Break-on-damage (Blind stun, Transmute): one bit per type, read by OnDamage. Any type can opt in.
            int mask = wa.GetInt(CombatFlags.BreakOnDamageMask, 0);
            if (breakOnDamage) mask |= 1 << (int)type; else mask &= ~(1 << (int)type);
            if (mask != 0) wa.SetInt(CombatFlags.BreakOnDamageMask, mask); else wa.RemoveAttribute(CombatFlags.BreakOnDamageMask);

            // Cosmetic HUD marker so a controlled player sees an icon + countdown (mobs have no effects HUD). Matches
            // the DR'd duration; cleared early by ClearControl / ClearAll on a break or a manual clear.
            if (target is EntityPlayer && p.HudEffect != null)
                Execution.SpellExecutor.ApplyEffect(target, p.HudEffect, 1, seconds);

            switch (type)
            {
                case ControlType.Stun: ApplyStunEffects(target, seconds, blindsVision, visionIntensity); break;
                case ControlType.Fear: ApplyFearEffects(target, source, seconds); break;
                case ControlType.Root: ApplyRootEffects(target, seconds); break;
                case ControlType.Transmute: ApplyTransmuteEffects(target, seconds); break;
                case ControlType.Silence: break; // flag only
            }

            // Mobs have no EBCombatState to expire the flag - schedule a cleanup at the deadline. (Fear doesn't flag
            // mobs at all - it drives their AI instead - so only stun/silence reach here for a non-player.)
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

            // After the side effects: the mob-fear path removes the Feared flag again, so the gate must be computed last.
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

            // Mobs: stop the current task and block any new one WHILE stunned (tied to the live flag, so a break-on-
            // damage stun frees the AI the instant the flag clears too).
            var mgr = target.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (mgr != null)
            {
                mgr.StopTasks();
                ActionBoolReturn<IAiTask> block = _ => !target.WatchedAttributes.GetBool(CombatFlags.Stunned, false);
                mgr.OnShouldExecuteTask += block;
                target.World.RegisterCallback(_ => mgr.OnShouldExecuteTask -= block, (int)(seconds * 1000f));
            }
        }

        /// <summary>Restores the "psychedelic" value a blind stun overwrote, once it ends (timer or break-on-damage).
        /// Called by EBCombatState (natural expiry) and StunPatches (early break). No-op if no blind is active.</summary>
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
                // A player isn't AI-driven: set up the wander (anchor = current pos, heading = current facing). The
                // Feared flag (set by Apply) takes control away via the shared gates; WanderTick does the moving.
                var wa = target.WatchedAttributes;
                wa.SetDouble(CombatFlags.FearSrcX, target.Pos.X);
                wa.SetDouble(CombatFlags.FearSrcZ, target.Pos.Z);
                wa.SetFloat(CombatFlags.FearHeading, target.Pos.Yaw);
                return;
            }

            // Mobs: real flee task if the creature has one, else briefly block its aggressive tasks (disorient).
            // The Feared flag only means anything for players, and nothing expires WA on a mob - drop it here.
            target.WatchedAttributes.RemoveAttribute(CombatFlags.Feared);
            target.WatchedAttributes.RemoveAttribute(CombatFlags.FearUntil);
            var mgr = target.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (mgr == null) return;
            var flee = mgr.GetTask<AiTaskFleeEntity>();
            if (flee != null && source != null)
            {
                mgr.StopTasks();
                flee.InstaFleeFrom(source);
            }
            else
            {
                mgr.StopTasks();
                ActionBoolReturn<IAiTask> block = t => t is not (AiTaskSeekEntity or AiTaskMeleeAttack);
                mgr.OnShouldExecuteTask += block;
                target.World.RegisterCallback(_ => mgr.OnShouldExecuteTask -= block, (int)(seconds * 1000f));
            }
        }

        // ---- Root side effects (Freezing Burst): stop movement, but leave casting/attacking alone ----
        private static void ApplyRootEffects(Entity target, float seconds)
        {
            target.Pos.Motion.X = 0;
            target.Pos.Motion.Z = 0;
            // Mobs: block only the chase (seek) task while rooted, so a rooted mob can't path to you but can still
            // melee if you're already in range. Tied to the live Rooted flag. Players are handled by the movement gate.
            var mgr = target.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (mgr != null)
            {
                mgr.StopTasks();
                ActionBoolReturn<IAiTask> block = t => !(t is AiTaskSeekEntity && target.WatchedAttributes.GetBool(CombatFlags.Rooted, false));
                mgr.OnShouldExecuteTask += block;
                target.World.RegisterCallback(_ => mgr.OnShouldExecuteTask -= block, (int)(seconds * 1000f));
            }
        }

        // ---- Transmute side effects: full incapacitate (like stun, no blind) + a sheep model on players ----
        private static void ApplyTransmuteEffects(Entity target, float seconds)
        {
            target.Pos.Motion.X = 0;
            target.Pos.Motion.Z = 0;

            // Players: swap the rendered model to a sheep (PlayerModelLib). The functional incap is the shared gates.
            if (target is EntityPlayer pl)
            {
                // Free the single model-swap slot first - ToModel no-ops while a swap is live, so a shaman caught
                // in Spirit Wolf would stay a wolf. Losing the form to CC is thematic anyway.
                Effects.ModelSwapEffects.CancelActive(pl);
                Integration.PlayerModelSwap.ToModel(pl, Integration.PlayerModelSwap.TransmuteModelCode);
                return;
            }

            // Mobs: full AI freeze, like a stun, tied to the live flag.
            var mgr = target.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (mgr != null)
            {
                mgr.StopTasks();
                ActionBoolReturn<IAiTask> block = _ => !target.WatchedAttributes.GetBool(CombatFlags.Polymorphed, false);
                mgr.OnShouldExecuteTask += block;
                target.World.RegisterCallback(_ => mgr.OnShouldExecuteTask -= block, (int)(seconds * 1000f));
            }
        }

        // ---- Player-fear wander tick (registered from StartServerSide via Init) ----
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

        /// <summary>Tears down the server tick listener and resets the static guard, so a second world loaded in the
        /// same process (singleplayer re-enter) re-registers on the new ServerApi. Called from the mod's Dispose.</summary>
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

        /// <summary>Horizontal knockback via the engine's onHurt channel - the only server-side way to move a player
        /// (adding to Pos.Motion does nothing; see SpellExecutor.ApplyDash). Stays under the damage-flash threshold.</summary>
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

        /// <summary>Fully ends a fear: flag, deadline and wander state. The wander keys aren't (flag, until) pairs,
        /// so EBCombatState's generic expiry misses them - every path that ends a fear must come through here.</summary>
        public static void ClearFear(ITreeAttribute wa)
        {
            wa.RemoveAttribute(CombatFlags.Feared);
            wa.RemoveAttribute(CombatFlags.FearUntil);
            wa.RemoveAttribute(CombatFlags.FearSrcX);
            wa.RemoveAttribute(CombatFlags.FearSrcZ);
            wa.RemoveAttribute(CombatFlags.FearHeading);
            RefreshCcGate(wa);
        }

        /// <summary>Drops every control from an entity (death/respawn: you don't come back still stunned or feared).
        /// Also restores a blind's screen effect if one was mid-flight.</summary>
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
            ClearFear(wa); // also refreshes the CC gate (all flags are gone by now)
            RestoreBlindVision(e);
            if (e is EntityPlayer pl) Integration.PlayerModelSwap.Restore(pl); // undo the sheep model
        }

        /// <summary>Ends every break-on-damage control on a victim that just took a non-absorbed hit. Called from
        /// StunPatches after mitigation; no-op when nothing opted in (mask == 0).</summary>
        public static void OnDamage(Entity victim)
        {
            var wa = victim?.WatchedAttributes;
            if (wa == null) return;
            int mask = wa.GetInt(CombatFlags.BreakOnDamageMask, 0);
            if (mask == 0) return;
            foreach (var kv in Table)
                if ((mask & (1 << (int)kv.Key)) != 0 && wa.GetBool(kv.Value.Flag)) ClearControl(victim, kv.Key);
        }

        /// <summary>Ends one control: its flag, deadline and break-on-damage bit, plus the type's cleanup (restore
        /// a blind, clear wander state, restore the model). Mob AI blocks free themselves once the flag is gone.</summary>
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

        /// <summary>Fully clears a single control type by hand (natural expiry paths that need the type's cleanup,
        /// e.g. EBCombatState removing an expired Transmute must also restore the model).</summary>
        public static void Clear(Entity e, ControlType t) => ClearControl(e, t);

        public static void BreakStun(Entity target) => ClearControl(target, ControlType.Stun);

        // Inside a ~15s window each next CC in the same bracket is shortened: full, x0.5, x0.25, then immune.
        // An immune attempt doesn't extend the window. Count + deadline live in WA, so they survive relog.
        private static float DiminishingReturns(Entity target, string bracket, float seconds)
        {
            var wa = target.WatchedAttributes;
            string countKey = "canrpgDr_" + bracket + "_count";
            string resetKey = "canrpgDr_" + bracket + "_reset";
            long now = target.World.ElapsedMilliseconds;
            long resetUntil = wa.GetLong(resetKey, 0);
            // A valid window is never more than 15s ahead of now; a larger gap is a stale future value from before a
            // reload (World.ElapsedMilliseconds resets each session) and must not reuse the old count.
            bool windowValid = now <= resetUntil && resetUntil - now <= 15000L;
            int count = windowValid ? wa.GetInt(countKey, 0) : 0;

            float scale = count switch { 0 => 1f, 1 => 0.5f, 2 => 0.25f, _ => 0f };
            if (scale <= 0f) return 0f; // immune: leave the existing window to expire on its own

            wa.SetInt(countKey, count + 1);
            wa.SetLong(resetKey, now + 15000L);
            return seconds * scale;
        }
    }
}
