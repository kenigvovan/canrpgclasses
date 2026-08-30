using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.HarmonyPatches;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Warrior
{
    /// <summary>
    /// The warrior's Rage economy: gain on white swings and on hits taken (<see cref="RageHook"/>),
    /// out-of-combat decay tick, and the <see cref="ImpactAction.GainResource"/> handler.
    /// </summary>
    public static class WarriorRage
    {
        public const string ClassId = "warrior";
        // Stance spell ids as stored in the active-aura WatchedAttribute (talents gate "in X stance" off these).
        public const string BattleStanceId = "canrpgclasses:offensive_stance";
        public const string BerserkerStanceId = "canrpgclasses:reckless_stance";
        public const string DefensiveStanceId = "canrpgclasses:guarded_stance";

        public static bool IsStanceSpell(string id)
            => id == BattleStanceId || id == BerserkerStanceId || id == DefensiveStanceId;

        private static bool registered;
        private static long decayTickId;

        public static void Init()
        {
            if (registered) return;
            registered = true;
            DamageModifiers.RegisterPersistent(RageHook);
            SpellExecutor.RegisterImpact(ImpactAction.GainResource, ApplyGainResource);
            decayTickId = canrpgclassesModSystem.ServerApi?.Event.RegisterGameTickListener(DecayTick, 1000) ?? 0;
        }

        /// <summary>Tears down the server tick listener and resets the static guard, so a second world loaded in the
        /// same process (singleplayer re-enter) re-registers on the new ServerApi. Called from the mod's Dispose.
        /// The persistent damage hook and impact are keyed by delegate/enum identity, so re-Init won't duplicate them.</summary>
        public static void Stop()
        {
            var api = canrpgclassesModSystem.ServerApi;
            if (api != null && decayTickId != 0) api.Event.UnregisterGameTickListener(decayTickId);
            decayTickId = 0;
            registered = false;
        }

        private static bool IsWarrior(Entity e) => TalentState.CurrentClass(e) == ClassId;

        // ---- The damage hook (runs on every hit, before core mitigation) ----
        private static void RageHook(Entity victim, Entity? attacker, DamageSource source, ref float damage)
        {
            if (source == null) return;
            bool heal = source.Type == EnumDamageType.Heal;

            // Attacker side: a warrior gains flat rage for landing a WHITE melee swing (not spells, not arrows).
            // Source == Player marks a real weapon swing; SourceEntity == attacker rejects projectiles; and our own
            // spell damage is a CanrpgDamageSource (whirlwind/thunder_clap don't build rage).
            if (!heal && damage > 0f && attacker is EntityPlayer ap && IsWarrior(ap)
                && source.Source == EnumDamageSource.Player
                && source.SourceEntity == attacker
                && source is not CanrpgDamageSource)
            {
                GrantMeleeRage(ap);
            }

            // Victim side: a warrior gains rage from hits it takes, and (Berserker stance) takes extra damage.
            if (victim is EntityPlayer vp && IsWarrior(vp))
            {
                if (!heal && damage > 0f && attacker != null)
                    GrantDamageTakenRage(vp, damage);

                // Berserker stance: +received damage. Applied after computing rage (rage income tracks the hit's
                // base severity, not the reckless penalty) and before the rest of mitigation (DR/absorb reduce it).
                if (!heal && damage > 0f
                    && vp.WatchedAttributes.GetString(AttrKeys.ActiveAura, "") == BerserkerStanceId)
                {
                    float takenBonus = BalanceConfig.Spell("reckless_stance").F("takenBonus", 0.10f);
                    damage *= 1f + takenBonus;
                }
            }
        }

        private static void GrantMeleeRage(Entity e)
        {
            long now = e.World.ElapsedMilliseconds;
            long last = e.WatchedAttributes.GetLong(WarriorStatKeys.LastRageHitMs, 0);
            float minInterval = BalanceConfig.Global("rageHitMinIntervalMs", 400f);
            // World.ElapsedMilliseconds is uptime-since-load and resets each session, but LastRageHitMs persists.
            // A negative delta means the clock reset after a reload (stale future timestamp) - treat it as allowed and
            // re-stamp, otherwise rage-from-hits stays disabled until uptime passes the old value. Only a positive
            // delta inside the window is a genuine too-soon spam. (Rate-limit: the prefix runs before vanilla i-frames.)
            long delta = now - last;
            if (delta >= 0 && delta < (long)minInterval) return;
            e.WatchedAttributes.SetLong(WarriorStatKeys.LastRageHitMs, now);

            float flat = BalanceConfig.Global("ragePerMeleeHit", 5f);
            // Fury tree mastery sharpens rage generation (1.0-based blend: unset = 1.0).
            float gen = e.Stats.GetBlended(WarriorStatKeys.RageGeneration);
            if (gen <= 0f) gen = 1f;
            AddRage(e, flat * gen);
        }

        private static void GrantDamageTakenRage(Entity e, float damage)
        {
            float perDamage = BalanceConfig.Global("ragePerDamageTaken", 1.5f);
            float cap = BalanceConfig.Global("rageTakenCapPerHit", 15f);
            float rage = Math.Min(damage * perDamage, cap);

            int specRank = TalentState.Rank(e, "warrior:shield_handling");
            if (specRank > 0)
                rage *= 1f + BalanceConfig.Talent("warrior:shield_handling").F("perRank", 0.15f) * specRank;

            AddRage(e, rage);
        }

        /// <summary>Grant a flat amount of rage (clamped to the pool max). Public so talent hooks (Boiling Wrath's
        /// per-swing rage) can feed the pool without re-implementing the clamp. No-op for a non-warrior / no pool.</summary>
        public static void AddRage(Entity e, float amount)
        {
            if (amount <= 0f) return;
            var pool = ResourceState.PrimaryPool(e);
            if (pool == null) return;
            float max = ResourceState.EffectiveMax(e, pool);
            ResourceState.Set(e, pool, ResourceState.Get(e, pool) + amount, max);
        }

        private static void DecayTick(float dt)
        {
            var api = canrpgclassesModSystem.ServerApi;
            if (api?.World == null) return;
            float decay = BalanceConfig.Global("rageDecayPerSec", 3f); // tick is 1s, so this is a per-tick amount
            foreach (var pl in api.World.AllOnlinePlayers)
            {
                var e = pl?.Entity;
                if (e == null || !IsWarrior(e)) continue;
                var pool = ResourceState.PrimaryPool(e);
                if (pool == null) continue;
                float cur = ResourceState.Get(e, pool);
                if (cur <= 0f) continue;
                if (StunPatches.IsInCombat(e)) continue; // in combat rage holds; it only bleeds off in the lull
                ResourceState.Set(e, pool, cur - decay);
            }
        }

        // ---- GainResource impact (Charge / Wild Rage / All-Out Attack / stance switch) ----
        private static void ApplyGainResource(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (target == null) return;
            var pool = ResourceState.PrimaryPool(target);
            if (pool == null) return;

            // A stance spell carries this impact too, but its grant is the "reward for the dance": rage only when
            // you switch TO a stance different from the last one you were rewarded for (re-toggling the same stance
            // or toggling off grants nothing). The ToggleAura impact runs first, so the active-aura WA is current.
            float amount;
            if (IsStanceSpell(ctx.Spell.Id))
            {
                // Only reward the dance in combat - otherwise alternating two stances on the 1.5s stance cooldown
                // (~3.3 rage/s) outpaces the 3/s out-of-combat decay and lets you macro rage with no enemy present.
                if (!StunPatches.IsInCombat(target)) return;
                string cur = target.WatchedAttributes.GetString(AttrKeys.ActiveAura, "");
                if (string.IsNullOrEmpty(cur)) return; // toggled a stance OFF - no reward
                if (cur == target.WatchedAttributes.GetString(WarriorStatKeys.LastStance, "")) return; // same stance
                target.WatchedAttributes.SetString(WarriorStatKeys.LastStance, cur);
                amount = BalanceConfig.Global("stanceSwitchRage", 5f);
            }
            else
            {
                amount = imp.ResourceGainAmount;
            }

            if (amount <= 0f) return;
            float max = ResourceState.EffectiveMax(target, pool);
            ResourceState.Set(target, pool, ResourceState.Get(target, pool) + amount, max);
        }
    }
}
