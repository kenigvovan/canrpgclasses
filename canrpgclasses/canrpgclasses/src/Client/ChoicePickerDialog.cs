using System.Collections.Generic;
using Vintagestory.API.Client;

namespace canrpgclasses.Client
{
    /// <summary>A list of buttons picked before a cast (Waypoint destination, summon target). Picking sends the cast
    /// with the entry's choice.</summary>
    public class ChoicePickerDialog : CanrpgDialog
    {
        private const string ComposerKey = "canrpgchoicepicker";
        private string spellId = "";

        public ChoicePickerDialog(ICoreClientAPI capi) : base(capi) { }

        public void Show(string forSpellId, string title, List<(string label, string choice)> entries, string emptyText)
        {
            spellId = forSpellId;
            Compose(title, entries, emptyText);
            if (!IsOpened()) TryOpen();
        }

        private void Compose(string title, List<(string label, string choice)> entries, string emptyText)
        {
            const double w = 300, rowH = 26, gap = 4;
            double h = System.Math.Max(1, entries.Count) * (rowH + gap);

            var compo = capi.Gui.CreateCompo(ComposerKey,
                    ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
                .AddShadedDialogBG(ElementBounds.Fixed(0, 0, w + 40, h + 50), true, 5.0, 0.75f)
                .AddDialogTitleBar(title, () => TryClose())
                .BeginChildElements(ElementBounds.Fixed(20, 35, w, h));

            double y = 0;
            foreach (var (label, choice) in entries)
            {
                string c = choice;
                compo.AddSmallButton(label, () => Pick(c), ElementBounds.Fixed(0, y, w, rowH));
                y += rowH + gap;
            }
            if (entries.Count == 0)
                compo.AddStaticText(emptyText, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 0, w, rowH));

            ClearComposers();
            SingleComposer = compo.EndChildElements().Compose();
        }

        private bool Pick(string choice)
        {
            TryClose();
            canrpgclassesModSystem.ClientInstance?.Hotbar?.Cast(spellId, choice);
            return true;
        }
    }
}
