using System;
using Vintagestory.API.Client;
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
                .AddShadedDialogBG(ElementBounds.Fixed(0, 0, colW * 2 + 30 + 40, 470), true, 5.0, 0.75f)
                .AddDialogTitleBar("HUD Layout", () => TryClose())
                .BeginChildElements(ElementBounds.Fixed(20, 30, colW * 2 + 30, 430));

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
                compo.AddSlider(v => { apply(v); layout.Save(); return true; },
                    ElementBounds.Fixed(x + labelW, y, sliderW, rowH), key);
                y += rowH + gap;
            }

            void Anchor(double x, string key, string label, Action<float> apply)
                => Slider(x, key, label, v => apply(v / (float)AnchorSteps));

            void Switch(double x, string key, string label, Action<bool> apply)
            {
                compo.AddStaticText(label, font, ElementBounds.Fixed(x, y + 3, labelW, rowH));
                compo.AddSwitch(on => { apply(on); layout.Save(); },
                    ElementBounds.Fixed(x + labelW, y, 30, rowH), key);
                y += rowH + gap;
            }

            // ---- left column: skill slots, then resources ----
            Header(leftX, "Skill slots");
            Anchor(leftX, "slotX", "X", v => cfg.SlotAnchorX = v);
            Anchor(leftX, "slotY", "Y", v => cfg.SlotAnchorY = v);
            Slider(leftX, "slotSize", "Size", v => cfg.SlotSize = v);
            Slider(leftX, "slotPad", "Padding", v => cfg.SlotPadding = v);
            Switch(leftX, "slotVert", "Vertical", on => cfg.Vertical = on);

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

            compo.AddStaticText("Settings for class: " + (cls.Length > 0 ? cls : "-") +
                                "   ·   drag the highlighted boxes on screen to move them",
                    CairoFont.WhiteDetailText(), ElementBounds.Fixed(leftX, bottom, colW * 2 + 30, rowH));
            compo.AddSmallButton("Reset this class", ResetClass,
                ElementBounds.Fixed(leftX, bottom + rowH, 160, rowH));

            ClearComposers();
            SingleComposer = compo.EndChildElements().Compose();

            SetSlider("slotX", (int)Math.Round(cfg.SlotAnchorX * AnchorSteps), 0, AnchorSteps);
            SetSlider("slotY", (int)Math.Round(cfg.SlotAnchorY * AnchorSteps), 0, AnchorSteps);
            SetSlider("slotSize", (int)cfg.SlotSize, 24, 96);
            SetSlider("slotPad", (int)cfg.SlotPadding, 0, 24);
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

            SingleComposer.GetSwitch("slotVert").SetValue(cfg.Vertical);
            SingleComposer.GetSwitch("resShow").SetValue(cfg.ShowResources);
            SingleComposer.GetSwitch("resVert").SetValue(cfg.ResVertical);
            SingleComposer.GetSwitch("castVert").SetValue(cfg.CastVertical);
            SingleComposer.GetSwitch("comboShow").SetValue(cfg.ShowCombo);
            SingleComposer.GetSwitch("comboVert").SetValue(cfg.ComboVertical);
        }

        private void SetSlider(string key, int value, int min, int max)
            => SingleComposer.GetSlider(key).SetValues(value, min, max, 1);

        private bool ResetClass()
        {
            layout.Reset(CurrentClass());
            Compose();
            return true;
        }
    }
}
