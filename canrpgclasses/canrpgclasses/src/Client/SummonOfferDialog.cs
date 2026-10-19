using canrpgclasses.Core.Portals;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace canrpgclasses.Client
{
    /// <summary>"X is summoning you" with Accept / Decline. Closing or timing out counts as a decline.</summary>
    public class SummonOfferDialog : CanrpgDialog
    {
        private const string ComposerKey = "canrpgsummonoffer";
        private long offerId;
        private bool answered;
        private long timeoutId;

        public SummonOfferDialog(ICoreClientAPI capi) : base(capi) { }

        public void Show(SummonOfferPacket p)
        {
            if (IsOpened() && !answered) Answer(false);
            offerId = p.Id;
            answered = false;

            const double w = 300, rowH = 26;
            var compo = capi.Gui.CreateCompo(ComposerKey,
                    ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
                .AddShadedDialogBG(ElementBounds.Fixed(0, 0, w + 40, 130), true, 5.0, 0.75f)
                .AddDialogTitleBar(Lang.Get("canrpgclasses:ui-summon-title"), () => TryClose())
                .BeginChildElements(ElementBounds.Fixed(20, 35, w, 90))
                .AddStaticText(Lang.Get("canrpgclasses:ui-summon-text", p.From), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 0, w, 40))
                .AddSmallButton(Lang.Get("canrpgclasses:ui-summon-accept"), () => { Answer(true); return true; }, ElementBounds.Fixed(0, 50, 140, rowH))
                .AddSmallButton(Lang.Get("canrpgclasses:ui-summon-decline"), () => { Answer(false); return true; }, ElementBounds.Fixed(160, 50, 140, rowH));

            ClearComposers();
            SingleComposer = compo.EndChildElements().Compose();
            TryOpen();

            if (timeoutId != 0) capi.Event.UnregisterCallback(timeoutId);
            long id = offerId;
            timeoutId = capi.Event.RegisterCallback(_ => { if (offerId == id && !answered) Answer(false); }, p.Seconds * 1000);
        }

        private void Answer(bool accept)
        {
            if (answered) return;
            answered = true;
            canrpgclassesModSystem.ClientInstance?.ClientChannel?.SendPacket(new SummonAnswerPacket { Id = offerId, Accept = accept });
            TryClose();
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            if (!answered) Answer(false);
        }
    }
}
