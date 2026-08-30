using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Items;
using canrpgclasses.Core.Net;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Client-side spell hotbar state: holds the spell id bound to each slot and turns a hotkey press into a
    /// <see cref="SpellRequestPacket"/> for the server. Slots are auto-filled from the player's available
    /// spells unless bound explicitly.
    /// </summary>
    public class SpellHotbar
    {
        /// <summary>Hard cap on slots (hotkeys are registered once at startup, so we reserve this many).</summary>
        public const int MaxSlots = 9;

        /// <summary>Hotkey codes for each slot (registered in the mod system, rebindable in Controls).</summary>
        public static readonly string[] HotkeyCodes =
        {
            "canrpgspell1", "canrpgspell2", "canrpgspell3", "canrpgspell4",
            "canrpgspell5", "canrpgspell6", "canrpgspell7", "canrpgspell8", "canrpgspell9"
        };

        /// <summary>Computed buffer: the spell shown in each slot this frame (bindings + auto-fill).</summary>
        public readonly string?[] Slots = new string?[MaxSlots];

        private const string ConfigFile = "canrpgclasses-hotbar.json";

        private readonly ICoreClientAPI capi;
        private HotbarConfig config = new HotbarConfig();

        // The slot contents depend only on the class, talent-granted spells, and the active loadout/bindings -
        // none of which change per frame. So rebuild only when a cheap signature of those inputs actually changes
        // (checked ~5x/second), or immediately when a local edit (bind / slot count / set switch) flips dirty.
        // In steady state RefreshSlots does no allocation and no rebuild at all.
        private readonly HashSet<string> usedBuf = new();
        private int lastSig;
        private bool dirty = true;
        private double lastCheckMs;
        private const double CheckIntervalMs = 200;

        public SpellHotbar(ICoreClientAPI capi)
        {
            this.capi = capi;
            LoadConfig();
            AutoFillFromRegistry();
        }

        // ---- per-class loadout sets: each class has its own list of named sets (slot count + bindings) ----

        private string CurrentClassId()
        {
            var e = capi.World?.Player?.Entity;
            return e != null ? TalentState.CurrentClass(e) : "";
        }

        private ClassSets ClassEntry()
        {
            string cls = CurrentClassId();
            if (!config.PerClass.TryGetValue(cls, out var cs) || cs == null) { cs = new ClassSets(); config.PerClass[cls] = cs; }
            if (cs.Sets.Count == 0) cs.Sets.Add(new SkillSet());
            if (cs.ActiveSet < 0 || cs.ActiveSet >= cs.Sets.Count) cs.ActiveSet = 0;
            return cs;
        }

        private SkillSet Active() { var cs = ClassEntry(); return cs.Sets[cs.ActiveSet]; }

        /// <summary>How many slots are active in the current class's active set (1..MaxSlots).</summary>
        public int SlotCount => Active().SlotCount;

        /// <summary>Player-chosen spell per slot (null = auto) for the active set. Indexing writes through.</summary>
        public string?[] Bindings => Active().Bindings;

        // ---- set management (current class), used by the spellbook ----
        public System.Collections.Generic.IReadOnlyList<SkillSet> Sets => ClassEntry().Sets;
        public int ActiveSetIndex => ClassEntry().ActiveSet;

        public void SwitchSet(int i) { var cs = ClassEntry(); if (i >= 0 && i < cs.Sets.Count) { cs.ActiveSet = i; SaveConfig(); dirty = true; } }
        public void AddSet() { var cs = ClassEntry(); cs.Sets.Add(new SkillSet { Name = "Set " + (cs.Sets.Count + 1) }); cs.ActiveSet = cs.Sets.Count - 1; SaveConfig(); dirty = true; }
        public void RenameSet(int i, string name) { var cs = ClassEntry(); if (i >= 0 && i < cs.Sets.Count && !string.IsNullOrWhiteSpace(name)) { cs.Sets[i].Name = name; SaveConfig(); } }
        public void DeleteSet(int i) { var cs = ClassEntry(); if (cs.Sets.Count <= 1 || i < 0 || i >= cs.Sets.Count) return; cs.Sets.RemoveAt(i); if (cs.ActiveSet >= cs.Sets.Count) cs.ActiveSet = cs.Sets.Count - 1; SaveConfig(); dirty = true; }

        /// <summary>
        /// Recomputes the slot contents (explicit bindings + auto-fill from the player's class/talent spells).
        /// Called every frame from the HUD, but only rebuilds when the inputs actually change (see the signature
        /// guard below).
        /// </summary>
        public void RefreshSlots()
        {
            var player = capi.World?.Player?.Entity;
            if (player == null) return;

            double now = capi.InWorldEllapsedMilliseconds;
            if (!dirty && now - lastCheckMs < CheckIntervalMs) return;
            lastCheckMs = now;

            int sig = ComputeSig(player);
            if (!dirty && sig == lastSig) return;
            dirty = false;
            lastSig = sig;

            Rebuild();
        }

        private void Rebuild()
        {
            var available = AvailableSpells();
            if (available.Count == 0) { AutoFillFromRegistry(); return; }

            var used = usedBuf;
            used.Clear();

            // 1) Honour explicit bindings (only those still available).
            for (int i = 0; i < SlotCount; i++)
            {
                if (!string.IsNullOrEmpty(Bindings[i]) && available.Contains(Bindings[i]!))
                {
                    Slots[i] = Bindings[i];
                    used.Add(Bindings[i]!);
                }
                else Slots[i] = null;
            }

            // 2) Auto-fill the remaining slots with leftover available skills.
            int next = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                if (Slots[i] != null) continue;
                while (next < available.Count && used.Contains(available[next])) next++;
                if (next < available.Count) { Slots[i] = available[next]; used.Add(available[next]); next++; }
            }
        }

        // A cheap, allocation-free fingerprint of everything the slot fill depends on: current class, talent ranks,
        // and the active set's slot count + bindings. When it's unchanged there's nothing to rebuild.
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

                var set = Active();
                h = h * 31 + set.SlotCount;
                var b = set.Bindings;
                if (b != null)
                    for (int i = 0; i < b.Length; i++)
                        h = h * 31 + (b[i]?.GetHashCode() ?? 0);
                return h;
            }
        }

        /// <summary>Spells the player can use right now: class base + talent-granted + the held item's bound spell.
        /// Same rule the server enforces - see <see cref="canrpgclasses.Core.Spells.SpellAccess"/>.</summary>
        public List<string> AvailableSpells()
        {
            var player = capi.World?.Player?.Entity;
            var mod = canrpgclassesModSystem.ClientInstance;
            var list = canrpgclasses.Core.Spells.SpellAccess.Available(player, mod);

            // Debug fallback: nothing unlocked yet → list the registry's active spells so the
            // hotbar and spellbook aren't empty while testing.
            if (list.Count == 0 && mod != null)
            {
                foreach (var spell in mod.Spells.All.Values)
                    if (spell.Type == SpellType.Active && !list.Contains(spell.Id)) list.Add(spell.Id);
            }

            return list;
        }

        /// <summary>Assigns a spell (or null to clear) to a hotbar slot and persists the choice.</summary>
        public void SetBinding(int slot, string? spellId)
        {
            if (slot < 0 || slot >= MaxSlots) return;
            Bindings[slot] = string.IsNullOrEmpty(spellId) ? null : spellId;
            SaveConfig();
            dirty = true;
        }

        public void SetSlotCount(int count)
        {
            Active().SlotCount = Math.Max(1, Math.Min(MaxSlots, count));
            SaveConfig();
            dirty = true;
        }

        private void LoadConfig()
        {
            try
            {
                var cfg = capi.LoadModConfig<HotbarConfig>(ConfigFile);
                if (cfg != null) { config = cfg; config.PerClass ??= new System.Collections.Generic.Dictionary<string, ClassSets>(); Normalize(); }
            }
            catch { }
        }

        // Repairs deserialized data: every set needs a full-length Bindings array, a sane SlotCount, and each class
        // at least one set with a valid ActiveSet index.
        private void Normalize()
        {
            foreach (var cs in config.PerClass.Values)
            {
                if (cs == null) continue;
                cs.Sets ??= new System.Collections.Generic.List<SkillSet>();
                if (cs.Sets.Count == 0) cs.Sets.Add(new SkillSet());
                foreach (var s in cs.Sets)
                {
                    if (s.Bindings == null || s.Bindings.Length != MaxSlots)
                    {
                        var b = new string?[MaxSlots];
                        for (int i = 0; s.Bindings != null && i < MaxSlots && i < s.Bindings.Length; i++) b[i] = s.Bindings[i];
                        s.Bindings = b;
                    }
                    s.SlotCount = Math.Max(1, Math.Min(MaxSlots, s.SlotCount));
                }
                if (cs.ActiveSet < 0 || cs.ActiveSet >= cs.Sets.Count) cs.ActiveSet = 0;
            }
        }

        private void SaveConfig()
        {
            try { capi.StoreModConfig(config, ConfigFile); } catch { }
        }

        /// <summary>Debug fallback binding: take the first few Active spells from the registry.</summary>
        public void AutoFillFromRegistry()
        {
            for (int i = 0; i < SlotCount; i++) Slots[i] = null;

            var mod = canrpgclassesModSystem.ClientInstance;
            if (mod == null) return;

            int n = 0;
            foreach (var spell in mod.Spells.All.Values)
            {
                if (n >= SlotCount) break;
                if (spell.Type != SpellType.Active) continue;
                Slots[n++] = spell.Id;
            }
        }

        public void SetSlot(int index, string? spellId)
        {
            if (index < 0 || index >= SlotCount) return;
            Slots[index] = spellId;
        }

        public Spell? SpellAt(int index)
        {
            if (index < 0 || index >= SlotCount) return null;
            var id = Slots[index];
            if (string.IsNullOrEmpty(id)) return null;
            var mod = canrpgclassesModSystem.ClientInstance;
            if (mod == null) return null;
            return mod.Spells.TryGet(id!, out var spell) ? spell : null;
        }

        public void CastSlot(int index)
        {
            var id = (index >= 0 && index < SlotCount) ? Slots[index] : null;
            if (!string.IsNullOrEmpty(id)) Cast(id!);
        }

        /// <summary>Sends a cast request for an arbitrary spell id, aimed at whatever the player is looking at.
        /// Shared by the hotbar keys and the right-click item-cast patch (<see cref="canrpgclasses.Core.HarmonyPatches.SpellItemUsePatches"/>).</summary>
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
    }

    public class HotbarConfig
    {
        public System.Collections.Generic.Dictionary<string, ClassSets> PerClass = new();
    }

    public class ClassSets
    {
        public System.Collections.Generic.List<SkillSet> Sets = new();
        public int ActiveSet = 0;
    }

    public class SkillSet
    {
        public string Name = "Set 1";
        public int SlotCount = 4;
        public string?[] Bindings = new string?[SpellHotbar.MaxSlots];
    }
}
