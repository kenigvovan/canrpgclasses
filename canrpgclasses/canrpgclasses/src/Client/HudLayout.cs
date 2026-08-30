using System.Collections.Generic;
using Vintagestory.API.Client;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Player-configurable layout for the spell-hotbar HUD: where and how the skill slots and the resource bar
    /// are drawn. Anchors are fractions (0..1) of the screen work area and mark the CENTRE of each block, so the
    /// player can place them anywhere. Persisted client-side, PER CLASS - see <see cref="HudLayout"/>.
    /// </summary>
    public class HudLayoutConfig
    {
        // ---- skill slots ----
        public float SlotAnchorX = 0.5f;   // centre X (0 = left, 1 = right)
        public float SlotAnchorY = 0.88f;  // centre Y (0 = top, 1 = bottom)
        public float SlotSize = 48f;
        public float SlotPadding = 6f;
        public bool Vertical = false;      // false = row, true = column

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

        public void Save() { try { capi.StoreModConfig(file, ConfigFile); } catch { } }

        public void Reset(string? classId) { file.PerClass[classId ?? ""] = new HudLayoutConfig(); Save(); }
    }

    public class HudLayoutFile
    {
        public Dictionary<string, HudLayoutConfig> PerClass = new();
    }
}
