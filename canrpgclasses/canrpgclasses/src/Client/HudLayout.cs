using System.Collections.Generic;
using Vintagestory.API.Client;

namespace canrpgclasses.Client
{
    /// <summary>How the player drives the spell hotbar. One global choice (not per class) - it decides which input
    /// the mod claims, so mixing it per class would only confuse.</summary>
    public enum InputMode
    {
        /// <summary>Cast keys only, on their own bindings (default Z R F V ...). The vanilla item hotbar keeps 1-9.</summary>
        Keys = 0,

        /// <summary>Cast keys, and they are allowed to sit on 1-9: the mod's hotkeys are registered ahead of the
        /// vanilla item-slot ones, so a slot bound to "1" casts instead of switching the held item.</summary>
        NumberRow = 1,

        /// <summary>Mouse: cast keys stay silent and slots are clicked instead. Needs a free cursor - the mod's
        /// cursor hotkey, vanilla's Alt, or any open window.</summary>
        Mouse = 2
    }

    /// <summary>
    /// Player-configurable layout for the spell-hotbar HUD: where and how the skill slots and the resource bar
    /// are drawn. Anchors are fractions (0..1) of the screen work area and mark the CENTRE of each block, so the
    /// player can place them anywhere. Persisted client-side, PER CLASS - see <see cref="HudLayout"/>.
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

        // ---- combo points (independent of the resource bar's own position/orientation) ----
        public bool ShowCombo = true;
        public float ComboAnchorX = 0.5f;
        public float ComboAnchorY = 0.775f; // just above the default resource-bar row
        public float ComboPipRadius = 7f;
        public float ComboPipSpacing = 22f;
        public bool ComboVertical = false;  // false = row (left→right), true = column (top→bottom)

        public HudLayoutConfig Clone() => (HudLayoutConfig)MemberwiseClone();
    }

    /// <summary>Loads/holds/saves the HUD layout, keyed by class id so each class can place its hotbar/resources
    /// differently. One instance, shared by the HUD renderer and the settings panel. The active config is whichever
    /// class the player currently is.</summary>
    public class HudLayout
    {
        private const string ConfigFile = "canrpgclasses-hud.json";
        private readonly ICoreClientAPI capi;
        private HudLayoutFile file = new();

        /// <summary>True while the HUD settings panel is open: the HUD then shows drag handles around its blocks.
        /// The panel owns the flag (raises it on open, drops it on close) and the HUD only reads it.</summary>
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

        /// <summary>The pre-bars placement saved for a class, for the hotbar's one-time migration. Null when that
        /// class was never configured, in which case the bar keeps its own defaults.</summary>
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
