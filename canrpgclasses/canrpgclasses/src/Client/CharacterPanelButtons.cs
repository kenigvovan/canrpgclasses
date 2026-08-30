using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Adds a "Talents" / "Spellbook" button panel next to the vanilla character screen, so our windows don't
    /// need separate hotkeys. Follows the game's own pattern for that dialog: compose an extra named panel into
    /// the loaded <see cref="GuiDialogCharacterBase"/>'s composer set.
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

            // ComposeExtraGuis fires whenever the dialog's own "Character" tab (re)composes - same hook the
            // vanilla Environment/Stats panels use, so our panel appears and disappears in lockstep with it.
            dlg.ComposeExtraGuis += ComposeButtons;
        }

        private void ComposeButtons()
        {
            var mod = canrpgclassesModSystem.ClientInstance;
            if (mod == null || dlg == null) return;

            // Stack below whichever of the vanilla side panels is present (Stats, else Environment, else just
            // the character panel itself) instead of anchoring "right of playercharacter" directly - that's the
            // exact spot the vanilla Stats panel already occupies, so anchoring there too caused an overlap.
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
