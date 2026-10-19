using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Adds a Talents / Spellbook button panel to the vanilla <see cref="GuiDialogCharacterBase"/> as an extra
    /// named composer.
    /// </summary>
    public class CharacterPanelButtons : ModSystem
    {
        private const string ComposerName = "canrpgclasses-charbuttons";

        private ICoreClientAPI capi = null!;
        private GuiDialogCharacterBase? dlg;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            dlg = api.Gui.LoadedGuis.Find(d => d is GuiDialogCharacterBase) as GuiDialogCharacterBase;
            if (dlg == null) return;

            // Same hook the vanilla Environment/Stats panels use, so ours follows the Character tab.
            dlg.ComposeExtraGuis += ComposeButtons;
        }

        private void ComposeButtons()
        {
            var mod = canrpgclassesModSystem.ClientInstance;
            if (mod == null || dlg == null) return;

            // Stack below the vanilla side panel; "right of playercharacter" is where Stats already sits.
            ElementBounds anchor =
                dlg.Composers.ContainsKey("playerstats") ? dlg.Composers["playerstats"].Bounds :
                dlg.Composers.ContainsKey("environment") ? dlg.Composers["environment"].Bounds :
                dlg.Composers["playercharacter"].Bounds;

            const double btnWidth = 150, btnHeight = 30, gap = 8;
            ElementBounds firstBtn = ElementBounds.Fixed(0, 25, btnWidth, btnHeight);
            ElementBounds secondBtn = firstBtn.BelowCopy(0, gap, 0, 0);
            ElementBounds thirdBtn = secondBtn.BelowCopy(0, gap, 0, 0);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(firstBtn, secondBtn, thirdBtn);

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.None)
                .WithFixedPosition(
                    anchor.renderX / RuntimeEnv.GUIScale,
                    (anchor.renderY + anchor.OuterHeight) / RuntimeEnv.GUIScale + 10);

            dlg.Composers[ComposerName] = capi.Gui.CreateCompo(ComposerName, dialogBounds)
                .AddShadedDialogBG(bgBounds, true, 5.0, 0.75f)
                .AddDialogTitleBar(Lang.Get("canrpgclasses:ui-charpanel-title"), () => dlg.OnTitleBarClose())
                .BeginChildElements(bgBounds)
                .AddButton(Lang.Get("canrpgclasses:ui-charpanel-talents"), () => mod.ToggleTalents(capi), firstBtn)
                .AddButton(Lang.Get("canrpgclasses:ui-charpanel-spellbook"), () => mod.ToggleSpellBook(capi), secondBtn)
                .AddButton(Lang.Get("canrpgclasses:ui-charpanel-classselect"), () => mod.ToggleClassSelect(capi), thirdBtn)
                .EndChildElements()
                .Compose(true);
        }

        public override void Dispose()
        {
            if (dlg != null) dlg.ComposeExtraGuis -= ComposeButtons;
            base.Dispose();
        }
    }
}
