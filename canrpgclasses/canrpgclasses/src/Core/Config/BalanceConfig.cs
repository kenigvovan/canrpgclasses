using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace canrpgclasses.Core.Config
{
    /// <summary>
    /// Every balance number in the mod, loaded from per-class JSON (plus <c>core.json</c>) with admin overrides
    /// layered on top. Getters take a default, so missing config leaves behaviour unchanged. All buckets live in
    /// one immutable <see cref="Store"/> behind a volatile reference - readers always see a coherent snapshot.
    /// </summary>
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

            /// <summary>Deep copy (outer and inner dictionaries) for copy-on-write edits - published stores
            /// are never mutated, so an Entry handed out earlier keeps reading a stable snapshot.</summary>
            public Store(Store src)
            {
                foreach (var kv in src.spells) spells[kv.Key] = new Dictionary<string, float>(kv.Value);
                foreach (var kv in src.talents) talents[kv.Key] = new Dictionary<string, float>(kv.Value);
                foreach (var kv in src.affinities) affinities[kv.Key] = new Dictionary<string, float>(kv.Value);
                foreach (var kv in src.globals) globals[kv.Key] = kv.Value;
            }
        }

        private static volatile Store current = new Store();

        // The overrides model as loaded/saved by the server (null on a pure client, before LoadOverrides, or
        // when no file exists). Load() re-applies it on every rebuild, so the client-side Load in singleplayer
        // cannot strip admin edits from the shared statics.
        private static volatile BalanceNumbersModel? serverOverrides;

        private static readonly Dictionary<string, float> EmptyMap = new Dictionary<string, float>();

        /// <summary>A read-only view of one config entry's numbers, with per-key defaults. A struct so the
        /// Spell/Talent/Affinity accessors below (called from per-hit combat hooks) return it by value without a
        /// heap allocation - it only wraps a reference to the already-built dictionary.</summary>
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

        // ---- enumeration (read-only), for the admin balance editor to list "what exists" without
        // hardcoding field names per spell/talent - everything here is already a plain dictionary. ----

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

        /// <summary>Live-edits one number (admin balance editor). In-memory only - the caller is responsible for
        /// persisting (<see cref="SaveOverrides"/>) and rebuilding the spell/talent registries so constructors
        /// (which read these values once, at construction) pick up the change. Copy-on-write: a rare admin edit
        /// deep-copies the store, mutates the copy and publishes it, so readers never see a half-edit.</summary>
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

        /// <summary>Loads/merges every <c>config/*.json</c> of this mod into a fresh store and publishes it.
        /// Call once per side from <c>AssetsLoaded</c>, before the spell/talent registries are scanned (their
        /// constructors read this). If the server has loaded persisted admin overrides, they are re-applied on
        /// top of the asset defaults on every rebuild.</summary>
        public static void Load(ICoreAPI api)
        {
            var next = new Store();

            var assets = api.Assets.GetMany("config/", canrpgclassesModSystem.ModId);
            foreach (var asset in assets)
            {
                if (!asset.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                // config/content/ holds class/spell/talent definitions, not balance numbers - a different shape
                // entirely. ContentLoader owns those.
                if (asset.Location.Path.Contains("/content/", StringComparison.OrdinalIgnoreCase)) continue;
                // Same for the attribute definitions - a different shape, owned by RpgAttributes.
                if (asset.Name.StartsWith("attributes", StringComparison.OrdinalIgnoreCase)) continue;

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

        /// <summary>Server-side only, once after assets load. Reads persisted admin edits and rebuilds the whole
        /// store (not an in-place merge), so a world opened later in the same process doesn't inherit leftover
        /// overrides.</summary>
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

        /// <summary>Server-side only. Persists the full merged snapshot (asset defaults plus every override
        /// applied so far) to <see cref="OverridesFile"/> after each admin edit, so it survives a restart.
        /// Writing the whole snapshot rather than a diff avoids tracking what the shipped defaults were; the
        /// cost is that a later mod update can't change a shipped default for an untouched key that shares an
        /// entry with an edited one.</summary>
        public static void SaveOverrides(ICoreServerAPI api)
        {
            var s = current;
            var snapshot = new BalanceNumbersModel { spells = s.spells, talents = s.talents, affinities = s.affinities, globals = s.globals };
            try { api.StoreModConfig(snapshot, OverridesFile); }
            catch (Exception e) { api.Logger.Warning("[canrpgclasses] failed to save balance overrides: {0}", e.Message); }
            // Keep the in-memory override model in sync with the live merged state, so the next Load
            // (e.g. the client side's AssetsLoaded in singleplayer) re-applies exactly what was saved.
            serverOverrides = snapshot;
        }

        /// <summary>Serializes the currently merged numbers (server side) to JSON for network sync.</summary>
        public static string Serialize()
        {
            var s = current;
            var net = new BalanceNumbersModel { spells = s.spells, talents = s.talents, affinities = s.affinities, globals = s.globals };
            return Newtonsoft.Json.JsonConvert.SerializeObject(net);
        }

        /// <summary>Overwrites the local merged numbers from a server-sent JSON blob (client side).
        /// Caller must rebuild the spell/talent registries afterward so constructors re-read these.
        /// The payload is already the server's merged authority - overrides are not re-applied here.</summary>
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

        // Shared shape for the asset config files, the network sync payload, and the persisted overrides file -
        // all three are "the same 4-bucket numbers", just sourced/consumed differently.
        private class BalanceNumbersModel
        {
            public Dictionary<string, Dictionary<string, float>>? spells { get; set; }
            public Dictionary<string, Dictionary<string, float>>? talents { get; set; }
            public Dictionary<string, Dictionary<string, float>>? affinities { get; set; }
            public Dictionary<string, float>? globals { get; set; }
        }
    }
}
