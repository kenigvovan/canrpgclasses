using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Edits the HUD layout for the player's current class - each class keeps its own. Controls write straight
    /// into the live config, so the HUD moves as you drag. Anchors are edited as thousandths because vanilla's
    /// slider is integer-valued.
    /// </summary>
    public class HudSettingsDialog : CanrpgDialog
    {
        private const string ComposerKey = "canrpghudsettings";

        /// <summary>Anchor sliders run 0..1000 for a 0.0..1.0 fraction - one step is a tenth of a percent of the
        /// screen, finer than anyone can aim with the mouse.</summary>
        private const int AnchorSteps = 1000;

        private readonly HudLayout layout;

        /// <summary>Which bar the sliders on the left edit.</summary>
        private int editBar;

        public HudSettingsDialog(ICoreClientAPI capi, HudLayout layout) : base(capi)
        {
            this.layout = layout;
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            layout.EditMode = true;
            Compose();
        }

        public override void OnGuiClosed()
        {
            layout.EditMode = false;
            base.OnGuiClosed();
        }

        private string CurrentClass()
        {
            var player = capi.World?.Player?.Entity;
            return player != null ? TalentState.CurrentClass(player) : "";
        }

        private void Compose()
        {
            string cls = CurrentClass();
            var cfg = layout.For(cls);

            const double colW = 300;
            const double rowH = 26;
            const double gap = 4;
            const double labelW = 130;
            const double sliderW = colW - labelW - 10;

            double leftX = 0, rightX = colW + 30;
            double y = 0;

            var compo = capi.Gui.CreateCompo(ComposerKey,
                    ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
                .AddShadedDialogBG(ElementBounds.Fixed(0, 0, colW * 2 + 30 + 40, 760), true, 5.0, 0.75f)
                .AddDialogTitleBar("HUD Layout", () => TryClose())
                .BeginChildElements(ElementBounds.Fixed(20, 30, colW * 2 + 30, 720));

            var font = CairoFont.WhiteSmallText();

            // Rows are laid out by hand rather than with BelowCopy chains: the two columns advance independently
            // and each section needs its own header spacing.
            void Header(double x, string text)
            {
                compo.AddStaticText(text, CairoFont.WhiteSmallishText(), ElementBounds.Fixed(x, y, colW, rowH));
                y += rowH;
            }

            // Ranges and current values are applied after Compose (SetSlider) - the element does not exist yet here.
            void Slider(double x, string key, string label, Action<int> apply)
            {
                compo.AddStaticText(label, font, ElementBounds.Fixed(x, y + 3, labelW, rowH));
                compo.AddSlider(v => { apply(v); SaveAll(); return true; },
                    ElementBounds.Fixed(x + labelW, y, sliderW, rowH), key);
                y += rowH + gap;
            }

            void Anchor(double x, string key, string label, Action<float> apply)
                => Slider(x, key, label, v => apply(v / (float)AnchorSteps));

            void Switch(double x, string key, string label, Action<bool> apply)
            {
                compo.AddStaticText(label, font, ElementBounds.Fixed(x, y + 3, labelW, rowH));
                compo.AddSwitch(on => { apply(on); SaveAll(); },
                    ElementBounds.Fixed(x + labelW, y, 30, rowH), key);
                y += rowH + gap;
            }

            // ---- left column: the selected skill bar, then resources ----
            var hotbar = canrpgclassesModSystem.ClientInstance?.Hotbar;
            editBar = hotbar != null ? Math.Clamp(editBar, 0, Math.Max(0, hotbar.BarCount - 1)) : 0;
            var bar = hotbar?.BarAt(editBar);

            Header(leftX, Lang.Get("canrpgclasses:ui-bars"));
            compo.AddDropDown(BarCodes(hotbar), BarNames(hotbar), editBar, OnBarSelected,
                ElementBounds.Fixed(leftX, y, colW - 180, rowH), "bars");
            compo.AddSmallButton(Lang.Get("canrpgclasses:ui-bar-new"), NewBar,
                ElementBounds.Fixed(leftX + colW - 174, y, 84, rowH));
            compo.AddSmallButton(Lang.Get("canrpgclasses:ui-bar-del"), DeleteBar,
                ElementBounds.Fixed(leftX + colW - 86, y, 86, rowH));
            y += rowH + gap;

            Anchor(leftX, "slotX", "X", v => { if (bar != null) bar.AnchorX = v; });
            Anchor(leftX, "slotY", "Y", v => { if (bar != null) bar.AnchorY = v; });
            Slider(leftX, "slotSize", "Size", v => { if (bar != null) bar.SlotSize = v; });
            Slider(leftX, "slotPad", "Padding", v => { if (bar != null) bar.SlotPadding = v; });
            Slider(leftX, "slotCount", Lang.Get("canrpgclasses:ui-bar-slots"),
                v => hotbar?.SetSlotCount(editBar, v));
            Switch(leftX, "slotVert", "Vertical", on => { if (bar != null) bar.Vertical = on; });
            Switch(leftX, "slotShow", Lang.Get("canrpgclasses:ui-bar-visible"), on => { if (bar != null) bar.Visible = on; });
            Switch(leftX, "slotRadial", Lang.Get("canrpgclasses:ui-bar-radial"), on => { if (bar != null) bar.Radial = on; });

            y += rowH / 2;
            Header(leftX, "Resources");
            Switch(leftX, "resShow", "Show", on => cfg.ShowResources = on);
            Switch(leftX, "resVert", "Vertical", on => cfg.ResVertical = on);
            Anchor(leftX, "resX", "X", v => cfg.ResAnchorX = v);
            Anchor(leftX, "resY", "Y", v => cfg.ResAnchorY = v);
            Slider(leftX, "resLen", "Length", v => cfg.ResWidth = v);
            Slider(leftX, "resThick", "Thickness", v => cfg.ResThickness = v);

            double leftBottom = y;
            y = 0;

            // ---- right column: cast bar, then combo points ----
            Header(rightX, "Cast bar");
            Switch(rightX, "castVert", "Vertical", on => cfg.CastVertical = on);
            Anchor(rightX, "castX", "X", v => cfg.CastAnchorX = v);
            Anchor(rightX, "castY", "Y", v => cfg.CastAnchorY = v);
            Slider(rightX, "castLen", "Length", v => cfg.CastWidth = v);
            Slider(rightX, "castThick", "Thickness", v => cfg.CastThickness = v);

            y += rowH / 2;
            Header(rightX, "Combo points");
            Switch(rightX, "comboShow", "Show", on => cfg.ShowCombo = on);
            Switch(rightX, "comboVert", "Vertical", on => cfg.ComboVertical = on);
            Anchor(rightX, "comboX", "X", v => cfg.ComboAnchorX = v);
            Anchor(rightX, "comboY", "Y", v => cfg.ComboAnchorY = v);
            Slider(rightX, "comboR", "Pip radius", v => cfg.ComboPipRadius = v);
            Slider(rightX, "comboGap", "Pip spacing", v => cfg.ComboPipSpacing = v);

            double bottom = Math.Max(leftBottom, y) + 10;

            // Input mode and its helper are global, so they sit under both columns rather than in either.
            compo.AddStaticText(Lang.Get("canrpgclasses:ui-input-mode"), CairoFont.WhiteSmallishText(),
                ElementBounds.Fixed(leftX, bottom, colW, rowH));
            compo.AddDropDown(ModeCodes, ModeNames(), (int)layout.Mode, OnModeSelected,
                ElementBounds.Fixed(leftX + labelW, bottom, sliderW, rowH), "inputmode");
            compo.AddStaticText(Lang.Get("canrpgclasses:ui-dim-unavailable"), font,
                ElementBounds.Fixed(rightX, bottom + 3, labelW, rowH));
            compo.AddSwitch(on => { layout.DimUnavailable = on; },
                ElementBounds.Fixed(rightX + labelW, bottom, 30, rowH), "dimswitch");
            bottom += rowH + gap;

            compo.AddDynamicText(ModeHint(), CairoFont.WhiteDetailText(),
                ElementBounds.Fixed(leftX, bottom, colW * 2 + 30, rowH * 2), "modehint");
            bottom += rowH * 2;

            compo.AddSmallButton(Lang.Get("canrpgclasses:ui-bind-numbers"), BindNumberRow,
                ElementBounds.Fixed(leftX, bottom, 200, rowH));
            compo.AddSmallButton(Lang.Get("canrpgclasses:ui-bind-default"), BindDefaults,
                ElementBounds.Fixed(leftX + 210, bottom, 200, rowH));
            compo.AddSmallButton(Lang.Get("canrpgclasses:ui-bar-copy"), CopyLayout,
                ElementBounds.Fixed(leftX + 420, bottom, 200, rowH));
            bottom += rowH + gap;

            // Profile export/import: the name box feeds both, and keys travel only when asked.
            compo.AddTextInput(ElementBounds.Fixed(leftX, bottom, 200, rowH), v => profileName = v,
                CairoFont.WhiteSmallText(), "profilename");
            compo.AddSmallButton(Lang.Get("canrpgclasses:ui-profile-export"), ExportProfile,
                ElementBounds.Fixed(leftX + 210, bottom, 130, rowH));
            compo.AddSmallButton(Lang.Get("canrpgclasses:ui-profile-import"), ImportProfile,
                ElementBounds.Fixed(leftX + 348, bottom, 130, rowH));
            compo.AddStaticText(Lang.Get("canrpgclasses:ui-profile-keys"), font,
                ElementBounds.Fixed(leftX + 486, bottom + 3, 100, rowH));
            compo.AddSwitch(on => profileKeys = on, ElementBounds.Fixed(leftX + 586, bottom, 30, rowH), "profilekeys");
            bottom += rowH + gap;

            compo.AddStaticText("Settings for class: " + (cls.Length > 0 ? cls : "-") +
                                "   ·   drag the highlighted boxes on screen to move them",
                    CairoFont.WhiteDetailText(), ElementBounds.Fixed(leftX, bottom, colW * 2 + 30, rowH));
            compo.AddSmallButton("Reset this class", ResetClass,
                ElementBounds.Fixed(leftX, bottom + rowH, 160, rowH));

            ClearComposers();
            SingleComposer = compo.EndChildElements().Compose();

            var shown = bar ?? new Bar();
            SetSlider("slotX", (int)Math.Round(shown.AnchorX * AnchorSteps), 0, AnchorSteps);
            SetSlider("slotY", (int)Math.Round(shown.AnchorY * AnchorSteps), 0, AnchorSteps);
            SetSlider("slotSize", (int)shown.SlotSize, 24, 96);
            SetSlider("slotPad", (int)shown.SlotPadding, 0, 24);
            SetSlider("slotCount", shown.Slots.Length, 1, SpellHotbar.MaxSlots);
            SetSlider("resX", (int)Math.Round(cfg.ResAnchorX * AnchorSteps), 0, AnchorSteps);
            SetSlider("resY", (int)Math.Round(cfg.ResAnchorY * AnchorSteps), 0, AnchorSteps);
            SetSlider("resLen", (int)cfg.ResWidth, 120, 420);
            SetSlider("resThick", (int)cfg.ResThickness, 6, 48);
            SetSlider("castX", (int)Math.Round(cfg.CastAnchorX * AnchorSteps), 0, AnchorSteps);
            SetSlider("castY", (int)Math.Round(cfg.CastAnchorY * AnchorSteps), 0, AnchorSteps);
            SetSlider("castLen", (int)cfg.CastWidth, 120, 420);
            SetSlider("castThick", (int)cfg.CastThickness, 6, 48);
            SetSlider("comboX", (int)Math.Round(cfg.ComboAnchorX * AnchorSteps), 0, AnchorSteps);
            SetSlider("comboY", (int)Math.Round(cfg.ComboAnchorY * AnchorSteps), 0, AnchorSteps);
            SetSlider("comboR", (int)cfg.ComboPipRadius, 3, 16);
            SetSlider("comboGap", (int)cfg.ComboPipSpacing, 10, 48);

            SingleComposer.GetSwitch("slotVert").SetValue(shown.Vertical);
            SingleComposer.GetSwitch("slotShow").SetValue(shown.Visible);
            SingleComposer.GetSwitch("slotRadial").SetValue(shown.Radial);
            SingleComposer.GetSwitch("resShow").SetValue(cfg.ShowResources);
            SingleComposer.GetSwitch("resVert").SetValue(cfg.ResVertical);
            SingleComposer.GetSwitch("castVert").SetValue(cfg.CastVertical);
            SingleComposer.GetSwitch("comboShow").SetValue(cfg.ShowCombo);
            SingleComposer.GetSwitch("comboVert").SetValue(cfg.ComboVertical);
            SingleComposer.GetSwitch("dimswitch").SetValue(layout.DimUnavailable);
            SingleComposer.GetSwitch("profilekeys").SetValue(profileKeys);
            SingleComposer.GetTextInput("profilename").SetValue(profileName);
        }

        private void SetSlider(string key, int value, int min, int max)
            => SingleComposer.GetSlider(key).SetValues(value, min, max, 1);

        private void SaveAll()
        {
            layout.Save();
            canrpgclassesModSystem.ClientInstance?.Hotbar?.SaveBars();
        }

        private static string[] BarCodes(SpellHotbar? hotbar)
            => Enumerable.Range(0, Math.Max(1, hotbar?.BarCount ?? 1)).Select(i => i.ToString()).ToArray();

        private static string[] BarNames(SpellHotbar? hotbar)
            => Enumerable.Range(0, Math.Max(1, hotbar?.BarCount ?? 1))
                .Select(i => Lang.Get("canrpgclasses:ui-bar-n", i + 1)).ToArray();

        private void OnBarSelected(string code, bool selected)
        {
            if (!int.TryParse(code, out int index)) return;
            editBar = index;
            Compose();
        }

        private bool NewBar()
        {
            int i = canrpgclassesModSystem.ClientInstance?.Hotbar?.AddBar() ?? -1;
            if (i >= 0) editBar = i;
            Compose();
            return true;
        }

        private bool DeleteBar()
        {
            canrpgclassesModSystem.ClientInstance?.Hotbar?.RemoveBar(editBar);
            editBar = 0;
            Compose();
            return true;
        }

        private bool CopyLayout()
        {
            canrpgclassesModSystem.ClientInstance?.Hotbar?.CopyLayoutToOtherSets();
            return true;
        }

        private string profileName = "default";
        private bool profileKeys;

        private bool ExportProfile()
        {
            var hotbar = canrpgclassesModSystem.ClientInstance?.Hotbar;
            if (hotbar == null) return true;
            string? file = ProfileIo.Export(capi, layout, hotbar, profileName);
            capi.ShowChatMessage(file != null
                ? Lang.Get("canrpgclasses:msg-profile-saved", file)
                : Lang.Get("canrpgclasses:msg-profile-failed"));
            return true;
        }

        private bool ImportProfile()
        {
            var hotbar = canrpgclassesModSystem.ClientInstance?.Hotbar;
            if (hotbar == null) return true;
            bool ok = ProfileIo.Import(capi, layout, hotbar, profileName, profileKeys);
            capi.ShowChatMessage(ok
                ? Lang.Get("canrpgclasses:msg-profile-loaded")
                : Lang.Get("canrpgclasses:msg-profile-failed"));
            if (ok) Compose();
            return true;
        }

        private static readonly string[] ModeCodes = { "0", "1", "2" };

        private static string[] ModeNames() => new[]
        {
            Lang.Get("canrpgclasses:ui-mode-keys"),
            Lang.Get("canrpgclasses:ui-mode-numbers"),
            Lang.Get("canrpgclasses:ui-mode-mouse")
        };

        private string ModeHint() => layout.Mode switch
        {
            InputMode.NumberRow => Lang.Get("canrpgclasses:ui-mode-numbers-hint"),
            InputMode.Mouse => Lang.Get("canrpgclasses:ui-mode-mouse-hint"),
            _ => Lang.Get("canrpgclasses:ui-mode-keys-hint")
        };

        /// <summary>Switching modes carries each mode's own key mapping - see <see cref="InputProfiles"/>.</summary>
        private void OnModeSelected(string code, bool selected)
        {
            if (!int.TryParse(code, out int mode)) return;
            InputProfiles.Switch(capi, layout, (InputMode)mode);
            SingleComposer.GetDynamicText("modehint")?.SetNewText(ModeHint());
        }

        private bool BindNumberRow()
        {
            InputProfiles.SetForCurrentMode(capi, layout, InputProfiles.NumberRowDefaults());
            Compose();
            return true;
        }

        private bool BindDefaults()
        {
            InputProfiles.SetForCurrentMode(capi, layout, InputProfiles.KeyDefaults());
            Compose();
            return true;
        }

        private bool ResetClass()
        {
            layout.Reset(CurrentClass());
            Compose();
            return true;
        }
    }
}
