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
    /// <summary>Rage economy: gain on white swings and hits taken, out-of-combat decay, GainResource impact.</summary>
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

        /// <summary>Resets the static guard so a singleplayer re-enter re-registers the tick on the new ServerApi.
        /// The damage hook and impact are keyed by identity, so re-Init won't duplicate them.</summary>
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

            // Only white melee swings build rage: SourceEntity == attacker rejects projectiles, and our own spell
            // damage is a CanrpgDamageSource.
            if (!heal && damage > 0f && attacker is EntityPlayer ap && IsWarrior(ap)
                && source.Source == EnumDamageSource.Player
                && source.SourceEntity == attacker
                && source is not CanrpgDamageSource)
            {
                GrantMeleeRage(ap);
            }

            if (victim is EntityPlayer vp && IsWarrior(vp))
            {
                if (!heal && damage > 0f && attacker != null)
                    GrantDamageTakenRage(vp, damage);

                // Berserker penalty goes after the rage grant (rage tracks base severity) and before DR/absorb.
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
            // ElapsedMilliseconds resets each session but LastRageHitMs persists, so a negative delta is a stale
            // stamp and must be allowed, or rage gain stays off until uptime catches up.
            long delta = now - last;
            if (delta >= 0 && delta < (long)minInterval) return;
            e.WatchedAttributes.SetLong(WarriorStatKeys.LastRageHitMs, now);

            float flat = BalanceConfig.Global("ragePerMeleeHit", 5f);
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

        /// <summary>Adds rage clamped to the pool max; public for talent hooks such as Boiling Wrath.</summary>
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

            // Stances grant rage only when switching to a different stance than the last rewarded one.
            // ToggleAura runs first, so the active-aura WA is already current.
            float amount;
            if (IsStanceSpell(ctx.Spell.Id))
            {
                // Combat only: out of combat, stance swapping (~3.3 rage/s) outpaces the 3/s decay.
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
