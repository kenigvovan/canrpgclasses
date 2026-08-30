using System;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Core.Resources
{
    /// <summary>
    /// Read/write helpers for class resources, stored on WatchedAttributes and synced to the client for the HUD:
    /// the <b>primary pool</b> a class runs on (energy, rage, mana), and <b>combo points</b> that builders add and
    /// finishers spend. Which resource a class uses comes from its class def, never from here.
    /// </summary>
    public static class ResourceState
    {
        public const int ComboMax = 5;

        private static string Key(string poolId) => Core.AttrKeys.ResourcePool(poolId);
        public const string ComboKey = Core.AttrKeys.Combo;
        public const string ComboLastMsKey = Core.AttrKeys.ComboLastMs;

        public static RpgClassDef? CurrentClass(Entity e)
        {
            var mod = canrpgclassesModSystem.For(e?.Api);
            return mod?.Classes.Get(TalentState.CurrentClass(e!));
        }

        public static ResourcePoolDef? PrimaryPool(Entity e) => CurrentClass(e)?.PrimaryResource;

        // ---- Secondary pools: a resource used alongside the primary, like the druid's Bear-form Rage ----
        // Registered by the module that owns the pool, so the core resolves it by id. Storage is already
        // pool-agnostic; only regen differs - a secondary pool drives its own in its module, not EBResources.
        private static readonly System.Collections.Generic.Dictionary<string, ResourcePoolDef> secondaryPools = new();

        public static void RegisterSecondaryPool(ResourcePoolDef pool)
        {
            if (pool != null) secondaryPools[pool.Id] = pool;
        }

        public static ResourcePoolDef? SecondaryPool(string? id)
            => id != null && secondaryPools.TryGetValue(id, out var p) ? p : null;

        /// <summary>The pool's maximum including talent bonuses (read live from talent ranks).</summary>
        public static float EffectiveMax(Entity e, ResourcePoolDef pool)
        {
            float bonus = 0f;
            var mod = canrpgclassesModSystem.For(e?.Api);
            if (mod != null)
            {
                string cls = TalentState.CurrentClass(e!);
                foreach (var t in mod.Talents.ForClass(cls))
                {
                    int rank = TalentState.Rank(e!, t.Id);
                    if (rank > 0) bonus += t.MaxResourceBonus(pool.Id, rank);
                }
            }
            return pool.Max + bonus;
        }

        public static float Get(Entity e, ResourcePoolDef pool)
        {
            string key = Key(pool.Id);
            var wa = e.WatchedAttributes;
            // Only fall back to EffectiveMax, which scans every class talent, when no value is stored yet.
            if (wa.HasAttribute(key)) return (float)wa.GetDouble(key);
            return pool.StartFull ? EffectiveMax(e, pool) : 0f;
        }

        public static void Set(Entity e, ResourcePoolDef pool, float value)
            => Set(e, pool, value, EffectiveMax(e, pool));

        /// <summary>Set with a pre-computed max, so a caller that already knows EffectiveMax (e.g. the regen tick)
        /// doesn't pay another full talent scan.</summary>
        public static void Set(Entity e, ResourcePoolDef pool, float value, float max)
            => e.WatchedAttributes.SetDouble(Key(pool.Id), Math.Max(0f, Math.Min(max, value)));

        public static bool Has(Entity e, ResourcePoolDef? pool, float cost)
            => cost <= 0f || pool == null || Get(e, pool) >= cost;

        public static void Spend(Entity e, ResourcePoolDef? pool, float cost)
        {
            if (cost > 0f && pool != null) Set(e, pool, Get(e, pool) - cost);
        }

        public static int Combo(Entity e) => e.WatchedAttributes.GetInt(ComboKey, 0);

        public static void AddCombo(Entity e, int amount)
        {
            if (amount <= 0) return;
            e.WatchedAttributes.SetInt(ComboKey, Math.Min(ComboMax, Combo(e) + amount));
            e.WatchedAttributes.SetLong(ComboLastMsKey, e.World.ElapsedMilliseconds);
        }

        public static void ResetCombo(Entity e) => e.WatchedAttributes.SetInt(ComboKey, 0);

        public static long ComboLastMs(Entity e) => e.WatchedAttributes.GetLong(ComboLastMsKey, 0);
    }
}
