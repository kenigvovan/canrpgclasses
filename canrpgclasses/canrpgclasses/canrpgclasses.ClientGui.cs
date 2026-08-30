using canrpgclasses.Client;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace canrpgclasses
{
    /// <summary>Client GUI part of the mod system: the spell hotbar + its hotkeys, the panels
    /// (talents, spellbook, class select, HUD layout, admin panels) behind one generic toggle helper,
    /// and their disposal.</summary>
    public partial class canrpgclassesModSystem
    {
        /// <summary>Client-only: spell hotbar state (slot bindings + cast dispatch).</summary>
        internal SpellHotbar? Hotbar;

        /// <summary>Client-only: the mod's one icon cache. Every panel and HUD renderer draws the same 200-odd
        /// spell/talent/class icons, so they share one loader instead of each keeping its own copy of the cache
        /// (and its own set of GPU textures).</summary>
        internal Client.IconLoader? Icons;

        /// <summary>Client-only: saved talent builds per class. Lives here rather than inside the talent window so
        /// the builds survive the window being closed.</summary>
        internal TalentLoadouts? Loadouts;
        private Client.Render.HudSpellHotbarRenderer? hotbarHud;
        private TalentTreeDialog? talentTree;
        private SpellBookDialog? spellBook;
        private AdminDialog? adminGui;
        private AdminBalanceDialog? adminBalanceGui;
        private TalentTreeEditorDialog? treeEditorGui;
        private ClassEditorDialog? classEditorGui;
        private SpellEditorDialog? spellEditorGui;
        private ClassSelectDialog? classSelectGui;
        private PetPanelDialog? petPanel;
        private HudLayout? hudLayout;
        private HudSettingsDialog? hudSettings;

        /// <summary>Client-only: floating damage numbers + health bars over nearby damaged entities.</summary>
        private Client.Render.CombatOverlayRenderer? combatOverlay;

        /// <summary>Client-only: adds a resource (mana/energy) bar per member to canparty's party-frames HUD.</summary>
        private PartyResourceOverlay? partyResourceOverlay;

        /// <summary>Client-only: snaps the local view yaw on request (shadow_step). See <see cref="ViewYawForcer"/>.</summary>
        private ViewYawForcer? viewYawForcer;

        /// <summary>Client-only: draws a translucent dome around shielded entities. See <see cref="ShieldDomeRenderer"/>.</summary>
        private ShieldDomeRenderer? shieldDomeRenderer;

        /// <summary>Client-only: tints Shadow Guise entities' models dark. See <see cref="ShadowformTint"/>.</summary>
        private ShadowformTint? shadowformTint;

        /// <summary>Client-only: renders state particles locally from the server-synced visuals mask. See <see cref="EffectVisualsEmitter"/>.</summary>
        private EffectVisualsEmitter? effectVisualsEmitter;

        /// <summary>Client-only: plays one-shot skill visuals (chain arcs, heal ribbons, ring waves) from
        /// <see cref="Core.Net.SkillFxPacket"/>s. See <see cref="SkillFxClient"/>.</summary>
        private SkillFxClient? skillFxClient;

        /// <summary>Client-only: draws channel beams (Mind Rend) as a solid ribbon mesh. See <see cref="BeamRenderer"/>.</summary>
        private BeamRenderer? beamRenderer;

        /// <summary>Client-only: draws one-shot lightning bolts as a solid jagged mesh. See <see cref="BoltRenderer"/>.</summary>
        private BoltRenderer? boltRenderer;

        // ---- Client: spell hotbar HUD + hotkeys ----
        private void SetupHotbar(ICoreClientAPI api)
        {
            Hotbar = new SpellHotbar(api);
            hudLayout = new HudLayout(api);
            Icons = new IconLoader(api);
            Loadouts = new TalentLoadouts(api);

            // Default keys: Z R F V for slots 1-4; 5-9 default to less-common keys (rebind in Controls).
            var defaultKeys = new[]
            {
                GlKeys.Z, GlKeys.R, GlKeys.F, GlKeys.V,
                GlKeys.Insert, GlKeys.Home, GlKeys.PageUp, GlKeys.PageDown, GlKeys.End
            };
            for (int i = 0; i < SpellHotbar.HotkeyCodes.Length; i++)
                RegisterCastHotKey(api, SpellHotbar.HotkeyCodes[i], Lang.Get("canrpgclasses:hotkey-cast", i + 1), defaultKeys[i], i);

            // Registers itself with the Ortho render stage; there is nothing to open - it draws whenever the
            // player is in-world.
            hotbarHud = new Client.Render.HudSpellHotbarRenderer(api, Hotbar, hudLayout);

            api.Input.RegisterHotKey("canrpgtalents", Lang.Get("canrpgclasses:hotkey-talents"), GlKeys.K, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("canrpgtalents", _ => ToggleTalents(api));

            api.Input.RegisterHotKey("canrpgspellbook", Lang.Get("canrpgclasses:hotkey-spellbook"), GlKeys.J, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("canrpgspellbook", _ => ToggleSpellBook(api));

            api.Input.RegisterHotKey("canrpgclassselect", Lang.Get("canrpgclasses:hotkey-classselect"), GlKeys.N, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("canrpgclassselect", _ => ToggleClassSelect(api));
            api.ChatCommands.Create("canrpgclassmenu")
                .WithDescription("Open the class selection panel")
                .HandleWith(_ => { ToggleClassSelect(api); return TextCommandResult.Success(); });

            api.Input.RegisterHotKey("canrpghud", Lang.Get("canrpgclasses:hotkey-hud"), GlKeys.H, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("canrpghud", _ => ToggleHudSettings(api));
            api.ChatCommands.Create("canrpghud")
                .WithDescription("Open the HUD layout panel (per-class slot/resource placement)")
                .HandleWith(_ => { ToggleHudSettings(api); return TextCommandResult.Success(); });

            api.Input.RegisterHotKey("canrpgpet", Lang.Get("canrpgclasses:hotkey-pet"), GlKeys.P, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("canrpgpet", _ => TogglePetPanel(api));
            api.ChatCommands.Create("canrpgpetpanel")
                .WithDescription("Open the hunter pet panel (name / health / orders)")
                .HandleWith(_ => { TogglePetPanel(api); return TextCommandResult.Success(); });

            // First-time players (no class chosen yet) get the picker automatically. Driven by the client
            // PlayerJoin event (the same hook vanilla's character-creation dialog uses), not a fixed timer:
            // it fires only once the join has completed and the player entity's synced attributes (incl. the
            // class-chosen flag) are present - a slow connection can no longer pop the picker for a player
            // who already has a class. Unsubscribed in DisposeClientGui.
            api.Event.PlayerJoin += OnClientPlayerJoin;

            // Admin panel - opened with O. controlserver only (client gate; the server re-checks every action).
            api.Input.RegisterHotKey("canrpgadmin", "RpgClasses: admin panel", GlKeys.O, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("canrpgadmin", _ => OpenAdmin(api));

            api.ChatCommands.Create("canrpgadmin")
                .WithDescription("Open the RpgClasses admin panel (give/set XP and level)")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(_ => OpenAdmin(api)
                    ? TextCommandResult.Success()
                    : TextCommandResult.Error("No permission."));

            api.ChatCommands.Create("canrpgspelleditor")
                .WithDescription("Open the spell editor (build a spell from existing impacts)")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(_ => OpenSpellEditor(api)
                    ? TextCommandResult.Success()
                    : TextCommandResult.Error("No permission."));

            api.ChatCommands.Create("canrpgclasseditor")
                .WithDescription("Open the class editor (build a class from existing spells)")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(_ => OpenClassEditor(api)
                    ? TextCommandResult.Success()
                    : TextCommandResult.Error("No permission."));

            api.ChatCommands.Create("canrpgtreeeditor")
                .WithDescription("Open the talent tree editor (build a class tree in-game)")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(_ => OpenTreeEditor(api)
                    ? TextCommandResult.Success()
                    : TextCommandResult.Error("No permission."));
        }

        private static readonly System.Collections.Generic.Dictionary<System.Type, long> _lastToggleMs = new();

        /// <summary>Lazily creates a GUI window and flips it open/closed - the one copy of the open/close
        /// dance every window shares (hotkeys, chat commands and CharacterPanelButtons all route here).</summary>
        private static bool ToggleGui<T>(ref T? gui, System.Func<T> create) where T : class, IToggleableGui
        {
            // Debounce: VS can fire a GUIOrOtherControls hotkey handler twice for a single key press, which would open
            // then immediately close the window. Ignore a repeat toggle of the same window within a few frames.
            long now = ClientApi?.ElapsedMilliseconds ?? 0;
            if (_lastToggleMs.TryGetValue(typeof(T), out var last) && now - last < 200) return true;
            _lastToggleMs[typeof(T)] = now;

            gui ??= create();
            if (gui.IsOpened) gui.Close();
            else gui.Open();
            return true;
        }

        private bool OpenAdmin(ICoreClientAPI api)
        {
            if (api.World.Player?.HasPrivilege(Privilege.controlserver) != true) return false;
            return ToggleGui(ref adminGui, () => new AdminDialog(api));
        }

        // Called from the admin panel's "Balance editor..." button.
        public bool OpenBalanceAdmin()
        {
            if (ClientApi == null) return false;
            return OpenBalanceAdmin(ClientApi);
        }

        private bool OpenBalanceAdmin(ICoreClientAPI api)
        {
            if (api.World.Player?.HasPrivilege(Privilege.controlserver) != true) return false;
            return ToggleGui(ref adminBalanceGui, () => new AdminBalanceDialog(api));
        }

        // Called from the admin panel's "Tree editor..." button.
        public bool OpenTreeEditor()
        {
            if (ClientApi == null) return false;
            return OpenTreeEditor(ClientApi);
        }

        private bool OpenTreeEditor(ICoreClientAPI api)
        {
            if (api.World.Player?.HasPrivilege(Privilege.controlserver) != true) return false;
            return ToggleGui(ref treeEditorGui, () => new TalentTreeEditorDialog(api));
        }

        /// <summary>The server's authored content changed and the registries were rebuilt. The open editors are
        /// looking straight at those registries, so they are told rather than left polling on a timer.</summary>
        internal void NotifyContentChanged()
        {
            treeEditorGui?.OnContentChanged();
            classEditorGui?.OnContentChanged();
            spellEditorGui?.OnContentChanged();
        }

        // Called from the admin panel's "Class editor..." button.
        public bool OpenClassEditor()
        {
            if (ClientApi == null) return false;
            return OpenClassEditor(ClientApi);
        }

        private bool OpenClassEditor(ICoreClientAPI api)
        {
            if (api.World.Player?.HasPrivilege(Privilege.controlserver) != true) return false;
            return ToggleGui(ref classEditorGui, () => new ClassEditorDialog(api));
        }

        // Called from the admin panel's "Spell editor..." button.
        public bool OpenSpellEditor()
        {
            if (ClientApi == null) return false;
            return OpenSpellEditor(ClientApi);
        }

        private bool OpenSpellEditor(ICoreClientAPI api)
        {
            if (api.World.Player?.HasPrivilege(Privilege.controlserver) != true) return false;
            return ToggleGui(ref spellEditorGui, () => new SpellEditorDialog(api));
        }

        // Public so CharacterPanelButtons (vanilla character-screen buttons) can reuse the same toggle logic
        // as the hotkeys, instead of duplicating the singleton-open/close dance.
        public bool ToggleTalents(ICoreClientAPI api) => ToggleGui(ref talentTree, () => new TalentTreeDialog(api));

        public bool ToggleSpellBook(ICoreClientAPI api)
        {
            if (Hotbar is not { } hotbar) return false;
            return ToggleGui(ref spellBook, () => new SpellBookDialog(api, hotbar));
        }

        public bool ToggleClassSelect(ICoreClientAPI api) => ToggleGui(ref classSelectGui, () => new ClassSelectDialog(api));

        public bool TogglePetPanel(ICoreClientAPI api) => ToggleGui(ref petPanel, () => new PetPanelDialog(api));

        private bool ToggleHudSettings(ICoreClientAPI api)
        {
            if (hudLayout is not { } layout) return false;
            return ToggleGui(ref hudSettings, () => new HudSettingsDialog(api, layout));
        }

        // PlayerJoin fires for every player coming into range - act only on the LOCAL player's own join.
        private void OnClientPlayerJoin(IClientPlayer byPlayer)
        {
            if (ClientApi is not { } api) return;
            if (byPlayer?.PlayerUID == null || byPlayer.PlayerUID != api.World?.Player?.PlayerUID) return;
            AutoOpenClassPickerIfNeeded(api);
        }

        // Opens the class picker for a player who hasn't chosen yet. The null-entity retry is a safety net
        // only - after PlayerJoin the local entity and its synced class-chosen flag are normally present.
        private void AutoOpenClassPickerIfNeeded(ICoreClientAPI api)
        {
            var e = api.World?.Player?.Entity;
            if (e == null) { api.Event.RegisterCallback(_ => AutoOpenClassPickerIfNeeded(api), 2000); return; }
            if (!Core.Classes.ClassChange.HasChosen(e) && classSelectGui?.IsOpened != true)
                ToggleClassSelect(api);
        }

        private void RegisterCastHotKey(ICoreClientAPI api, string code, string label, GlKeys key, int slot)
        {
            api.Input.RegisterHotKey(code, label, key, HotkeyType.CharacterControls);
            api.Input.SetHotKeyHandler(code, _ => { Hotbar?.CastSlot(slot); return true; });
        }

        // Client half of Dispose (called from the main Dispose): GUI windows hold GPU textures/renderers.
        private void DisposeClientGui()
        {
            // Instance handler - only this instance's own subscription matches, so the server instance's
            // Dispose (which also runs through here in singleplayer) is a harmless no-op.
            if (ClientApi != null) ClientApi.Event.PlayerJoin -= OnClientPlayerJoin;
            hotbarHud?.Dispose();
            hotbarHud = null;
            talentTree?.Dispose();
            talentTree = null;
            spellBook?.Dispose();
            spellBook = null;
            adminGui?.Dispose();
            adminGui = null;
            adminBalanceGui?.Dispose();
            adminBalanceGui = null;
            treeEditorGui?.Dispose();
            treeEditorGui = null;
            classEditorGui?.Dispose();
            classEditorGui = null;
            spellEditorGui?.Dispose();
            spellEditorGui = null;
            classSelectGui?.Dispose();
            classSelectGui = null;
            petPanel?.Dispose();
            petPanel = null;
            hudSettings?.Dispose();
            hudSettings = null;
            combatOverlay?.Dispose();
            combatOverlay = null;
            partyResourceOverlay?.Dispose();
            partyResourceOverlay = null;
            viewYawForcer?.Dispose();
            viewYawForcer = null;
            shieldDomeRenderer?.Dispose();
            shieldDomeRenderer = null;
            shadowformTint?.Dispose();
            shadowformTint = null;
            effectVisualsEmitter?.Dispose();
            effectVisualsEmitter = null;
            skillFxClient = null; // no resources of its own - delayed callbacks are TTL-bounded
            beamRenderer?.Dispose();
            beamRenderer = null;
            boltRenderer?.Dispose();
            boltRenderer = null;
            Icons?.Dispose();
            Icons = null;
            Loadouts = null;
            Hotbar = null;
        }
    }
}
