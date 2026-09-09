using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Items;
using canrpgclasses.Core.Net;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Client-side spell hotbar state: the player's skill bars (where they sit and what is on them) and turning a
    /// press or click into a <see cref="SpellRequestPacket"/>. Slots left empty are auto-filled from the spells
    /// the player can currently use.
    /// </summary>
    public class SpellHotbar
    {
        /// <summary>Slots per bar, and bars per set. Both are hard caps: the hotkeys for every (bar, slot) pair
        /// are registered once at startup, so the pool has to be known up front.</summary>
        public const int MaxSlots = 9;
        public const int MaxBars = 3;

        /// <summary>Hotkey code per bar and slot. Bar 1 keeps the original codes so existing rebinds survive.</summary>
        public static readonly string[][] HotkeyCodes = BuildHotkeyCodes();

        private static string[][] BuildHotkeyCodes()
        {
            var codes = new string[MaxBars][];
            for (int bar = 0; bar < MaxBars; bar++)
            {
                codes[bar] = new string[MaxSlots];
                for (int slot = 0; slot < MaxSlots; slot++)
                    codes[bar][slot] = bar == 0
                        ? "canrpgspell" + (slot + 1)
                        : "canrpgbar" + (bar + 1) + "spell" + (slot + 1);
            }
            return codes;
        }

        /// <summary>Separates the entries of a sequence binding: one key, first castable entry wins.</summary>
        public const char SequenceSeparator = ';';

        private const string ConfigFile = "canrpgclasses-hotbar.json";

        private readonly ICoreClientAPI capi;
        private readonly HudLayout layout;
        private HotbarConfig config = new HotbarConfig();

        // Slot contents depend only on class, talent-granted spells and the active set's bars - none of which
        // change per frame. Rebuild only when a cheap signature of those changes, or when a local edit sets dirty.
        private readonly HashSet<string> usedBuf = new();
        private int lastSig;
        private bool dirty = true;
        private double lastCheckMs;
        private const double CheckIntervalMs = 200;

        // Aura the last set-swap reacted to, and whether any slot holds a sequence (skips the per-tick resolve).
        private string lastAura = "";
        private bool hasSequences;

        public SpellHotbar(ICoreClientAPI capi, HudLayout layout)
        {
            this.capi = capi;
            this.layout = layout;
            LoadConfig();
        }

        // ---- per-class loadout sets ----

        private string CurrentClassId()
        {
            var e = capi.World?.Player?.Entity;
            return e != null ? TalentState.CurrentClass(e) : "";
        }

        private ClassSets ClassEntry()
        {
            string cls = CurrentClassId();
            if (!config.PerClass.TryGetValue(cls, out var cs) || cs == null) { cs = new ClassSets(); config.PerClass[cls] = cs; }
            if (cs.Sets.Count == 0) cs.Sets.Add(NewSet("Set 1"));
            if (cs.ActiveSet < 0 || cs.ActiveSet >= cs.Sets.Count) cs.ActiveSet = 0;
            return cs;
        }

        private SkillSet Active() { var cs = ClassEntry(); return cs.Sets[cs.ActiveSet]; }

        public IReadOnlyList<SkillSet> Sets => ClassEntry().Sets;
        public int ActiveSetIndex => ClassEntry().ActiveSet;

        public void SwitchSet(int i) { var cs = ClassEntry(); if (i >= 0 && i < cs.Sets.Count) { cs.ActiveSet = i; cs.BaseSet = i; SaveConfig(); dirty = true; } }

        /// <summary>Cycles to the next set of the current class (wraps).</summary>
        public void NextSet(int delta = 1)
        {
            var cs = ClassEntry();
            if (cs.Sets.Count <= 1) return;
            int n = cs.Sets.Count;
            SwitchSet(((cs.ActiveSet + delta) % n + n) % n);
        }

        /// <summary>A new set copies the current bar geometry, so adding one does not mean placing bars again.</summary>
        public void AddSet()
        {
            var cs = ClassEntry();
            var fresh = new SkillSet { Name = "Set " + (cs.Sets.Count + 1) };
            foreach (var bar in Active().Bars) fresh.Bars.Add(bar.CloneGeometry());
            if (fresh.Bars.Count == 0) fresh.Bars.Add(new Bar());

            cs.Sets.Add(fresh);
            cs.ActiveSet = cs.Sets.Count - 1;
            cs.BaseSet = cs.ActiveSet;
            SaveConfig();
            dirty = true;
        }

        public void RenameSet(int i, string name)
        {
            var cs = ClassEntry();
            if (i >= 0 && i < cs.Sets.Count && !string.IsNullOrWhiteSpace(name)) { cs.Sets[i].Name = name; SaveConfig(); }
        }

        public void DeleteSet(int i)
        {
            var cs = ClassEntry();
            if (cs.Sets.Count <= 1 || i < 0 || i >= cs.Sets.Count) return;
            cs.Sets.RemoveAt(i);
            if (cs.ActiveSet >= cs.Sets.Count) cs.ActiveSet = cs.Sets.Count - 1;
            if (cs.BaseSet >= cs.Sets.Count) cs.BaseSet = cs.ActiveSet;
            SaveConfig();
            dirty = true;
        }

        /// <summary>Pushes this set's bar placement onto every other set of the class - bar positions belong to a
        /// set, and this is how a player keeps them identical everywhere.</summary>
        public void CopyLayoutToOtherSets()
        {
            var cs = ClassEntry();
            var source = Active().Bars;
            foreach (var set in cs.Sets)
            {
                if (set == Active()) continue;
                while (set.Bars.Count > source.Count) set.Bars.RemoveAt(set.Bars.Count - 1);
                for (int i = 0; i < source.Count; i++)
                {
                    if (i >= set.Bars.Count) { set.Bars.Add(source[i].CloneGeometry()); continue; }
                    set.Bars[i].CopyGeometryFrom(source[i]);
                }
            }
            SaveConfig();
            dirty = true;
        }

        /// <summary>Which aura/form this set auto-activates in ("" = none).</summary>
        public string SetAura(int i) { var cs = ClassEntry(); return i >= 0 && i < cs.Sets.Count ? cs.Sets[i].AuraId ?? "" : ""; }

        public void SetSetAura(int i, string? auraId)
        {
            var cs = ClassEntry();
            if (i < 0 || i >= cs.Sets.Count) return;
            cs.Sets[i].AuraId = string.IsNullOrEmpty(auraId) ? null : auraId;
            SaveConfig();
        }

        /// <summary>Swaps to the set tied to the aura the player just entered, and back to the last manually
        /// chosen set when it ends. Not persisted - the manual choice stays the saved one.</summary>
        private void ApplyAuraSet(Entity player)
        {
            var cs = ClassEntry();
            string aura = player.WatchedAttributes.GetString(Core.AttrKeys.ActiveAura, "") ?? "";
            if (aura == lastAura) return;
            lastAura = aura;

            int target = -1;
            if (aura.Length > 0)
                for (int i = 0; i < cs.Sets.Count; i++)
                    if (cs.Sets[i].AuraId == aura) { target = i; break; }

            if (target < 0) target = Math.Clamp(cs.BaseSet, 0, cs.Sets.Count - 1);
            if (target == cs.ActiveSet) return;
            cs.ActiveSet = target;
            dirty = true;
        }

        // ---- bars ----

        public IReadOnlyList<Bar> Bars => Active().Bars;
        public int BarCount => Active().Bars.Count;

        public Bar? BarAt(int bar)
        {
            var bars = Active().Bars;
            return bar >= 0 && bar < bars.Count ? bars[bar] : null;
        }

        public int SlotCount(int bar) => BarAt(bar)?.Slots.Length ?? 0;

        public string? Binding(int bar, int slot)
        {
            var b = BarAt(bar);
            return b != null && slot >= 0 && slot < b.Slots.Length ? b.Slots[slot] : null;
        }

        /// <summary>What the slot shows and casts right now: its binding, a sequence's current pick, or an
        /// auto-filled spell.</summary>
        public string? Resolved(int bar, int slot)
        {
            var b = BarAt(bar);
            return b != null && slot >= 0 && slot < b.Resolved.Length ? b.Resolved[slot] : null;
        }

        public Spell? SpellAt(int bar, int slot)
        {
            var id = Resolved(bar, slot);
            var mod = canrpgclassesModSystem.ClientInstance;
            if (string.IsNullOrEmpty(id) || mod == null) return null;
            return mod.Spells.TryGet(id!, out var spell) ? spell : null;
        }

        /// <summary>Adds a bar, placed a row above the last one so it does not land on top of it.</summary>
        public int AddBar()
        {
            var bars = Active().Bars;
            if (bars.Count >= MaxBars) return -1;

            var fresh = bars.Count > 0 ? bars[bars.Count - 1].CloneGeometry() : new Bar();
            fresh.AnchorY = Math.Clamp(fresh.AnchorY - 0.07f, 0f, 1f);
            bars.Add(fresh);
            SaveConfig();
            dirty = true;
            return bars.Count - 1;
        }

        public void RemoveBar(int bar)
        {
            var bars = Active().Bars;
            if (bars.Count <= 1 || bar < 0 || bar >= bars.Count) return;
            bars.RemoveAt(bar);
            SaveConfig();
            dirty = true;
        }

        /// <summary>Persists bar geometry after an on-screen drag.</summary>
        public void SaveBars() => SaveConfig();

        public void SetSlotCount(int bar, int count)
        {
            var b = BarAt(bar);
            if (b == null) return;
            b.Resize(Math.Clamp(count, 1, MaxSlots));
            SaveConfig();
            dirty = true;
        }

        /// <summary>Assigns a spell (or null to clear) to a slot and persists the choice.</summary>
        public void SetBinding(int bar, int slot, string? spellId)
        {
            var b = BarAt(bar);
            if (b == null || slot < 0 || slot >= b.Slots.Length) return;
            b.Slots[slot] = string.IsNullOrEmpty(spellId) ? null : spellId;
            SaveConfig();
            dirty = true;
        }

        /// <summary>Appends a spell to a slot's sequence (or binds it when the slot is empty).</summary>
        public void AddToSequence(int bar, int slot, string spellId)
        {
            string? cur = Binding(bar, slot);
            if (string.IsNullOrEmpty(spellId)) return;
            if (string.IsNullOrEmpty(cur)) { SetBinding(bar, slot, spellId); return; }

            foreach (var id in cur!.Split(SequenceSeparator, StringSplitOptions.RemoveEmptyEntries))
                if (id == spellId) return;

            SetBinding(bar, slot, cur + SequenceSeparator + spellId);
        }

        /// <summary>The entries of a slot's binding: one id for a plain slot, several for a sequence.</summary>
        public string[] SequenceAt(int bar, int slot)
        {
            string? binding = Binding(bar, slot);
            return string.IsNullOrEmpty(binding)
                ? Array.Empty<string>()
                : binding!.Split(SequenceSeparator, StringSplitOptions.RemoveEmptyEntries);
        }

        // ---- refresh ----

        /// <summary>Recomputes what each slot shows. Called every frame from the HUD, but only rebuilds when the
        /// inputs actually change.</summary>
        public void RefreshSlots()
        {
            var player = capi.World?.Player?.Entity;
            if (player == null) return;

            double now = capi.InWorldEllapsedMilliseconds;
            if (!dirty && now - lastCheckMs < CheckIntervalMs) return;
            lastCheckMs = now;

            ApplyAuraSet(player);

            int sig = ComputeSig(player);
            if (dirty || sig != lastSig)
            {
                dirty = false;
                lastSig = sig;
                Rebuild();
            }

            // Cooldowns move without any of the above changing, so a sequence slot re-picks on its own tick.
            if (hasSequences) ResolveSequences(player);
        }

        private void Rebuild()
        {
            var available = AvailableSpells();
            var used = usedBuf;
            used.Clear();
            hasSequences = false;

            var bars = Active().Bars;
            foreach (var bar in bars) bar.SyncResolved();

            // 1) Explicit bindings first, across every bar. A sequence keeps only the entries the player can use.
            foreach (var bar in bars)
            {
                for (int i = 0; i < bar.Slots.Length; i++)
                {
                    string? binding = bar.Slots[i];
                    bar.Resolved[i] = null;
                    if (string.IsNullOrEmpty(binding)) continue;

                    if (binding!.IndexOf(SequenceSeparator) >= 0)
                    {
                        var kept = new List<string>();
                        foreach (var id in binding.Split(SequenceSeparator, StringSplitOptions.RemoveEmptyEntries))
                            if (available.Contains(id) && !kept.Contains(id)) kept.Add(id);
                        if (kept.Count == 0) continue;

                        bar.Slots[i] = string.Join(SequenceSeparator, kept);
                        bar.Resolved[i] = kept[0];
                        hasSequences = true;
                        foreach (var id in kept) used.Add(id);
                    }
                    else if (available.Contains(binding))
                    {
                        bar.Resolved[i] = binding;
                        used.Add(binding);
                    }
                }
            }

            // 2) Auto-fill what is left, first bar first.
            int next = 0;
            foreach (var bar in bars)
            {
                for (int i = 0; i < bar.Slots.Length; i++)
                {
                    if (bar.Resolved[i] != null) continue;
                    while (next < available.Count && used.Contains(available[next])) next++;
                    if (next >= available.Count) return;
                    bar.Resolved[i] = available[next];
                    used.Add(available[next]);
                    next++;
                }
            }
        }

        /// <summary>Points every sequence slot at its first castable entry, falling back to the first one.</summary>
        private void ResolveSequences(EntityPlayer player)
        {
            foreach (var bar in Active().Bars)
            {
                bar.SyncResolved();
                for (int i = 0; i < bar.Slots.Length; i++)
                {
                    string? binding = bar.Slots[i];
                    if (string.IsNullOrEmpty(binding) || binding!.IndexOf(SequenceSeparator) < 0) continue;

                    var parts = binding.Split(SequenceSeparator, StringSplitOptions.RemoveEmptyEntries);
                    string? pick = null;
                    foreach (var id in parts)
                    {
                        if (!IsCastable(player, id)) continue;
                        pick = id;
                        break;
                    }
                    bar.Resolved[i] = pick ?? (parts.Length > 0 ? parts[0] : null);
                }
            }
        }

        /// <summary>Client-side "can I press this now": off cooldown (its own and the GCD) and enough resource.
        /// Advisory only - the server still decides.</summary>
        public bool IsCastable(EntityPlayer player, string? spellId)
        {
            var mod = canrpgclassesModSystem.ClientInstance;
            if (string.IsNullOrEmpty(spellId) || mod == null || !mod.Spells.TryGet(spellId!, out var spell) || spell == null)
                return false;

            var cooldowns = player.GetBehavior<Core.EB.EBSpellCooldowns>();
            if (cooldowns != null)
            {
                if (cooldowns.RemainingSeconds(spell.CooldownKey) > 0) return false;
                if (spell.TriggersGlobalCooldown && cooldowns.RemainingSeconds(Spell.GlobalCooldownKey) > 0) return false;
            }

            if (spell.Cost.Resource > 0)
            {
                var pool = Core.Resources.ResourceState.PrimaryPool(player);
                if (pool != null && Core.Resources.ResourceState.Get(player, pool) < spell.Cost.Resource) return false;
            }
            return true;
        }

        // A cheap fingerprint of everything the fill depends on: class, talent ranks, and the active set's bars.
        private int ComputeSig(Entity player)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (TalentState.CurrentClass(player)?.GetHashCode() ?? 0);

                var ranks = player.WatchedAttributes.GetTreeAttribute(TalentState.RanksKey);
                if (ranks != null)
                    foreach (var kv in ranks)
                        h = h * 31 + kv.Key.GetHashCode() + ranks.GetInt(kv.Key) * 7;

                foreach (var bar in Active().Bars)
                {
                    h = h * 31 + bar.Slots.Length;
                    foreach (var id in bar.Slots) h = h * 31 + (id?.GetHashCode() ?? 0);
                }
                return h;
            }
        }

        /// <summary>Spells the player can use right now: class base + talent-granted + the held item's bound spell.
        /// Same rule the server enforces - see <see cref="canrpgclasses.Core.Spells.SpellAccess"/>.</summary>
        public List<string> AvailableSpells()
        {
            var player = capi.World?.Player?.Entity;
            var mod = canrpgclassesModSystem.ClientInstance;
            var list = SpellAccess.Available(player, mod);

            // Debug fallback: nothing unlocked yet - list the registry's active spells so the hotbar and
            // spellbook aren't empty while testing.
            if (list.Count == 0 && mod != null)
            {
                foreach (var spell in mod.Spells.All.Values)
                    if (spell.Type == SpellType.Active && !list.Contains(spell.Id)) list.Add(spell.Id);
            }

            return list;
        }

        // ---- casting ----

        public void CastSlot(int bar, int slot)
        {
            var b = BarAt(bar);
            if (b == null || slot < 0 || slot >= b.Slots.Length) return;

            // Re-pick now: the buffered choice can be a tick old, and that tick is where a just-finished
            // cooldown would be missed.
            var player = capi.World?.Player?.Entity;
            string? binding = b.Slots[slot];
            if (player != null && !string.IsNullOrEmpty(binding) && binding!.IndexOf(SequenceSeparator) >= 0)
                ResolveSequences(player);

            var id = Resolved(bar, slot);
            if (!string.IsNullOrEmpty(id)) Cast(id!);
        }

        /// <summary>Sends a cast request for an arbitrary spell id, aimed at whatever the player is looking at.
        /// Shared by the hotbar keys, the click handler and the right-click item-cast patch.</summary>
        public void Cast(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            var mod = canrpgclassesModSystem.ClientInstance;
            if (mod?.ClientChannel == null) return;

            var packet = new SpellRequestPacket
            {
                Action = (int)SpellRequestAction.Cast,
                SpellId = id
            };

            var player = capi.World?.Player;
            var es = player?.CurrentEntitySelection;
            if (es?.Entity != null) packet.TargetEntityId = es.Entity.EntityId;

            var bs = player?.CurrentBlockSelection;
            if (bs != null)
            {
                packet.HasAimPos = true;
                packet.AimX = bs.FullPosition.X;
                packet.AimY = bs.FullPosition.Y;
                packet.AimZ = bs.FullPosition.Z;
            }
            else if (es?.Entity != null)
            {
                var p = es.Entity.Pos;
                packet.HasAimPos = true;
                packet.AimX = p.X;
                packet.AimY = p.Y;
                packet.AimZ = p.Z;
            }

            mod.ClientChannel.SendPacket(packet);
        }

        // ---- persistence ----

        private static SkillSet NewSet(string name)
        {
            var set = new SkillSet { Name = name };
            set.Bars.Add(new Bar());
            return set;
        }

        private void LoadConfig()
        {
            try
            {
                var cfg = capi.LoadModConfig<HotbarConfig>(ConfigFile);
                if (cfg != null) { config = cfg; config.PerClass ??= new Dictionary<string, ClassSets>(); }
            }
            catch { }
            Normalize();
        }

        /// <summary>Repairs deserialized data and migrates the pre-bars format: the old single slot row becomes
        /// bar 1, taking its placement from the HUD layout's legacy fields.</summary>
        private void Normalize()
        {
            foreach (var kv in config.PerClass)
            {
                var cs = kv.Value;
                if (cs == null) continue;

                cs.Sets ??= new List<SkillSet>();
                if (cs.Sets.Count == 0) cs.Sets.Add(NewSet("Set 1"));

                foreach (var set in cs.Sets)
                {
                    set.Bars ??= new List<Bar>();

                    if (set.Bars.Count == 0)
                    {
                        var bar = new Bar();
                        int count = Math.Clamp(set.SlotCount > 0 ? set.SlotCount : 4, 1, MaxSlots);
                        bar.Resize(count);
                        for (int i = 0; i < count && set.Bindings != null && i < set.Bindings.Length; i++)
                            bar.Slots[i] = set.Bindings[i];

                        var legacy = layout.LegacyBarPlacement(kv.Key);
                        if (legacy != null) bar.CopyGeometryFrom(legacy);

                        set.Bars.Add(bar);
                        set.Bindings = null;
                        set.SlotCount = 0;
                    }

                    if (set.Bars.Count > MaxBars) set.Bars.RemoveRange(MaxBars, set.Bars.Count - MaxBars);
                    foreach (var bar in set.Bars) bar.Normalize();
                }

                if (cs.ActiveSet < 0 || cs.ActiveSet >= cs.Sets.Count) cs.ActiveSet = 0;
                if (cs.BaseSet < 0 || cs.BaseSet >= cs.Sets.Count) cs.BaseSet = cs.ActiveSet;
            }
        }

        private void SaveConfig()
        {
            try { capi.StoreModConfig(config, ConfigFile); } catch { }
        }

        /// <summary>The whole hotbar config, for profile export/import.</summary>
        public HotbarConfig Config => config;

        public void ReplaceConfig(HotbarConfig replacement)
        {
            config = replacement ?? new HotbarConfig();
            config.PerClass ??= new Dictionary<string, ClassSets>();
            Normalize();
            SaveConfig();
            dirty = true;
        }
    }

    /// <summary>One skill bar: where it is drawn and what is on it. Geometry and contents live together so a bar
    /// is a single thing to move, copy and export.</summary>
    public class Bar
    {
        public float AnchorX = 0.5f;   // centre X (0 = left, 1 = right)
        public float AnchorY = 0.88f;  // centre Y (0 = top, 1 = bottom)
        public float SlotSize = 48f;
        public float SlotPadding = 6f;
        public bool Vertical = false;  // false = row, true = column
        public bool Visible = true;

        /// <summary>Shown only as a wheel while its hold key is down, never as a row on screen.</summary>
        public bool Radial = false;

        /// <summary>Player-chosen spell per slot (null = auto-filled). Its length is the bar's slot count.</summary>
        public string?[] Slots = new string?[4];

        /// <summary>What each slot shows this frame - bindings resolved against sequences and auto-fill.</summary>
        [JsonIgnore]
        public string?[] Resolved = new string?[4];

        public void Resize(int count)
        {
            var slots = new string?[count];
            for (int i = 0; i < count && i < Slots.Length; i++) slots[i] = Slots[i];
            Slots = slots;
            Resolved = new string?[count];
        }

        /// <summary>Keeps the render buffer the same length as the bindings.</summary>
        public void SyncResolved()
        {
            if (Resolved.Length != Slots.Length) Resolved = new string?[Slots.Length];
        }

        public void Normalize()
        {
            Slots ??= new string?[4];
            if (Slots.Length < 1 || Slots.Length > SpellHotbar.MaxSlots)
                Resize(Math.Clamp(Slots.Length, 1, SpellHotbar.MaxSlots));
            SlotSize = Math.Clamp(SlotSize, 24f, 96f);
            SlotPadding = Math.Clamp(SlotPadding, 0f, 24f);
            AnchorX = Math.Clamp(AnchorX, 0f, 1f);
            AnchorY = Math.Clamp(AnchorY, 0f, 1f);
            SyncResolved();
        }

        public Bar CloneGeometry()
        {
            var bar = new Bar();
            bar.CopyGeometryFrom(this);
            bar.Resize(Slots.Length);
            return bar;
        }

        public void CopyGeometryFrom(Bar other)
        {
            AnchorX = other.AnchorX;
            AnchorY = other.AnchorY;
            SlotSize = other.SlotSize;
            SlotPadding = other.SlotPadding;
            Vertical = other.Vertical;
            Visible = other.Visible;
            Radial = other.Radial;
        }
    }

    public class HotbarConfig
    {
        public Dictionary<string, ClassSets> PerClass = new();
    }

    public class ClassSets
    {
        public List<SkillSet> Sets = new();
        public int ActiveSet = 0;

        /// <summary>The set to come back to when an aura-tied set ends.</summary>
        public int BaseSet = 0;
    }

    public class SkillSet
    {
        public string Name = "Set 1";

        /// <summary>Aura/form spell id this set auto-activates in (null = manual only).</summary>
        public string? AuraId;

        public List<Bar> Bars = new();

        // ---- pre-bars format, read once by the migration then cleared ----
        public int SlotCount;
        public string?[]? Bindings;
    }
}
