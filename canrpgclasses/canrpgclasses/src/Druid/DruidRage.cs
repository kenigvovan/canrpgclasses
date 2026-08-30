using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.HarmonyPatches;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Druid
{
    /// <summary>
    /// The druid's Bear-form Rage economy (mirrors <see cref="Warrior.WarriorRage"/>). Rage is a secondary
    /// pool next to mana; gain is gated on the active Bear-form aura, so no rage builds out of form.
    /// </summary>
    public static class DruidRage
    {
        public const string ClassId = "druid";
        public const string PoolId = "rage";

        public static readonly ResourcePoolDef RagePool = new ResourcePoolDef
        {
            Id = PoolId, Max = 100f, RegenPerSec = 0f, StartFull = false,
            ColorR = 0.85f, ColorG = 0.25f, ColorB = 0.15f // rage red
        };

        private static bool registered;
        private static long decayTickId;

        public static void Init()
        {
            if (registered) return;
            registered = true;
            ResourceState.RegisterSecondaryPool(RagePool);
            DamageModifiers.RegisterPersistent(RageHook);
            decayTickId = canrpgclassesModSystem.ServerApi?.Event.RegisterGameTickListener(DecayTick, 1000) ?? 0;
        }

        public static void Stop()
        {
            var api = canrpgclassesModSystem.ServerApi;
            if (api != null && decayTickId != 0) api.Event.UnregisterGameTickListener(decayTickId);
            decayTickId = 0;
            registered = false;
        }

        private static bool IsBearDruid(Entity? e)
            => e is EntityPlayer && TalentState.CurrentClass(e) == ClassId
               && e!.WatchedAttributes.GetString(AttrKeys.ActiveAura, "") == DruidEffectIds.UrsineFormSpellId;

        private static void RageHook(Entity victim, Entity? attacker, DamageSource source, ref float damage)
        {
            if (source == null || damage <= 0f || source.Type == EnumDamageType.Heal) return;

            // Attacker side: a Bear-form druid gains flat Rage for landing a hit (its abilities - unlike the warrior,
            // there are no white weapon swings in form, so ability damage IS the source).
            if (attacker != null && attacker != victim && IsBearDruid(attacker))
                AddRage(attacker, BalanceConfig.Global("druidRagePerHit", 6f));

            if (IsBearDruid(victim))
            {
                float perDamage = BalanceConfig.Global("druidRagePerDamageTaken", 1.5f);
                float cap = BalanceConfig.Global("druidRageTakenCapPerHit", 15f);
                AddRage(victim, Math.Min(damage * perDamage, cap));
            }
        }

        public static void AddRage(Entity e, float amount)
        {
            if (amount <= 0f) return;
            float max = ResourceState.EffectiveMax(e, RagePool);
            ResourceState.Set(e, RagePool, ResourceState.Get(e, RagePool) + amount, max);
        }

        private static void DecayTick(float dt)
        {
            var api = canrpgclassesModSystem.ServerApi;
            if (api?.World == null) return;
            float decay = BalanceConfig.Global("druidRageDecayPerSec", 3f);
            foreach (var pl in api.World.AllOnlinePlayers)
            {
                var e = pl?.Entity;
                if (e == null || TalentState.CurrentClass(e) != ClassId) continue;
                float cur = ResourceState.Get(e, RagePool);
                if (cur <= 0f) continue;
                if (StunPatches.IsInCombat(e)) continue; // in combat rage holds; it bleeds off only in the lull
                ResourceState.Set(e, RagePool, cur - decay);
            }
        }
    }
}
