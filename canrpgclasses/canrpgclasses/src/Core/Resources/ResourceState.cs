using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Core.Resources
{
    /// <summary>Class resource pools and combo points, stored in synced WatchedAttributes.</summary>
    public static class ResourceState
    {
        public const int ComboMax = 5;

        private static string Key(string poolId) => Core.AttrKeys.ResourcePool(poolId);
        public const string ComboKey = Core.AttrKeys.Combo;
        public const string ComboLastMsKey = Core.AttrKeys.ComboLastMs;

        public static RpgClassDef? CurrentClass(Entity e)
        {
            // The "rogue" fallback is for players only: a classless NPC caster must have no pool at all.
            if (e is not EntityPlayer && e?.WatchedAttributes?.HasAttribute(TalentState.ClassKey) != true) return null;
            var mod = canrpgclassesModSystem.For(e?.Api);
            return mod?.Classes.Get(TalentState.CurrentClass(e!));
        }

        public static ResourcePoolDef? PrimaryPool(Entity e) => CurrentClass(e)?.PrimaryResource;

        // ---- Secondary pools (e.g. Bear-form Rage) ----
        // Registered by the owning module, which also drives its regen (EBResources only handles the primary).
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

        /// <summary>Set with a precomputed max, skipping another talent scan.</summary>
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
