using System;
using System.Collections.Generic;
using Vintagestory.API.Client;

namespace canrpgclasses.Client
{
    /// <summary>How the player drives the spell hotbar. Global, not per class.</summary>
    public enum InputMode
    {
        /// <summary>Cast keys only, on their own bindings (default Z R F V ...). The vanilla item hotbar keeps 1-9.</summary>
        Keys = 0,

        /// <summary>Cast keys may sit on 1-9: our hotkeys are registered ahead of vanilla's slot keys.</summary>
        NumberRow = 1,

        /// <summary>Slots are clicked; needs a free cursor (cursor hotkey, Alt, or an open window).</summary>
        Mouse = 2
    }

    /// <summary>
    /// Per-class HUD layout. Anchors are 0..1 fractions of the screen and mark the centre of each block.
    /// </summary>
    public class HudLayoutConfig
    {
        // ---- pre-bars slot placement: read once by the hotbar migration, then unused ----
        public float SlotAnchorX = 0.5f;
        public float SlotAnchorY = 0.88f;
        public float SlotSize = 48f;
        public float SlotPadding = 6f;
        public bool Vertical = false;

        // ---- resources (class pool bar + combo pips) ----
        public bool ShowResources = true;
        public float ResAnchorX = 0.5f;
        public float ResAnchorY = 0.82f;
        public float ResWidth = 240f;    // length of the bar (horizontal: width, vertical: height)
        public float ResThickness = 16f; // thickness of the bar (the short side)
        public bool ResVertical = false; // false = horizontal bar, true = vertical bar (fills bottom-up)

        // ---- channeled-cast bar ----
        public float CastAnchorX = 0.5f;
        public float CastAnchorY = 0.62f;
        public float CastWidth = 240f;     // length of the bar (horizontal: width, vertical: height)
        public float CastThickness = 18f;  // thickness of the bar (the short side)
        public bool CastVertical = false;  // false = horizontal bar, true = vertical bar (fills bottom-up)

        // ---- combo points ----
        public bool ShowCombo = true;
        public float ComboAnchorX = 0.5f;
        public float ComboAnchorY = 0.775f; // just above the default resource-bar row
        public float ComboPipRadius = 7f;
        public float ComboPipSpacing = 22f;
        public bool ComboVertical = false;  // false = row, true = column

        public HudLayoutConfig Clone() => (HudLayoutConfig)MemberwiseClone();
    }

    /// <summary>Client-side HUD layout store, keyed by class id.</summary>
    public class HudLayout
    {
        private const string ConfigFile = "canrpgclasses-hud.json";
        private readonly ICoreClientAPI capi;
        private HudLayoutFile file = new();

        /// <summary>Set by the settings panel while open; the HUD then shows drag handles.</summary>
        public bool EditMode;

        public HudLayout(ICoreClientAPI capi)
        {
            this.capi = capi;
            try { file = capi.LoadModConfig<HudLayoutFile>(ConfigFile) ?? new HudLayoutFile(); }
            catch { file = new HudLayoutFile(); }
            file.PerClass ??= new Dictionary<string, HudLayoutConfig>();
            file.KeyProfiles ??= new Dictionary<string, List<KeyBindingSnapshot>>();
        }

        /// <summary>The layout for a class id, creating a default entry on first access.</summary>
        public HudLayoutConfig For(string? classId)
        {
            string key = classId ?? "";
            if (!file.PerClass.TryGetValue(key, out var cfg) || cfg == null)
            {
                cfg = new HudLayoutConfig();
                file.PerClass[key] = cfg;
            }
            return cfg;
        }

        /// <summary>How the hotbar is driven. Global, not per class.</summary>
        public InputMode Mode
        {
            get => file.Mode;
            set { file.Mode = value; Save(); }
        }

        /// <summary>Dim slots the player can't afford or that are on cooldown.</summary>
        public bool DimUnavailable
        {
            get => file.DimUnavailable;
            set { file.DimUnavailable = value; Save(); }
        }

        /// <summary>Skill sound volume, 0..1 on top of the game's own sound volume. 0 = off.</summary>
        public float SkillSoundVolume
        {
            get => Math.Clamp(file.SkillSoundVolume, 0f, 1f);
            set { file.SkillSoundVolume = value; Save(); }
        }

        /// <summary>Legacy slot placement for the hotbar's one-time migration; null if never configured.</summary>
        public Bar? LegacyBarPlacement(string? classId)
        {
            if (!file.PerClass.TryGetValue(classId ?? "", out var cfg) || cfg == null) return null;
            return new Bar
            {
                AnchorX = cfg.SlotAnchorX,
                AnchorY = cfg.SlotAnchorY,
                SlotSize = cfg.SlotSize,
                SlotPadding = cfg.SlotPadding,
                Vertical = cfg.Vertical
            };
        }

        /// <summary>The whole HUD config, for profile export/import.</summary>
        public HudLayoutFile File => file;

        public void ReplaceFile(HudLayoutFile replacement)
        {
            file = replacement ?? new HudLayoutFile();
            file.PerClass ??= new Dictionary<string, HudLayoutConfig>();
            file.KeyProfiles ??= new Dictionary<string, List<KeyBindingSnapshot>>();
            Save();
        }

        /// <summary>The key mapping remembered for a mode, or null if that mode was never left.</summary>
        public List<KeyBindingSnapshot>? Profile(InputMode mode)
            => file.KeyProfiles.TryGetValue(mode.ToString(), out var p) && p is { Count: > 0 } ? p : null;

        public void StoreProfile(InputMode mode, List<KeyBindingSnapshot> bindings)
        {
            file.KeyProfiles[mode.ToString()] = bindings;
            Save();
        }

        public void Save() { try { capi.StoreModConfig(file, ConfigFile); } catch { } }

        public void Reset(string? classId) { file.PerClass[classId ?? ""] = new HudLayoutConfig(); Save(); }
    }

    public class HudLayoutFile
    {
        public Dictionary<string, HudLayoutConfig> PerClass = new();
        public InputMode Mode = InputMode.Keys;
        public bool DimUnavailable = true;
        public float SkillSoundVolume = 1f;

        /// <summary>Cast-key mapping per input mode, so switching modes and back restores what the player had.</summary>
        public Dictionary<string, List<KeyBindingSnapshot>> KeyProfiles = new();
    }

    public class KeyBindingSnapshot
    {
        public string Code = "";
        public int KeyCode;
        public bool Shift;
        public bool Ctrl;
        public bool Alt;
    }
}
