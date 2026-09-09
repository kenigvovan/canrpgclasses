using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Core.Attributes
{
    /// <summary>One "each point of this attribute grants PerPoint of Stat" payout, already resolved for one class.</summary>
    public readonly struct AttributeEffect
    {
        public readonly string Stat;
        public readonly float PerPoint;
        /// <summary>Absolute ceiling on the payout (0 = none), so 40 points of dexterity can't mean +40% walk speed.</summary>
        public readonly float Cap;
        /// <summary>Display hint: 1.0-based stats read as a percentage, flat ones as raw points.</summary>
        public readonly bool Flat;

        public AttributeEffect(string stat, float perPoint, float cap, bool flat)
        {
            Stat = stat; PerPoint = perPoint; Cap = cap; Flat = flat;
        }

        /// <summary>The payout for a given number of points, capped.</summary>
        public float Value(float points)
        {
            float v = PerPoint * points;
            if (Cap > 0f) v = Math.Clamp(v, -Cap, Cap);
            return v;
        }
    }

    /// <summary>An attribute (strength, wisdom…) from <c>config/attributes.json</c>. The attribute itself is an
    /// ordinary EntityStats key - class grants, talents, gear and the admin command all feed it the same way -
    /// and its effects are what it pays back out. Points are read 0-based, like every 1.0-based stat here.</summary>
    public sealed class AttributeDef
    {
        public readonly string Id;
        public readonly int Order;
        /// <summary>Points past which each further point counts for <see cref="SoftcapFactor"/> of one (0 = linear).</summary>
        public readonly float Softcap;
        public readonly float SoftcapFactor;

        /// <summary>Every stat this attribute can write, across all classes - the payout always writes all of them
        /// (0 where a class has no such effect), so nothing lingers after a class change.</summary>
        public readonly IReadOnlyList<string> Stats;

        private readonly AttributeEffect[] fallback;
        private readonly Dictionary<string, AttributeEffect[]> byClass;
        private readonly string? name;

        public AttributeDef(string id, int order, string? name, float softcap, float softcapFactor,
                            AttributeEffect[] fallback, Dictionary<string, AttributeEffect[]> byClass)
        {
            Id = id; Order = order; this.name = name;
            Softcap = softcap; SoftcapFactor = softcapFactor;
            this.fallback = fallback; this.byClass = byClass;
            Stats = fallback.Select(e => e.Stat).ToArray();
        }

        /// <summary>Localized name: lang key <c>canrpgclasses:attr-&lt;id&gt;</c> wins, else the JSON name, else the id.</summary>
        public string DisplayName
        {
            get
            {
                string key = "canrpgclasses:attr-" + Id;
                if (Lang.HasTranslation(key, true, false)) return Lang.Get(key);
                return string.IsNullOrEmpty(name) ? Id : name!;
            }
        }

        /// <summary>The payouts as this class sees them (per-class overrides and affinity already folded in).
        /// Always covers <see cref="Stats"/> in full.</summary>
        public IReadOnlyList<AttributeEffect> EffectsFor(string classId)
            => !string.IsNullOrEmpty(classId) && byClass.TryGetValue(classId, out var e) ? e : fallback;

        /// <summary>Raw points on an entity, from every source. Unset blends to 1.0, so the base comes off.</summary>
        public float Points(Entity e) => e.Stats.GetBlended(Id) - 1f;

        /// <summary>Points as the payouts see them: past the softcap each further point counts for less.</summary>
        public float EffectivePoints(Entity e)
        {
            float p = Points(e);
            if (Softcap <= 0f || p <= Softcap) return p;
            return Softcap + (p - Softcap) * SoftcapFactor;
        }
    }

    /// <summary>What being a class does to attributes: points handed out, and how much payout each point buys.</summary>
    public sealed class ClassAttributes
    {
        public readonly Dictionary<string, float> Base = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, float> PerLevel = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>Multiplier on every payout of an attribute - "intelligence is worth 2× to a mage, 0.1× to a warrior".</summary>
        public readonly Dictionary<string, float> Affinity = new(StringComparer.OrdinalIgnoreCase);

        public float Granted(string attrId, int levelsAboveFirst)
            => (Base.TryGetValue(attrId, out var b) ? b : 0f)
             + (PerLevel.TryGetValue(attrId, out var p) ? p : 0f) * levelsAboveFirst;
    }

    /// <summary>The attribute definitions, loaded from <c>config/attributes*.json</c> on both sides: a server can
    /// rename, drop or invent attributes without touching C#. No file means an empty registry and no attributes at
    /// all.</summary>
    public static class RpgAttributes
    {
        /// <summary>Stat source of one attribute's payout - one per attribute, so two attributes can feed one stat.</summary>
        public static string PayoutSource(string attrId) => "canrpgattr_" + attrId;

        /// <summary>Stat source of the class grant on the attribute itself (base + per level).</summary>
        public const string ClassSource = "canrpgattrclass";

        /// <summary>Stat source of the <c>/canrpgattr</c> admin bonus. Persistent, so it survives a relog.</summary>
        public const string AdminSource = "canrpgattr_admin";

        private static AttributeDef[] defs = Array.Empty<AttributeDef>();
        private static Dictionary<string, AttributeDef> byId = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, ClassAttributes> byClass = new(StringComparer.OrdinalIgnoreCase);

        // (stat, source) pairs this config writes, plus the ones an earlier config wrote and this one doesn't:
        // stat sources outlive a reload within the session, so they need clearing.
        private static (string Stat, string Source)[] pairs = Array.Empty<(string, string)>();
        private static (string Stat, string Source)[] stale = Array.Empty<(string, string)>();

        // The merged definitions exactly as they were read, kept so the server can hand them to a joining client.
        private static AttributeFileModel source = new();

        // Affinities a class declares on itself (see RpgClassDef.AttributeAffinity), layered over the JSON ones.
        private static Dictionary<string, Dictionary<string, float>> classAffinity =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly ClassAttributes NoGrant = new();

        /// <summary>Definitions in display order.</summary>
        public static IReadOnlyList<AttributeDef> All => defs;
        public static bool Any => defs.Length > 0;

        public static AttributeDef? Get(string id) => id != null && byId.TryGetValue(id, out var d) ? d : null;
        public static bool IsAttribute(string statKey) => statKey != null && byId.ContainsKey(statKey);
        public static IEnumerable<string> Ids => defs.Select(d => d.Id);

        public static ClassAttributes ForClass(string classId)
            => !string.IsNullOrEmpty(classId) && byClass.TryGetValue(classId, out var g) ? g : NoGrant;

        internal static IReadOnlyList<(string Stat, string Source)> StalePairs => stale;

        /// <summary>Reads every <c>config/attributes*.json</c> of this mod. Call once per side from
        /// <c>AssetsLoaded</c>; a later file's definition of the same id wins, so an add-on can replace ours.</summary>
        public static void Load(ICoreAPI api)
        {
            var merged = new AttributeFileModel
            {
                attributes = new List<AttributeModel>(),
                classes = new Dictionary<string, ClassGrantModel>(StringComparer.OrdinalIgnoreCase)
            };
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // id -> slot in the list

            foreach (var asset in api.Assets.GetMany("config/", canrpgclassesModSystem.ModId))
            {
                if (!IsAttributeFile(asset.Name)) continue;

                AttributeFileModel? file;
                try { file = asset.ToObject<AttributeFileModel>(); }
                catch (Exception e)
                {
                    api.Logger.Warning("[canrpgclasses] bad attribute config {0}: {1}", asset.Location, e.Message);
                    continue;
                }
                if (file == null) continue;

                if (file.attributes != null)
                    foreach (var m in file.attributes)
                    {
                        if (string.IsNullOrWhiteSpace(m.id))
                        {
                            api.Logger.Warning("[canrpgclasses] {0}: an attribute has no 'id'", asset.Location);
                            continue;
                        }
                        string id = m.id!.Trim();
                        if (index.TryGetValue(id, out int slot)) merged.attributes![slot] = m;
                        else { index[id] = merged.attributes!.Count; merged.attributes.Add(m); }
                    }

                if (file.classes != null)
                    foreach (var kv in file.classes)
                    {
                        if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value == null) continue;
                        if (!merged.classes!.TryGetValue(kv.Key, out var grant))
                            merged.classes[kv.Key] = grant = new ClassGrantModel();
                        grant.@base = Merge(grant.@base, kv.Value.@base);
                        grant.perLevel = Merge(grant.perLevel, kv.Value.perLevel);
                        grant.affinity = Merge(grant.affinity, kv.Value.affinity);
                    }
            }

            Publish(merged, api.Logger);
        }

        /// <summary>Folds the affinities a class declares on itself over the ones from JSON, and rebuilds. Call after
        /// every registry rebuild: payouts are resolved per class ahead of time, so the classes must be known.</summary>
        public static void SyncClassAffinity(IEnumerable<(string ClassId, IReadOnlyList<(string Attribute, float Multiplier)> Affinity)> classes,
                                             ILogger? logger)
        {
            var next = new Dictionary<string, Dictionary<string, float>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (classId, affinity) in classes)
            {
                if (string.IsNullOrEmpty(classId) || affinity == null || affinity.Count == 0) continue;
                var map = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                foreach (var (attr, mul) in affinity) map[attr] = mul;
                next[classId] = map;
            }

            // Neither side has anything (the common case) - rebuilding would churn every entity's payouts for nothing.
            if (next.Count == 0 && classAffinity.Count == 0) return;

            classAffinity = next;
            Publish(source, logger);
        }

        /// <summary>The merged definitions as JSON, for the server to hand a joining client - otherwise the client
        /// reads its own file, which need not match.</summary>
        public static string Serialize() => Newtonsoft.Json.JsonConvert.SerializeObject(source);

        /// <summary>Client side: adopt the server's definitions. Payouts are recomputed on the next stat pass,
        /// which the caller triggers through <c>ReapplyStatTalents</c>.</summary>
        public static void ApplySerialized(string json, ILogger? logger)
        {
            if (string.IsNullOrEmpty(json)) return;
            AttributeFileModel? file;
            try { file = Newtonsoft.Json.JsonConvert.DeserializeObject<AttributeFileModel>(json); }
            catch (Exception e) { logger?.Warning("[canrpgclasses] bad attribute sync: {0}", e.Message); return; }
            if (file != null) Publish(file, logger);
        }

        private static Dictionary<string, float>? Merge(Dictionary<string, float>? into, Dictionary<string, float>? from)
        {
            if (from == null) return into;
            into ??= new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in from) into[kv.Key] = kv.Value;
            return into;
        }

        /// <summary>Resolves one merged definition set into the live registry - the single path both the asset
        /// load and the network sync go through.</summary>
        private static void Publish(AttributeFileModel file, ILogger? logger)
        {
            var models = new List<AttributeModel>();
            foreach (var m in file.attributes ?? new List<AttributeModel>())
                if (!string.IsNullOrWhiteSpace(m.id)) models.Add(m);

            var classes = new Dictionary<string, ClassAttributes>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in file.classes ?? new Dictionary<string, ClassGrantModel>())
            {
                if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value == null) continue;
                var grant = new ClassAttributes();
                Copy(kv.Value.@base, grant.Base);
                Copy(kv.Value.perLevel, grant.PerLevel);
                Copy(kv.Value.affinity, grant.Affinity);
                classes[kv.Key] = grant;
            }

            // A class's own affinity wins over the JSON entry, and a class the JSON never mentions gets its own.
            foreach (var kv in classAffinity)
            {
                if (!classes.TryGetValue(kv.Key, out var grant)) classes[kv.Key] = grant = new ClassAttributes();
                foreach (var a in kv.Value) grant.Affinity[a.Key] = a.Value;
            }

            // Every class that changes an attribute's payout needs its own resolved effect list.
            var affected = new HashSet<string>(classes.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var m in models)
                if (m.effects != null)
                    foreach (var e in m.effects)
                        if (e.perClass != null)
                            foreach (var cls in e.perClass.Keys) affected.Add(cls);

            var built = new List<AttributeDef>();
            foreach (var m in models)
            {
                var def = Build(m, classes, affected, logger);
                if (def != null) built.Add(def);
            }

            built.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));

            var idx = new Dictionary<string, AttributeDef>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in built) idx[d.Id] = d;

            // A grant naming an attribute that doesn't exist is a typo that would silently do nothing.
            foreach (var kv in classes)
                foreach (var attr in kv.Value.Base.Keys.Concat(kv.Value.PerLevel.Keys).Concat(kv.Value.Affinity.Keys))
                    if (!idx.ContainsKey(attr))
                        logger?.Warning("[canrpgclasses] class '{0}' names unknown attribute '{1}'", kv.Key, attr);

            // An attribute id IS its stat key: named after a real stat, its points would act as that stat.
            foreach (var d in built)
                if (Array.Exists(ReservedStatKeys, k => string.Equals(k, d.Id, StringComparison.OrdinalIgnoreCase)))
                    logger?.Warning("[canrpgclasses] attribute '{0}' has the same key as an existing stat - "
                        + "rename it, or the two will feed each other", d.Id);

            var next = new HashSet<(string, string)>();
            foreach (var d in built)
            {
                next.Add((d.Id, ClassSource));
                foreach (var stat in d.Stats) next.Add((stat, PayoutSource(d.Id)));
            }
            stale = pairs.Concat(stale).Where(p => !next.Contains(p)).Distinct().ToArray();
            pairs = next.ToArray();

            defs = built.ToArray();
            byId = idx;
            byClass = classes;
            source = file;

            Client.StatCatalog.Invalidate(); // the editors' stat dropdown lists attributes too

            if (defs.Length > 0)
                logger?.Notification("[canrpgclasses] attributes: {0} ({1}), {2} class grant(s)",
                    defs.Length, string.Join(", ", Ids), byClass.Count);
        }

        // Stat keys the codebase reads, by reflection off StatKeys - an attribute id is checked against them.
        private static readonly string[] ReservedStatKeys = BuildReservedStatKeys();

        private static string[] BuildReservedStatKeys()
        {
            var list = new List<string>();
            foreach (var f in typeof(StatKeys).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                if (f.FieldType == typeof(string) && f.GetValue(null) is string key && key.Length > 0)
                    list.Add(key);
            return list.ToArray();
        }

        private static bool IsAttributeFile(string assetName)
            => assetName.StartsWith("attributes", StringComparison.OrdinalIgnoreCase)
            && assetName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        private static void Copy(Dictionary<string, float>? src, Dictionary<string, float> dst)
        {
            if (src == null) return;
            foreach (var kv in src) dst[kv.Key] = kv.Value;
        }

        private static AttributeDef? Build(AttributeModel m, Dictionary<string, ClassAttributes> classes,
                                           HashSet<string> affectedClasses, ILogger? logger)
        {
            string id = m.id!.Trim();

            // One resolved effect list per class that changes it, plus a fallback. All cover the same stats, so a
            // class switch always overwrites a payout instead of leaving it.
            var stats = new List<string>();
            var perClassPayout = new Dictionary<string, Dictionary<string, float>>(StringComparer.OrdinalIgnoreCase);
            var basePayout = new Dictionary<string, float>(StringComparer.Ordinal);
            var caps = new Dictionary<string, float>(StringComparer.Ordinal);
            var flats = new HashSet<string>(StringComparer.Ordinal);

            foreach (var cls in affectedClasses) perClassPayout[cls] = new Dictionary<string, float>(StringComparer.Ordinal);

            if (m.effects != null)
                foreach (var e in m.effects)
                    foreach (string stat in ExpandStats(e, id, logger))
                    {
                        if (!basePayout.ContainsKey(stat)) { basePayout[stat] = 0f; stats.Add(stat); }
                        // Overlapping school groups can name one stat twice; one source per attribute means summing.
                        basePayout[stat] += e.perPoint;
                        if (e.cap > 0f) caps[stat] = Math.Max(caps.TryGetValue(stat, out var c) ? c : 0f, e.cap);
                        if (string.Equals(e.display, "flat", StringComparison.OrdinalIgnoreCase)) flats.Add(stat);

                        foreach (var cls in affectedClasses)
                        {
                            float perPoint = e.perClass != null && e.perClass.TryGetValue(cls, out var over)
                                ? over : e.perPoint;
                            float affinity = classes.TryGetValue(cls, out var grant)
                                && grant.Affinity.TryGetValue(id, out var a) ? a : 1f;
                            perClassPayout[cls].TryGetValue(stat, out var cur);
                            perClassPayout[cls][stat] = cur + perPoint * affinity;
                        }
                    }

            AttributeEffect[] Pack(Dictionary<string, float> payout) => stats
                .Select(s => new AttributeEffect(s, payout.TryGetValue(s, out var v) ? v : 0f,
                                                 caps.TryGetValue(s, out var c) ? c : 0f, flats.Contains(s)))
                .ToArray();

            var byCls = new Dictionary<string, AttributeEffect[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in perClassPayout) byCls[kv.Key] = Pack(kv.Value);

            float factor = m.softcapFactor > 0f ? m.softcapFactor : 0.5f;
            return new AttributeDef(id, m.order, m.name, m.softcap, factor, Pack(basePayout), byCls);
        }

        /// <summary>A literal stat key, or - with <c>schools</c> set - one key per school of the spellpower, resist
        /// or penetration family.</summary>
        private static IEnumerable<string> ExpandStats(EffectModel e, string attrId, ILogger? logger)
        {
            if (string.IsNullOrWhiteSpace(e.stat))
            {
                logger?.Warning("[canrpgclasses] attribute '{0}' has an effect with no 'stat'", attrId);
                yield break;
            }

            string stat = e.stat!.Trim();
            if (string.IsNullOrWhiteSpace(e.schools)) { yield return stat; yield break; }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var school in ParseSchools(e.schools!, logger))
            {
                string? key = stat.ToLowerInvariant() switch
                {
                    "spellpower" => StatKeys.SpellpowerFor(school),
                    "resist" => DamageSchools.For(school).ResistStat,
                    "pen" or "penetration" => DamageSchools.For(school).PenStat,
                    _ => null
                };
                if (key == null)
                {
                    logger?.Warning("[canrpgclasses] attribute '{0}' uses 'schools' with stat '{1}' - expected spellpower, resist or pen",
                        attrId, stat);
                    yield break;
                }
                if (seen.Add(key)) yield return key; // the two physical schools share one resist key
            }
        }

        /// <summary>"magical", "physical", "all", or a comma-separated list of school names.</summary>
        private static IEnumerable<SpellSchool> ParseSchools(string spec, ILogger? logger)
        {
            var all = Enum.GetValues<SpellSchool>();
            switch (spec.Trim().ToLowerInvariant())
            {
                case "all": return all;
                case "magic" or "magical": return all.Where(s => !DamageSchools.For(s).Physical);
                case "physical": return all.Where(s => DamageSchools.For(s).Physical);
            }

            var list = new List<SpellSchool>();
            foreach (var part in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<SpellSchool>(part, true, out var school)) list.Add(school);
                else logger?.Warning("[canrpgclasses] unknown school '{0}' in an attribute effect", part);
            }
            return list;
        }

        // ---- JSON shape ----

        private class AttributeFileModel
        {
            public List<AttributeModel>? attributes { get; set; }
            /// <summary>Per class id: the points it hands out and how much each point is worth to it.</summary>
            public Dictionary<string, ClassGrantModel>? classes { get; set; }
        }

        private class AttributeModel
        {
            public string? id { get; set; }
            /// <summary>Fallback name when there is no <c>attr-&lt;id&gt;</c> lang entry.</summary>
            public string? name { get; set; }
            public int order { get; set; }
            /// <summary>Points past which each further point is worth <see cref="softcapFactor"/> of one (0 = linear).</summary>
            public float softcap { get; set; }
            public float softcapFactor { get; set; } = 0.5f;
            public List<EffectModel>? effects { get; set; }
        }

        private class EffectModel
        {
            /// <summary>A stat key, or one of spellpower/resist/pen when <see cref="schools"/> is set.</summary>
            public string? stat { get; set; }
            /// <summary>"magical", "physical", "all" or e.g. "fire,frost". Omit for a literal stat key.</summary>
            public string? schools { get; set; }
            public float perPoint { get; set; }
            /// <summary>Per-class replacement for <see cref="perPoint"/>, e.g. dexterity arming only a rogue's melee.</summary>
            public Dictionary<string, float>? perClass { get; set; }
            /// <summary>Ceiling on the payout, whatever the point total.</summary>
            public float cap { get; set; }
            /// <summary>"flat" to show the payout as points on the character sheet instead of a percentage.</summary>
            public string? display { get; set; }
        }

        private class ClassGrantModel
        {
            public Dictionary<string, float>? @base { get; set; }
            public Dictionary<string, float>? perLevel { get; set; }
            public Dictionary<string, float>? affinity { get; set; }
        }
    }
}
