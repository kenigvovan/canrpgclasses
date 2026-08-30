using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;

namespace canrpgclasses.Core.Talents
{
    /// <summary>Discovers and stores talents (mirrors SpellRegistry).</summary>
    public class TalentRegistry
    {
        private readonly Dictionary<string, Talent> talents = new Dictionary<string, Talent>();

        // Precomputed lookups so the per-class / per-tree queries below don't LINQ-scan the whole talent set
        // (all classes) and allocate a Where-closure on every call. ForClass runs on the per-player resource
        // tick, so it must be allocation-free. Rebuilt whenever the talent set changes (ScanAssembly).
        private static readonly Talent[] Empty = Array.Empty<Talent>();
        private Dictionary<string, Talent[]> byClass = new();
        private Dictionary<string, Talent[]> byClassTree = new();

        public IReadOnlyDictionary<string, Talent> All => talents;
        public int Count => talents.Count;

        public void ScanAssembly(Assembly assembly, ILogger? logger = null)
        {
            foreach (var type in canrpgclasses.Core.ModExtensions.Types(assembly, logger))
            {
                if (type.IsAbstract) continue;
                var attr = type.GetCustomAttribute<TalentRegistrationAttribute>();
                if (attr == null || !type.IsSubclassOf(typeof(Talent))) continue;
                if (Activator.CreateInstance(type) is not Talent t || string.IsNullOrEmpty(t.Id)) continue;
                talents[t.Id] = t;
            }

            RebuildDerived();
            logger?.Notification("[canrpgclasses] {0} talent(s) registered", talents.Count);
        }

        /// <summary>Registers one talent built at runtime rather than discovered by attribute - the JSON content
        /// loader's way in. Registration is by id, so a later call replaces an earlier talent of the same id.
        /// Call <see cref="RebuildDerived"/> once after a batch.</summary>
        public void Register(Talent talent)
        {
            if (talent != null && !string.IsNullOrEmpty(talent.Id)) talents[talent.Id] = talent;
        }

        /// <summary>Republishes the combat-hook registries and the per-class caches from the current talent set.
        /// Separate from the scan so JSON content can be registered in between.</summary>
        public void RebuildDerived()
        {
            // Hook talents expose a DamageModifier and/or OnMeleeHit, run by the damage patch and DidAttack.
            var mods = new List<canrpgclasses.Core.DamageModifier>();
            var hits = new List<canrpgclasses.Core.MeleeHitHook>();
            foreach (var t in talents.Values)
            {
                if (t.DamageModifier is { } m) mods.Add(m);
                if (t.OnMeleeHit is { } h) hits.Add(h);
            }
            canrpgclasses.Core.DamageModifiers.Publish(mods);
            canrpgclasses.Core.MeleeHitHooks.Publish(hits);
            RebuildCaches();
        }

        private void RebuildCaches()
        {
            var cls = new Dictionary<string, List<Talent>>();
            var tree = new Dictionary<string, List<Talent>>();
            foreach (var t in talents.Values)
            {
                if (!cls.TryGetValue(t.ClassId, out var cl)) cls[t.ClassId] = cl = new List<Talent>();
                cl.Add(t);
                string tk = t.ClassId + ":" + t.TreeIndex;
                if (!tree.TryGetValue(tk, out var tl)) tree[tk] = tl = new List<Talent>();
                tl.Add(t);
            }
            byClass = cls.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
            byClassTree = tree.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
        }

        public Talent? Get(string id) => id != null && talents.TryGetValue(id, out var t) ? t : null;
        public bool TryGet(string id, out Talent talent) => talents.TryGetValue(id, out talent!);

        // Returns the cached array directly (not a copy) - callers only read it. foreach over a T[] is
        // allocation-free, unlike foreach over IEnumerable/IReadOnlyList.
        public Talent[] ForClass(string classId)
            => classId != null && byClass.TryGetValue(classId, out var list) ? list : Empty;
        public Talent[] ForClassTree(string classId, int treeIndex)
            => classId != null && byClassTree.TryGetValue(classId + ":" + treeIndex, out var list) ? list : Empty;
    }
}
