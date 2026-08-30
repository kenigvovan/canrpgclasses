using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace canrpgclasses.Core.Classes
{
    /// <summary>
    /// Config-driven gate on which RPG class a player may pick, keyed on vanilla class and race. The client gets
    /// a copy so the picker can grey out refusals; <see cref="ClassChange.TryChange"/> re-checks and stays the
    /// authority.
    /// </summary>
    public static class ClassRestrictions
    {
        /// <summary>Failure reasons handed to <see cref="ClassChange.TryChange"/>; lang key suffixes under
        /// <c>canrpgclasses:classchange-</c>. Two of them, so the player is told what blocked the pick.</summary>
        public const string ReasonClass = "vanilla-restricted";
        public const string ReasonRace = "race-restricted";

        /// <summary>WatchedAttributes key the vanilla CharacterSystem stores the chosen character class under.</summary>
        public const string VanillaClassAttr = "characterClass";

        public const string ConfigFile = "canrpgclasses-restrictions.json";

        private const string DefaultKey = "*";

        private sealed class Rules
        {
            public bool Enabled;
            // Either axis can be switched off on its own: a server may want race gating without touching what
            // the vanilla character classes may become, or the reverse.
            public bool VanillaClassRulesEnabled = true;
            public bool RaceRulesEnabled = true;
            public bool AllowWhenVanillaUnknown = true;
            public readonly Dictionary<string, (HashSet<string> Allow, HashSet<string> Deny)> ByVanilla =
                new(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, (HashSet<string> Allow, HashSet<string> Deny)> ByRace =
                new(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, RpgLimits> ByRpg = new(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>The per-RPG-class half of the rules: who may become it.</summary>
        private sealed class RpgLimits
        {
            public HashSet<string> RequireVanilla = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> DenyVanilla = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> RequireRace = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> DenyRace = new(StringComparer.OrdinalIgnoreCase);
        }

        // Immutable after publish, swapped atomically - same discipline as BalanceConfig's store, since the
        // client and server sides of a singleplayer process share these statics.
        private static volatile Rules current = new Rules();

        public static bool Enabled => current.Enabled;

        /// <summary>The player's vanilla character class code ("" when they have not picked one).</summary>
        public static string VanillaClassOf(Entity? e) =>
            e?.WatchedAttributes?.GetString(VanillaClassAttr, "") ?? "";

        /// <summary>The player's race - their own PlayerModelLib model code, not the form they are currently
        /// rendered as. "seraph" (the default model) when that mod isn't installed.</summary>
        public static string RaceOf(Entity? e) => Integration.PlayerModelSwap.BaseModelOf(e);

        /// <summary>Whether <paramref name="rpgClassId"/> is a legal pick for this player. Works on either
        /// side. <paramref name="reason"/> is a lang key suffix on failure.</summary>
        public static bool Allowed(Entity? entity, string rpgClassId, out string reason)
        {
            reason = "";
            var r = current;
            if (!r.Enabled || string.IsNullOrEmpty(rpgClassId)) return true;

            string vanilla = VanillaClassOf(entity);
            string race = RaceOf(entity);

            // Skipped for a player who hasn't picked a vanilla class yet, when the config allows that.
            bool checkVanilla = r.VanillaClassRulesEnabled && (vanilla.Length > 0 || !r.AllowWhenVanillaUnknown);

            // "What may this vanilla class become" - a specific entry replaces the "*" default entirely.
            if (checkVanilla && !Passes(r.ByVanilla, vanilla, rpgClassId)) { reason = ReasonClass; return false; }

            // "What may this race become". Race is always known (it falls back to the default model), so this
            // half needs no equivalent of the unknown-vanilla-class escape above.
            if (r.RaceRulesEnabled && !Passes(r.ByRace, race, rpgClassId)) { reason = ReasonRace; return false; }

            // "Who may become this RPG class" - the same two axes stated from the class's side, and gated by the
            // same two switches.
            if (r.ByRpg.TryGetValue(rpgClassId, out var c))
            {
                if (checkVanilla)
                {
                    if (c.DenyVanilla.Contains(vanilla)) { reason = ReasonClass; return false; }
                    if (c.RequireVanilla.Count > 0 && !c.RequireVanilla.Contains(vanilla)) { reason = ReasonClass; return false; }
                }
                if (r.RaceRulesEnabled)
                {
                    if (c.DenyRace.Contains(race)) { reason = ReasonRace; return false; }
                    if (c.RequireRace.Count > 0 && !c.RequireRace.Contains(race)) { reason = ReasonRace; return false; }
                }
            }

            return true;
        }

        /// <summary>One allow/deny table: the entry for <paramref name="key"/>, or the <c>"*"</c> default when it
        /// has none of its own (a specific entry replaces the default rather than stacking, so exceptions work).
        /// An empty allow list means "no whitelist", not "nothing allowed".</summary>
        private static bool Passes(Dictionary<string, (HashSet<string> Allow, HashSet<string> Deny)> table,
            string key, string rpgClassId)
        {
            if (!table.TryGetValue(key, out var rule) && !table.TryGetValue(DefaultKey, out rule)) return true;
            if (rule.Deny != null && rule.Deny.Contains(rpgClassId)) return false;
            if (rule.Allow != null && rule.Allow.Count > 0 && !rule.Allow.Contains(rpgClassId)) return false;
            return true;
        }

        /// <summary>Server side, once at startup. Reads (and, when absent, writes a disabled-by-default
        /// template of) the mod-config file. A broken file leaves the feature off rather than locking players
        /// out of picking a class at all.</summary>
        public static void LoadServer(ICoreServerAPI api)
        {
            Model? model = null;
            try { model = api.LoadModConfig<Model>(ConfigFile); }
            catch (Exception e)
            {
                api.Logger.Warning("[canrpgclasses] bad {0} ({1}) - class restrictions disabled", ConfigFile, e.Message);
            }

            if (model == null)
            {
                model = Template();
                try { api.StoreModConfig(model, ConfigFile); }
                catch (Exception e) { api.Logger.Warning("[canrpgclasses] could not write {0}: {1}", ConfigFile, e.Message); }
            }

            Apply(model);

            if (current.Enabled)
                api.Logger.Notification("[canrpgclasses] class restrictions: {0} vanilla-class rule(s) ({1}), {2} race rule(s) ({3}), {4} rpg-class rule(s)",
                    current.ByVanilla.Count, current.VanillaClassRulesEnabled ? "on" : "off",
                    current.ByRace.Count, current.RaceRulesEnabled ? "on" : "off",
                    current.ByRpg.Count);
        }

        private static void Apply(Model? m)
        {
            var next = new Rules();
            if (m != null)
            {
                next.Enabled = m.enabled;
                next.VanillaClassRulesEnabled = m.vanillaClassRulesEnabled;
                next.RaceRulesEnabled = m.raceRulesEnabled;
                next.AllowWhenVanillaUnknown = m.allowWhenVanillaUnknown;

                if (m.byVanillaClass != null)
                    foreach (var kv in m.byVanillaClass)
                        next.ByVanilla[kv.Key] = (Set(kv.Value?.allow), Set(kv.Value?.deny));

                if (m.byRace != null)
                    foreach (var kv in m.byRace)
                        next.ByRace[kv.Key] = (Set(kv.Value?.allow), Set(kv.Value?.deny));

                if (m.byRpgClass != null)
                    foreach (var kv in m.byRpgClass)
                        next.ByRpg[kv.Key] = new RpgLimits
                        {
                            RequireVanilla = Set(kv.Value?.requireVanilla),
                            DenyVanilla = Set(kv.Value?.denyVanilla),
                            RequireRace = Set(kv.Value?.requireRace),
                            DenyRace = Set(kv.Value?.denyRace)
                        };
            }
            current = next;
        }

        /// <summary>Server -> client payload: the live rules as JSON. Sent on join so the class picker can
        /// grey out what the server would refuse.</summary>
        public static string Serialize()
        {
            var r = current;
            var m = new Model
            {
                enabled = r.Enabled,
                vanillaClassRulesEnabled = r.VanillaClassRulesEnabled,
                raceRulesEnabled = r.RaceRulesEnabled,
                allowWhenVanillaUnknown = r.AllowWhenVanillaUnknown,
                byVanillaClass = Table(r.ByVanilla),
                byRace = Table(r.ByRace),
                byRpgClass = new Dictionary<string, RpgRule>()
            };
            foreach (var kv in r.ByRpg)
                m.byRpgClass[kv.Key] = new RpgRule
                {
                    requireVanilla = new List<string>(kv.Value.RequireVanilla),
                    denyVanilla = new List<string>(kv.Value.DenyVanilla),
                    requireRace = new List<string>(kv.Value.RequireRace),
                    denyRace = new List<string>(kv.Value.DenyRace)
                };
            return Newtonsoft.Json.JsonConvert.SerializeObject(m);
        }

        /// <summary>Client side: adopt the server's rules. A malformed payload clears them (the picker then
        /// shows everything as available and the server refusal is the only gate) rather than throwing.</summary>
        public static void ApplySerialized(string json)
        {
            Model? m;
            try { m = Newtonsoft.Json.JsonConvert.DeserializeObject<Model>(json); }
            catch { m = null; }
            Apply(m);
        }

        private static Dictionary<string, VanillaRule> Table(
            Dictionary<string, (HashSet<string> Allow, HashSet<string> Deny)> src)
        {
            var outp = new Dictionary<string, VanillaRule>();
            foreach (var kv in src)
                outp[kv.Key] = new VanillaRule
                {
                    allow = new List<string>(kv.Value.Allow ?? new HashSet<string>()),
                    deny = new List<string>(kv.Value.Deny ?? new HashSet<string>())
                };
            return outp;
        }

        private static HashSet<string> Set(List<string>? src)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (src != null) foreach (var s in src) if (!string.IsNullOrEmpty(s)) set.Add(s);
            return set;
        }

        // The file an admin actually edits: off by default, with the syntax explained inline (the mod-config
        // folder has no other place to document it) and one worked example per rule direction.
        private static Model Template() => new Model
        {
            _comment = "Which RPG class a player may pick, based on their VANILLA character class and their RACE. Nothing is restricted until enabled=true. The two axes have their own switches on top of that: vanillaClassRulesEnabled and raceRulesEnabled - turn either off to use only the other. Vanilla class codes: commoner, hunter, malefactor, clockmaker, blackguard, tailor (plus whatever other mods add). RPG class ids: druid, hunter, mage, paladin, priest, rogue, shaman, warrior.",
            _comment_rules = "Rule directions, all enabled ones must pass. byVanillaClass / byRace = 'what may this vanilla class / this race become': allow (whitelist - empty means no whitelist) and deny (blacklist) of RPG class ids. The '*' entry is the default, used only when the player's own class/race has no entry of its own - a specific entry replaces the default instead of stacking with it, so per-class exceptions are possible. byRpgClass = 'who may become this RPG class': requireVanilla / denyVanilla (vanilla class codes) and requireRace / denyRace (race codes); an empty require list means no requirement. allowWhenVanillaUnknown lets a player who has not picked a vanilla class yet take any RPG class.",
            _comment_race = "A race is a PlayerModelLib model code, written as <mod domain>:<model> exactly as that mod registers it (e.g. racialequality:ork - the domain is the asset folder, the name is the key inside config/customplayermodels/*.json, so 'ork', not 'ork-char'); 'seraph' is the default human model, and is what everyone counts as when no race mod is installed. Check a server log line from PlayerModelLib for the codes your pack actually uses. A player shapeshifted by this mod (druid cat, Ghost Wolf, polymorph) still counts as their real race.",
            _comment_example = "Everything below is a disabled EXAMPLE - edit or delete it freely. The race rules are written for the racialequality pack (racialequality:human, :elf, :dwarf, :ork, :goblin); with a different race mod the codes will not match and every race falls through to '*'. Humans and the default seraph are deliberately absent - they fall through to '*' and may take anything. The vanilla-class axis is switched off and carries one entry just to show the syntax.",
            enabled = false,
            vanillaClassRulesEnabled = false,
            raceRulesEnabled = true,
            allowWhenVanillaUnknown = true,
            byVanillaClass = new Dictionary<string, VanillaRule>
            {
                [DefaultKey] = new VanillaRule { allow = new List<string>(), deny = new List<string>() },
                ["blackguard"] = new VanillaRule { allow = new List<string>(), deny = new List<string> { "priest", "paladin" } }
            },
            // Paladin (human/dwarf) and druid (human/elf) are the deliberately rare picks; the rest sit with three
            // or more races, so no class is left with a single home.
            byRace = new Dictionary<string, VanillaRule>
            {
                [DefaultKey] = new VanillaRule { allow = new List<string>(), deny = new List<string>() },
                ["racialequality:elf"] = new VanillaRule { allow = new List<string> { "druid", "hunter", "mage", "priest", "rogue" }, deny = new List<string>() },
                ["racialequality:dwarf"] = new VanillaRule { allow = new List<string> { "warrior", "paladin", "priest", "hunter", "rogue" }, deny = new List<string>() },
                ["racialequality:ork"] = new VanillaRule { allow = new List<string> { "warrior", "shaman", "hunter", "rogue" }, deny = new List<string>() },
                ["racialequality:goblin"] = new VanillaRule { allow = new List<string> { "rogue", "mage", "shaman", "hunter" }, deny = new List<string>() }
            },
            byRpgClass = new Dictionary<string, RpgRule>()
        };

        // JSON shape of the config file and of the network payload (the client only ever reads it back).
        private class Model
        {
            public string? _comment { get; set; }
            public string? _comment_rules { get; set; }
            public string? _comment_race { get; set; }
            public string? _comment_example { get; set; }
            public bool enabled { get; set; }
            public bool vanillaClassRulesEnabled { get; set; } = true;
            public bool raceRulesEnabled { get; set; } = true;
            public bool allowWhenVanillaUnknown { get; set; } = true;
            public Dictionary<string, VanillaRule>? byVanillaClass { get; set; }
            public Dictionary<string, VanillaRule>? byRace { get; set; }
            public Dictionary<string, RpgRule>? byRpgClass { get; set; }
        }

        // Same allow/deny shape for both "what may this vanilla class become" and "what may this race become".
        private class VanillaRule
        {
            public List<string>? allow { get; set; }
            public List<string>? deny { get; set; }
        }

        private class RpgRule
        {
            public List<string>? requireVanilla { get; set; }
            public List<string>? denyVanilla { get; set; }
            public List<string>? requireRace { get; set; }
            public List<string>? denyRace { get; set; }
        }
    }
}
