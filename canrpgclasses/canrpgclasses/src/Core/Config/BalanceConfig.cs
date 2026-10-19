using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace canrpgclasses.Core.Config
{
    /// <summary>All balance numbers: per-class JSON plus admin overrides, in one immutable <see cref="Store"/>
    /// behind a volatile ref so readers always see a coherent snapshot.</summary>
    public static class BalanceConfig
    {
        private sealed class Store
        {
            public readonly Dictionary<string, Dictionary<string, float>> spells =
                new Dictionary<string, Dictionary<string, float>>();
            public readonly Dictionary<string, Dictionary<string, float>> talents =
                new Dictionary<string, Dictionary<string, float>>();
            public readonly Dictionary<string, Dictionary<string, float>> affinities =
                new Dictionary<string, Dictionary<string, float>>();
            public readonly Dictionary<string, float> globals = new Dictionary<string, float>();

            public Store() { }

            /// <summary>Deep copy for copy-on-write edits; published stores are never mutated.</summary>
            public Store(Store src)
            {
                foreach (var kv in src.spells) spells[kv.Key] = new Dictionary<string, float>(kv.Value);
                foreach (var kv in src.talents) talents[kv.Key] = new Dictionary<string, float>(kv.Value);
                foreach (var kv in src.affinities) affinities[kv.Key] = new Dictionary<string, float>(kv.Value);
                foreach (var kv in src.globals) globals[kv.Key] = kv.Value;
            }
        }

        private static volatile Store current = new Store();

        // Server-side overrides, re-applied on every Load so the singleplayer client-side Load can't strip admin
        // edits from the shared statics. Null on a pure client.
        private static volatile BalanceNumbersModel? serverOverrides;

        private static readonly Dictionary<string, float> EmptyMap = new Dictionary<string, float>();

        /// <summary>Read-only view of one entry's numbers. A struct so per-hit lookups don't allocate.</summary>
        public readonly struct Entry
        {
            private readonly Dictionary<string, float>? map;
            internal Entry(Dictionary<string, float>? m) { map = m; }

            public float F(string key, float def) => map != null && map.TryGetValue(key, out var v) ? v : def;
            public int I(string key, int def) => map != null && map.TryGetValue(key, out var v) ? (int)Math.Round(v) : def;
            public bool Has(string key) => map != null && map.ContainsKey(key);
        }

        private static readonly Entry Empty = new Entry(null);

        public static Entry Spell(string localId) =>
            current.spells.TryGetValue(localId, out var m) ? new Entry(m) : Empty;

        public static Entry Talent(string talentId) =>
            current.talents.TryGetValue(talentId, out var m) ? new Entry(m) : Empty;

        /// <summary>Gear-affinity numbers, namespaced by class id (e.g. <c>rogue:light_blade</c>).</summary>
        public static Entry Affinity(string classId, string key) =>
            current.affinities.TryGetValue(classId + ":" + key, out var m) ? new Entry(m) : Empty;

        public static float Global(string key, float def) =>
            current.globals.TryGetValue(key, out var v) ? v : def;

        // ---- Enumeration for the admin balance editor ----

        public static IEnumerable<string> SpellIds => current.spells.Keys;
        public static IEnumerable<string> TalentIds => current.talents.Keys;
        public static IEnumerable<string> AffinityIds => current.affinities.Keys;
        public static IEnumerable<string> GlobalIds => current.globals.Keys;

        public static IReadOnlyDictionary<string, float> SpellPairs(string localId) =>
            current.spells.TryGetValue(localId, out var m) ? m : EmptyMap;
        public static IReadOnlyDictionary<string, float> TalentPairs(string talentId) =>
            current.talents.TryGetValue(talentId, out var m) ? m : EmptyMap;
        public static IReadOnlyDictionary<string, float> AffinityPairs(string affinityId) =>
            current.affinities.TryGetValue(affinityId, out var m) ? m : EmptyMap;
        public static IReadOnlyDictionary<string, float> GlobalPairs() => current.globals;

        /// <summary>Which bucket a balance number lives in - carried over the wire by <c>BalanceEditPacket</c>.</summary>
        public enum BalanceCategory { Spell = 0, Talent = 1, Affinity = 2, Global = 3 }

        /// <summary>Copy-on-write edit of one number, in memory only. The caller persists (<see cref="SaveOverrides"/>)
        /// and rebuilds the registries, whose constructors read these values once.</summary>
        public static void Set(BalanceCategory cat, string id, string key, float value)
        {
            var next = new Store(current);
            if (cat == BalanceCategory.Global)
            {
                next.globals[key] = value;
            }
            else
            {
                var dict = cat switch
                {
                    BalanceCategory.Spell => next.spells,
                    BalanceCategory.Talent => next.talents,
                    BalanceCategory.Affinity => next.affinities,
                    _ => throw new ArgumentOutOfRangeException(nameof(cat))
                };
                GetOrCreate(dict, id)[key] = value;
            }
            current = next;
        }

        private static Dictionary<string, float> GetOrCreate(Dictionary<string, Dictionary<string, float>> dict, string id)
        {
            if (!dict.TryGetValue(id, out var m)) { m = new Dictionary<string, float>(); dict[id] = m; }
            return m;
        }

        /// <summary>Merges this mod's <c>config/*.json</c> (plus any server overrides) into a fresh store. Call from
        /// <c>AssetsLoaded</c> before the registries are scanned, since their constructors read it.</summary>
        public static void Load(ICoreAPI api)
        {
            var next = new Store();

            var assets = api.Assets.GetMany("config/", canrpgclassesModSystem.ModId);
            foreach (var asset in assets)
            {
                if (!asset.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                // Other config shapes: ContentLoader, RpgAttributes and XpRules own these.
                if (asset.Location.Path.Contains("/content/", StringComparison.OrdinalIgnoreCase)) continue;
                if (asset.Name.StartsWith("attributes", StringComparison.OrdinalIgnoreCase)) continue;
                if (asset.Name.Equals("xp.json", StringComparison.OrdinalIgnoreCase)) continue;

                BalanceNumbersModel? file;
                try { file = asset.ToObject<BalanceNumbersModel>(); }
                catch (Exception e)
                {
                    api.Logger.Warning("[canrpgclasses] bad balance config {0}: {1}", asset.Location, e.Message);
                    continue;
                }
                if (file == null) continue;

                string classId = asset.Name.Substring(0, asset.Name.Length - ".json".Length);

                if (file.spells != null)
                    foreach (var kv in file.spells) next.spells[kv.Key] = kv.Value;

                if (file.talents != null)
                    foreach (var kv in file.talents) next.talents[classId + ":" + kv.Key] = kv.Value;

                if (file.affinities != null)
                    foreach (var kv in file.affinities) next.affinities[classId + ":" + kv.Key] = kv.Value;

                if (file.globals != null)
                    foreach (var kv in file.globals) next.globals[kv.Key] = kv.Value;
            }

            var over = serverOverrides;
            if (over != null) ApplyOverrides(next, over);

            current = next;

            api.Logger.Notification("[canrpgclasses] balance config: {0} spell, {1} talent, {2} affinity, {3} global entries",
                next.spells.Count, next.talents.Count, next.affinities.Count, next.globals.Count);
        }

        private const string OverridesFile = "canrpgclasses-balance-overrides.json";

        /// <summary>Server only. Rebuilds the whole store rather than merging, so a later world in the same
        /// process doesn't inherit leftover overrides.</summary>
        public static void LoadOverrides(ICoreServerAPI api)
        {
            try { serverOverrides = api.LoadModConfig<BalanceNumbersModel>(OverridesFile); }
            catch (Exception e)
            {
                api.Logger.Warning("[canrpgclasses] bad balance overrides file: {0}", e.Message);
                serverOverrides = null;
            }
            Load(api);
        }

        private static void ApplyOverrides(Store store, BalanceNumbersModel over)
        {
            if (over.spells != null) foreach (var kv in over.spells) MergeInto(store.spells, kv.Key, kv.Value);
            if (over.talents != null) foreach (var kv in over.talents) MergeInto(store.talents, kv.Key, kv.Value);
            if (over.affinities != null) foreach (var kv in over.affinities) MergeInto(store.affinities, kv.Key, kv.Value);
            if (over.globals != null) foreach (var kv in over.globals) store.globals[kv.Key] = kv.Value;
        }

        private static void MergeInto(Dictionary<string, Dictionary<string, float>> target, string id, Dictionary<string, float> src)
        {
            var m = GetOrCreate(target, id);
            foreach (var kv in src) m[kv.Key] = kv.Value;
        }

        /// <summary>Server only. Persists the full merged snapshot, not a diff; downside: a mod update can't change
        /// defaults of untouched keys in an entry that was edited.</summary>
        public static void SaveOverrides(ICoreServerAPI api)
        {
            var s = current;
            var snapshot = new BalanceNumbersModel { spells = s.spells, talents = s.talents, affinities = s.affinities, globals = s.globals };
            try { api.StoreModConfig(snapshot, OverridesFile); }
            catch (Exception e) { api.Logger.Warning("[canrpgclasses] failed to save balance overrides: {0}", e.Message); }
            // So the next Load (e.g. singleplayer client AssetsLoaded) re-applies exactly what was saved.
            serverOverrides = snapshot;
        }

        /// <summary>Serializes the currently merged numbers (server side) to JSON for network sync.</summary>
        public static string Serialize()
        {
            var s = current;
            var net = new BalanceNumbersModel { spells = s.spells, talents = s.talents, affinities = s.affinities, globals = s.globals };
            return Newtonsoft.Json.JsonConvert.SerializeObject(net);
        }

        /// <summary>Client side: replace the numbers with the server's merged set. Caller rebuilds the registries.</summary>
        public static void ApplySerialized(string json)
        {
            BalanceNumbersModel? net;
            try { net = Newtonsoft.Json.JsonConvert.DeserializeObject<BalanceNumbersModel>(json); }
            catch { return; }
            if (net == null) return;

            var next = new Store();
            if (net.spells != null) foreach (var kv in net.spells) next.spells[kv.Key] = kv.Value;
            if (net.talents != null) foreach (var kv in net.talents) next.talents[kv.Key] = kv.Value;
            if (net.affinities != null) foreach (var kv in net.affinities) next.affinities[kv.Key] = kv.Value;
            if (net.globals != null) foreach (var kv in net.globals) next.globals[kv.Key] = kv.Value;
            current = next;
        }

        // Shared shape of the asset files, the sync payload and the overrides file.
        private class BalanceNumbersModel
        {
            public Dictionary<string, Dictionary<string, float>>? spells { get; set; }
            public Dictionary<string, Dictionary<string, float>>? talents { get; set; }
            public Dictionary<string, Dictionary<string, float>>? affinities { get; set; }
            public Dictionary<string, float>? globals { get; set; }
        }
    }
}
