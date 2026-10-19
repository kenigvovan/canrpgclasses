using canrpgclasses.Client;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace canrpgclasses
{
    /// <summary>Client GUI part of the mod system: spell hotbar, hotkeys, panels and renderers.</summary>
    public partial class canrpgclassesModSystem
    {
        // ---- Client-only state ----
        internal SpellHotbar? Hotbar;

        /// <summary>Shared icon cache, so every panel reuses the same GPU textures.</summary>
        internal Client.IconLoader? Icons;

        /// <summary>Saved talent builds; kept here so they outlive the talent window.</summary>
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
        /// <summary>The one drag in flight, shared so a skill can go from the book straight onto a bar.</summary>
        internal Client.Gui.IconDragState? SpellDrag;

        private SpellClickInput? clickInput;
        private Client.Render.RadialCastMenu? radialMenu;
        private Client.Render.CombatOverlayRenderer? combatOverlay;
        private PartyResourceOverlay? partyResourceOverlay;
        private ViewYawForcer? viewYawForcer;
        private ShieldDomeRenderer? shieldDomeRenderer;
        private ShadowformTint? shadowformTint;
        private EffectVisualsEmitter? effectVisualsEmitter;
        private SkillFxClient? skillFxClient;
        private BeamRenderer? beamRenderer;
        private BoltRenderer? boltRenderer;
        private PortalRenderer? portalRenderer;

        private ChoicePickerDialog? choicePicker;
        private SummonOfferDialog? summonOffer;
        private UtilityBuffsClient? utilityBuffs;

        private SpellCastSounds? castSounds;

        /// <summary>Waypoint destinations: admin points, plus the own anchor in Both mode (empty choice).</summary>
        internal void ShowPortalPicker(string spellId)
        {
            if (ClientApi == null) return;
            var me = ClientApi.World?.Player?.Entity;
            var entries = new System.Collections.Generic.List<(string, string)>();
            if (Core.Portals.PortalAnchors.Mode == Core.Portals.AnchorMode.Both)
                entries.Add((Lang.Get("canrpgclasses:ui-portal-own"), ""));
            foreach (var a in Core.Portals.PortalAnchors.ClientList)
            {
                string label = a.Name;
                if (me != null && a.Dim == me.Pos.Dimension)
                {
                    double dx = a.X - me.Pos.X, dz = a.Z - me.Pos.Z;
                    label += "  (" + (int)System.Math.Sqrt(dx * dx + dz * dz) + ")";
                }
                entries.Add((label, a.Name));
            }
            choicePicker ??= new ChoicePickerDialog(ClientApi);
            choicePicker.Show(spellId, Lang.Get("canrpgclasses:ui-portal-pick"), entries, Lang.Get("canrpgclasses:ui-portal-none"));
        }

        /// <summary>Summon targets: online party members other than me (choice = player uid).</summary>
        internal void ShowPartyPicker(string spellId)
        {
            if (ClientApi == null) return;
            var party = ClientApi.ModLoader.GetModSystem<canparty.canpartyModSystem>()?.Client;
            string myUid = ClientApi.World.Player.PlayerUID;
            var entries = new System.Collections.Generic.List<(string, string)>();
            if (party != null)
                foreach (var p in ClientApi.World.AllOnlinePlayers)
                    if (p != null && p.PlayerUID != myUid && party.IsInMyParty(p.PlayerUID))
                        entries.Add((p.PlayerName, p.PlayerUID));
            choicePicker ??= new ChoicePickerDialog(ClientApi);
            choicePicker.Show(spellId, Lang.Get("canrpgclasses:ui-summon-pick"), entries, Lang.Get("canrpgclasses:ui-summon-none"));
        }

        private void ShowSummonOffer(Core.Portals.SummonOfferPacket p)
        {
            if (ClientApi == null) return;
            summonOffer ??= new SummonOfferDialog(ClientApi);
            summonOffer.Show(p);
        }

        // ---- Client: spell hotbar HUD + hotkeys ----
        private void SetupHotbar(ICoreClientAPI api)
        {
            hudLayout = new HudLayout(api);
            Hotbar = new SpellHotbar(api, hudLayout);
            Icons = new IconLoader(api);
            Loadouts = new TalentLoadouts(api);

            // Bar 1: Z R F V X, then Shift + Z R F V. Bars 2-3 are unbound (click, drag or wheel).
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

            hotbarHud = new Client.Render.HudSpellHotbarRenderer(api, Hotbar, hudLayout);
            SpellDrag = new Client.Gui.IconDragState();
            clickInput = new SpellClickInput(api, Hotbar, hudLayout, hotbarHud, SpellDrag);
            radialMenu = new Client.Render.RadialCastMenu(api, Hotbar, clickInput);

            // Unbound by default: vanilla Alt already frees the cursor while held; this is a toggle alternative.
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

            // Auto-open the picker for new players on PlayerJoin (like vanilla character creation), not a timer:
            // by then the synced class-chosen flag is present even on a slow connection.
            api.Event.PlayerJoin += OnClientPlayerJoin;

            // controlserver only here; the server re-checks every action.
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

        /// <summary>Lazily creates a window and toggles it; every hotkey, command and button routes here.</summary>
        private static bool ToggleGui<T>(ref T? gui, System.Func<T> create) where T : class, IToggleableGui
        {
            // VS can fire a GUIOrOtherControls handler twice per press, which would open then close the window.
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

        /// <summary>Tells open editors the registries were rebuilt from new server content.</summary>
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

        // Public for CharacterPanelButtons.
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

        // The null-entity retry is only a safety net; after PlayerJoin the entity is normally there.
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

        /// <summary>Registered ahead of vanilla so a slot bound to 1-9 beats the item hotbar. Returning false lets
        /// vanilla run, which is how Mouse mode silences the keys.</summary>
        private void RegisterCastHotKey(ICoreClientAPI api, string code, string label, GlKeys key, int bar, int slot, bool shift = false)
        {
            // Hotkeys outlive the world (static manager) and RegisterHotKeyFirst throws on a duplicate, so on relog
            // only re-attach the handler.
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
            // Instance handler, so the server instance's Dispose in singleplayer is a no-op here.
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
            portalRenderer?.Dispose();
            portalRenderer = null;
            choicePicker?.Dispose();
            choicePicker = null;
            summonOffer?.Dispose();
            summonOffer = null;
            utilityBuffs?.Dispose();
            utilityBuffs = null;
            castSounds?.Dispose();
            castSounds = null;
            Icons?.Dispose();
            Icons = null;
            Loadouts = null;
            Hotbar = null;
        }
    }
}
