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
        /// <summary>Client-only: the one drag in flight, shared by the spellbook and the on-screen bars so a skill
        /// can be dragged from the book straight onto a bar.</summary>
        internal Client.Gui.IconDragState? SpellDrag;

        private SpellClickInput? clickInput;
        private Client.Render.RadialCastMenu? radialMenu;

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
            hudLayout = new HudLayout(api);
            Hotbar = new SpellHotbar(api, hudLayout);
            Icons = new IconLoader(api);
            Loadouts = new TalentLoadouts(api);

            // Bar 1, slots 1-4 on Z R F V, 5-9 on X and Shift + Z R F V - one hand covers all nine. Bars 2-3 come
            // with no keys: they are meant to be clicked, dragged onto, or opened as a wheel.
            var defaultKeys = new[]
            {
                GlKeys.Z, GlKeys.R, GlKeys.F, GlKeys.V, GlKeys.X,
                GlKeys.Z, GlKeys.R, GlKeys.F, GlKeys.V
            };
            for (int bar = 0; bar < SpellHotbar.MaxBars; bar++)
                for (int slot = 0; slot < SpellHotbar.MaxSlots; slot++)
                    RegisterCastHotKey(api, SpellHotbar.HotkeyCodes[bar][slot],
                        Lang.Get("canrpgclasses:hotkey-cast-bar", bar + 1, slot + 1),
                        bar == 0 ? defaultKeys[slot] : GlKeys.Unknown, bar, slot, shift: bar == 0 && slot >= 5);

            // Registers itself with the Ortho render stage; there is nothing to open - it draws whenever the
            // player is in-world.
            hotbarHud = new Client.Render.HudSpellHotbarRenderer(api, Hotbar, hudLayout);
            SpellDrag = new Client.Gui.IconDragState();
            clickInput = new SpellClickInput(api, Hotbar, hudLayout, hotbarHud, SpellDrag);
            radialMenu = new Client.Render.RadialCastMenu(api, Hotbar, clickInput);

            // Unbound by default: vanilla's Alt (togglemousecontrol) already frees the cursor while held. This is
            // only for players who would rather toggle it than hold a key.
            api.Input.RegisterHotKey("canrpgcursor", Lang.Get("canrpgclasses:hotkey-cursor"), GlKeys.Unknown, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("canrpgcursor", _ => { clickInput?.ToggleCursor(); return true; });

            // One hold-to-open wheel per bar; only the first gets a key by default.
            for (int bar = 0; bar < SpellHotbar.MaxBars; bar++)
            {
                int index = bar;
                string code = "canrpgwheel" + (bar + 1);
                api.Input.RegisterHotKey(code, Lang.Get("canrpgclasses:hotkey-wheel", bar + 1),
                    bar == 0 ? GlKeys.Tilde : GlKeys.Unknown, HotkeyType.GUIOrOtherControls);
                api.Input.SetHotKeyHandler(code, comb => { radialMenu?.Open(index, comb.KeyCode); return true; });
            }

            api.Input.RegisterHotKey("canrpgnextset", Lang.Get("canrpgclasses:hotkey-nextset"), GlKeys.B, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("canrpgnextset", _ => { Hotbar?.NextSet(); return true; });

            for (int i = 0; i < SetHotkeyCodes.Length; i++)
                RegisterSetHotKey(api, SetHotkeyCodes[i], i);

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

            api.ChatCommands.Create("canrpgexport")
                .WithDescription("Export the hotbar/HUD setup to ModConfig (arg: profile name)")
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("name"))
                .HandleWith(args => ExportProfile(api, args[0] as string));

            api.ChatCommands.Create("canrpgimport")
                .WithDescription("Import a hotbar/HUD profile from ModConfig (args: name, keys)")
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("name"), api.ChatCommands.Parsers.OptionalBool("keys"))
                .HandleWith(args => ImportProfile(api, args[0] as string, args[1] as bool? ?? false));

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
            var drag = SpellDrag ??= new Client.Gui.IconDragState();
            return ToggleGui(ref spellBook, () => new SpellBookDialog(api, hotbar, drag));
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

        private TextCommandResult ExportProfile(ICoreClientAPI api, string? name)
        {
            if (hudLayout is not { } layout || Hotbar is not { } hotbar) return TextCommandResult.Error("Not ready.");
            string? file = ProfileIo.Export(api, layout, hotbar, string.IsNullOrWhiteSpace(name) ? "default" : name!);
            return file != null
                ? TextCommandResult.Success("Saved to ModConfig/" + file)
                : TextCommandResult.Error("Could not write the profile.");
        }

        private TextCommandResult ImportProfile(ICoreClientAPI api, string? name, bool withKeys)
        {
            if (hudLayout is not { } layout || Hotbar is not { } hotbar) return TextCommandResult.Error("Not ready.");
            return ProfileIo.Import(api, layout, hotbar, string.IsNullOrWhiteSpace(name) ? "default" : name!, withKeys)
                ? TextCommandResult.Success("Profile loaded.")
                : TextCommandResult.Error("No such profile, or it is from a newer version.");
        }

        /// <summary>Set-switch hotkeys. Unbound by default - the cycle key covers most players.</summary>
        private static readonly string[] SetHotkeyCodes = { "canrpgset1", "canrpgset2", "canrpgset3" };

        /// <summary>
        /// Registered ahead of the vanilla hotkeys, so a cast slot bound to 1-9 wins over the item hotbar in
        /// NumberRow mode. Returning false in the other modes lets the vanilla handler run as usual - which is
        /// also how Mouse mode silences the keys.
        /// </summary>
        private void RegisterCastHotKey(ICoreClientAPI api, string code, string label, GlKeys key, int bar, int slot, bool shift = false)
        {
            // The hotkey manager is a process-lifetime static (ScreenManager.hotkeyManager): leaving a world only
            // clears the handlers, the hotkeys stay. A second insert-first of the same code throws (Insert ->
            // Dictionary.Add), so on a relog re-register only what isn't there - the entry keeps its ordering and
            // the player's rebind, and the handler below is re-attached either way.
            if (api.Input.GetHotKeyByCode(code) == null)
                api.Input.RegisterHotKeyFirst(code, label, key, HotkeyType.CharacterControls, shiftPressed: shift);
            api.Input.SetHotKeyHandler(code, _ =>
            {
                if (hudLayout?.Mode == InputMode.Mouse) return false;
                Hotbar?.CastSlot(bar, slot);
                return true;
            });
        }

        private void RegisterSetHotKey(ICoreClientAPI api, string code, int index)
        {
            api.Input.RegisterHotKey(code, Lang.Get("canrpgclasses:hotkey-set", index + 1), GlKeys.Unknown, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler(code, _ => { Hotbar?.SwitchSet(index); return true; });
        }

        // Client half of Dispose (called from the main Dispose): GUI windows hold GPU textures/renderers.
        private void DisposeClientGui()
        {
            // Instance handler - only this instance's own subscription matches, so the server instance's
            // Dispose (which also runs through here in singleplayer) is a harmless no-op.
            if (ClientApi != null) ClientApi.Event.PlayerJoin -= OnClientPlayerJoin;
            radialMenu?.Dispose();
            radialMenu = null;
            clickInput?.Dispose();
            clickInput = null;
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
