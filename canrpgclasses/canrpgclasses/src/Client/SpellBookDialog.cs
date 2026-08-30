using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using canrpgclasses.Client.Gui;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Spellbook dialog: left page lists the skills usable right now, right page holds the loadout picker, hotbar
    /// slots and bind-to-item panel. Bindings persist via <see cref="SpellHotbar"/> and never leave the client -
    /// only bind-to-item goes to the server.
    /// </summary>
    public class SpellBookDialog : CanrpgDialog
    {
        private const string ComposerKey = "canrpgspellbook";

        private const double PageMargin = 26;
        private const double PageTop = 22;
        private const double SpineGap = 46;
        private const double PageW = 420;
        // Tall enough for the chapter heading, hint and ruled sections that make it read as a book page - the
        // earlier value only fitted the bare controls.
        private const double BookH = 540;

        private const double CellSize = 56;
        // Two lines of caption: most spell names do not fit one line at this cell width.
        private const double CellLabelH = 34;
        private const double CellPad = 6;
        private const double SlotSize = 44;

        private const double GridHeight = 300;

        // Parchment and ink palette: warm, low-chroma aged paper with dark ink text.
        private static readonly double[] PageLight = { 0.69, 0.59, 0.42, 1 };
        private static readonly double[] PageEdge = { 0.57, 0.47, 0.31, 1 };
        private static readonly double[] Ink = { 0.14, 0.09, 0.04, 1 };
        private static readonly double[] InkSoft = { 0.30, 0.22, 0.13, 1 };
        private static readonly double[] Bronze = { 0.38, 0.24, 0.06, 1 };
        private static readonly double[] Heading = { 0.42, 0.12, 0.09, 1 };
        private static readonly double[] BookBorder = { 0.28, 0.17, 0.08, 1 };
        private static readonly double[] Picked = { 0.85, 0.65, 0.20, 1 };

        /// <summary>Cell plate and badge backing, tuned for parchment: a dark slot would read as a hole in the page.</summary>
        private static readonly double[] Plate = { 0.42, 0.33, 0.20, 0.55 };
        private static readonly double[] BadgeBack = { 0.22, 0.15, 0.07, 0.80 };
        private static readonly double[] Parchment = { 0.93, 0.88, 0.78, 1 };

        private readonly SpellHotbar hotbar;
        private readonly IconDragState drag = new();

        private string? picked;
        private string filter = "";

        /// <summary>Ids the left grid currently shows, in cell order - the grid only knows indices.</summary>
        private List<string> shown = new();

        /// <summary>Grid geometry fixed at compose time; the filter only changes which cells are filled, never the
        /// layout, so typing never rebuilds (and never steals focus from) the search box.</summary>
        private int gridCols = 1, gridRows = 1;

        private ElementBounds gridBounds = null!;
        private double gridStartY;
        private long listenerId;

        public SpellBookDialog(ICoreClientAPI capi, SpellHotbar hotbar) : base(capi)
        {
            this.hotbar = hotbar;
            // The available list changes with talents and with what's in hand; the slot row changes when a set is
            // switched. Both are cheap to re-read on a slow tick.
            listenerId = capi.Event.RegisterGameTickListener(_ => Refresh(), 500);
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Compose();
        }

        private void Refresh()
        {
            if (!IsOpened()) return;
            hotbar.RefreshSlots();

            // A change in how many skills exist (talent spent, weapon swapped) needs new cells, which is a layout
            // change; anything else is just contents.
            int count = hotbar.AvailableSpells().Count;
            int neededRows = Math.Max(1, (count + gridCols - 1) / gridCols);
            if (neededRows > gridRows) { Compose(); return; }

            FillSpellGrid();
            FillSlotGrid();
        }

        private void Compose()
        {
            var mod = canrpgclassesModSystem.ClientInstance;
            var player = capi.World?.Player?.Entity;

            var available = hotbar.AvailableSpells();
            gridCols = Math.Max(1, (int)((PageW + CellPad) / (CellSize + CellPad)));
            gridRows = Math.Max(1, (available.Count + gridCols - 1) / gridCols);

            double bookW = PageMargin * 2 + PageW * 2 + SpineGap;
            double leftX = PageMargin;
            double rightX = PageMargin + PageW + SpineGap;

            var bookBounds = ElementBounds.Fixed(0, 0, bookW, BookH);

            // ---- left page ----
            // Chapter heading: a small kicker over the class name, a rule under both, then the usage hint - the
            // page needs to read as the opening of a chapter, not as a toolbar.
            var kickerBounds = ElementBounds.Fixed(leftX + 56, PageTop + 4, PageW - 60, 18);
            var classNameBounds = ElementBounds.Fixed(leftX + 56, PageTop + 20, PageW - 60, 26);
            var headRuleBounds = ElementBounds.Fixed(leftX, PageTop + 52, PageW, 2);
            var hintBounds = ElementBounds.Fixed(leftX, PageTop + 58, PageW, 34);

            double ly = PageTop + 96;
            var skillsRuleBounds = ElementBounds.Fixed(leftX, ly + 22, PageW, 2);
            var filterBounds = ElementBounds.Fixed(leftX, ly + 28, PageW, 26);
            var emptyBounds = ElementBounds.Fixed(leftX, ly + 64, PageW, 22);
            var clipBounds = ElementBounds.Fixed(leftX, ly + 60, PageW, GridHeight);
            // BeginClip calls BeginChildElements, so the grid's bounds are relative to the clip - not to the page.
            // Giving it the page coordinates again shifted it down (and right) by the clip's own offset.
            gridStartY = 0;
            gridBounds = ElementBounds.Fixed(0, gridStartY, PageW, gridRows * (CellSize + CellLabelH + CellPad));
            var scrollBounds = clipBounds.CopyOffsetedSibling(PageW + 2, 0, 0, 0).WithFixedWidth(18);

            // ---- right page ----
            var setRuleBounds = ElementBounds.Fixed(rightX, PageTop + 22, PageW, 2);
            double ry = PageTop + 30;
            var setDropBounds = ElementBounds.Fixed(rightX, ry, PageW - 90, 26);
            var setNewBounds = ElementBounds.Fixed(rightX + PageW - 84, ry, 84, 26);
            var setNameBounds = ElementBounds.Fixed(rightX, ry + 32, PageW - 90, 26);
            var setDelBounds = ElementBounds.Fixed(rightX + PageW - 84, ry + 32, 84, 26);

            double slotY = ry + 96;
            int slotCols = Math.Max(1, (int)((PageW + CellPad) / (SlotSize + CellPad)));
            int slotRows = Math.Max(1, (hotbar.SlotCount + slotCols - 1) / slotCols);
            var slotLabelBounds = ElementBounds.Fixed(rightX, slotY - 30, PageW - 70, 22);
            var slotDecBounds = ElementBounds.Fixed(rightX + PageW - 64, slotY - 32, 30, 24);
            var slotIncBounds = ElementBounds.Fixed(rightX + PageW - 30, slotY - 32, 30, 24);
            var slotRuleBounds = ElementBounds.Fixed(rightX, slotY - 6, PageW, 2);
            var slotGridBounds = ElementBounds.Fixed(rightX, slotY, PageW, slotRows * (SlotSize + CellPad));

            double pickY = slotY + slotRows * (SlotSize + CellPad) + 4;
            var pickHintBounds = ElementBounds.Fixed(rightX, pickY, PageW, 22);

            double bindY = pickY + 30;
            var bindHeadBounds = ElementBounds.Fixed(rightX, bindY, PageW, 22);
            var bindRuleBounds = ElementBounds.Fixed(rightX, bindY + 22, PageW, 2);
            var bindTextBounds = ElementBounds.Fixed(rightX, bindY + 28, PageW, 40);
            // Stacked, full width: these captions are whole phrases ("Bind picked spell to held item") and their
            // text runs past a half-width button into the one beside it.
            var bindDoBounds = ElementBounds.Fixed(rightX, bindY + 70, PageW, 26);
            var bindClearBounds = ElementBounds.Fixed(rightX, bindY + 100, PageW, 26);

            string classId = player != null ? TalentState.CurrentClass(player) : "";
            RpgClassDef? cls = mod?.Classes.Get(classId);

            ClearComposers();
            var compo = capi.Gui.CreateCompo(ComposerKey,
                    ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
                .AddShadedDialogBG(ElementBounds.Fixed(0, 0, bookW + 40, BookH + 60), true, 5.0, 0.75f)
                .AddDialogTitleBar(Lang.Get("canrpgclasses:ui-spellbook-title"), () => TryClose())
                // Below the title bar: at y=30 the chapter heading ran under it.
                .BeginChildElements(ElementBounds.Fixed(20, GuiStyle.TitleBarHeight + 12, bookW, BookH))
                    .AddStaticCustomDraw(bookBounds, DrawParchment)

                    // left page
                    .AddIconGrid(ElementBounds.Fixed(leftX, PageTop, 46, 46), 1, 1, 46, 0, 0, "classbadge")
                    .AddStaticText(Lang.Get("canrpgclasses:ui-spellbook-title").ToUpperInvariant(),
                        CairoFont.WhiteDetailText().WithColor(InkSoft), kickerBounds)
                    .AddStaticText(cls?.DisplayName ?? classId,
                        CairoFont.WhiteSmallishText().WithColor(Bronze), classNameBounds)
                    .AddStaticCustomDraw(headRuleBounds, PageRule)
                    .AddStaticText(Lang.Get("canrpgclasses:ui-spellbook-hint"),
                        CairoFont.WhiteDetailText().WithColor(InkSoft), hintBounds)

                    .AddStaticText(Lang.Get("canrpgclasses:ui-available-skills"),
                        CairoFont.WhiteSmallText().WithColor(Heading), ElementBounds.Fixed(leftX, ly, PageW, 22))
                    .AddStaticCustomDraw(skillsRuleBounds, PageRule)
                    .AddTextInput(filterBounds, OnFilterChanged, CairoFont.WhiteSmallText().WithColor(Ink), "filter")
                    .AddDynamicText("", CairoFont.WhiteSmallText().WithColor(InkSoft), emptyBounds, "emptyhint")
                    .BeginClip(clipBounds)
                        .AddIconGrid(gridBounds, gridCols, gridRows, CellSize, CellLabelH, CellPad, "spellgrid")
                    .EndClip()
                    .AddVerticalScrollbar(OnScroll, scrollBounds, "gridscroll")

                    // right page
                    .AddStaticText(Lang.Get("canrpgclasses:ui-set"), CairoFont.WhiteSmallText().WithColor(Heading),
                        ElementBounds.Fixed(rightX, PageTop, PageW, 22))
                    .AddStaticCustomDraw(setRuleBounds, PageRule)
                    .AddDropDown(SetCodes(), SetNames(), hotbar.ActiveSetIndex, OnSetSelected, setDropBounds, "sets")
                    .AddSmallButton(Lang.Get("canrpgclasses:ui-set-new"), NewSet, setNewBounds)
                    .AddTextInput(setNameBounds, OnSetRenamed, CairoFont.WhiteSmallText().WithColor(Ink), "setname")
                    .AddIf(hotbar.Sets.Count > 1)
                        .AddSmallButton(Lang.Get("canrpgclasses:ui-set-del"), DeleteSet, setDelBounds)
                    .EndIf()

                    .AddDynamicText(Lang.Get("canrpgclasses:ui-hotbar-slots", hotbar.SlotCount),
                        CairoFont.WhiteSmallText().WithColor(Heading), slotLabelBounds, "slotlabel")
                    .AddSmallButton("-", () => ChangeSlotCount(-1), slotDecBounds)
                    .AddSmallButton("+", () => ChangeSlotCount(1), slotIncBounds)
                    .AddStaticCustomDraw(slotRuleBounds, PageRule)
                    .AddIconGrid(slotGridBounds, slotCols, slotRows, SlotSize, 0, CellPad, "slotgrid")
                    .AddDynamicText(PickHint(), CairoFont.WhiteDetailText().WithColor(InkSoft), pickHintBounds, "pickhint")

                    .AddStaticText(Lang.Get("canrpgclasses:ui-bind-header"),
                        CairoFont.WhiteSmallText().WithColor(Heading), bindHeadBounds)
                    .AddStaticCustomDraw(bindRuleBounds, PageRule)
                    .AddDynamicText(BindText(), CairoFont.WhiteDetailText().WithColor(InkSoft), bindTextBounds, "bindtext")
                    .AddSmallButton(Lang.Get("canrpgclasses:ui-bind-do"), BindPicked, bindDoBounds)
                    .AddSmallButton(Lang.Get("canrpgclasses:ui-bind-clear"), ClearBind, bindClearBounds)

                    .AddSpellTooltip(ElementBounds.Fixed(0, 0, 1, 1), "tooltip");

            SingleComposer = compo.EndChildElements().Compose();

            SingleComposer.GetTextInput("filter").SetPlaceHolderText(Lang.Get("canrpgclasses:ui-search"));
            SingleComposer.GetTextInput("filter").SetValue(filter);
            SingleComposer.GetTextInput("setname").SetPlaceHolderText(Lang.Get("canrpgclasses:ui-set-rename"));
            SingleComposer.GetTextInput("setname").SetValue(ActiveSetName());

            SetupGrids();
            FillClassBadge(cls, classId);
            FillSpellGrid();
            FillSlotGrid();

            gridBounds.CalcWorldBounds();
            clipBounds.CalcWorldBounds();
            SingleComposer.GetScrollbar("gridscroll")
                .SetHeights((float)GridHeight, (float)gridBounds.fixedHeight);
        }

        /// <summary>The parchment spread: page fill with an edge gradient, the spine fold, and a double border.</summary>
        private void DrawParchment(Context ctx, ImageSurface surface, ElementBounds bounds)
        {
            double x = bounds.drawX, y = bounds.drawY, w = bounds.OuterWidth, h = bounds.OuterHeight;

            ctx.SetSourceRGBA(PageLight[0], PageLight[1], PageLight[2], 1);
            GuiElement.RoundRectangle(ctx, x, y, w, h, GuiStyle.DialogBGRadius);
            ctx.Fill();

            // Darker towards both outer edges, lighter mid-page - the look of a bound book lying open.
            using (var grad = new LinearGradient(x, y, x + w, y))
            {
                grad.AddColorStop(0.00, new Color(PageEdge[0], PageEdge[1], PageEdge[2], 1));
                grad.AddColorStop(0.15, new Color(PageLight[0], PageLight[1], PageLight[2], 0));
                grad.AddColorStop(0.85, new Color(PageLight[0], PageLight[1], PageLight[2], 0));
                grad.AddColorStop(1.00, new Color(PageEdge[0], PageEdge[1], PageEdge[2], 1));
                ctx.SetSource(grad);
                GuiElement.RoundRectangle(ctx, x, y, w, h, GuiStyle.DialogBGRadius);
                ctx.Fill();
            }

            // Spine: a soft shadow either side of a hairline crease.
            double spineX = x + w / 2;
            using (var grad = new LinearGradient(spineX - GuiElement.scaled(SpineGap) / 2, y,
                                                 spineX + GuiElement.scaled(SpineGap) / 2, y))
            {
                grad.AddColorStop(0.0, new Color(PageEdge[0], PageEdge[1], PageEdge[2], 0));
                grad.AddColorStop(0.5, new Color(0.35, 0.27, 0.16, 0.55));
                grad.AddColorStop(1.0, new Color(PageEdge[0], PageEdge[1], PageEdge[2], 0));
                ctx.SetSource(grad);
                ctx.Rectangle(spineX - GuiElement.scaled(SpineGap) / 2, y, GuiElement.scaled(SpineGap), h);
                ctx.Fill();
            }
            ctx.SetSourceRGBA(BookBorder[0], BookBorder[1], BookBorder[2], 0.5);
            ctx.LineWidth = 1;
            ctx.MoveTo(spineX, y + 6);
            ctx.LineTo(spineX, y + h - 6);
            ctx.Stroke();

            ctx.SetSourceRGBA(BookBorder[0], BookBorder[1], BookBorder[2], 0.9);
            ctx.LineWidth = 2;
            GuiElement.RoundRectangle(ctx, x + 2, y + 2, w - 4, h - 4, GuiStyle.DialogBGRadius);
            ctx.Stroke();
            ctx.LineWidth = 1;
            GuiElement.RoundRectangle(ctx, x + 7, y + 7, w - 14, h - 14, GuiStyle.DialogBGRadius);
            ctx.Stroke();
        }

        private static void PageRule(Context ctx, ImageSurface surface, ElementBounds bounds)
        {
            ctx.SetSourceRGBA(BookBorder[0], BookBorder[1], BookBorder[2], 0.55);
            ctx.LineWidth = 1.5;
            ctx.MoveTo(bounds.drawX, bounds.drawY);
            ctx.LineTo(bounds.drawX + bounds.OuterWidth, bounds.drawY);
            ctx.Stroke();
        }

        private void SetupGrids()
        {
            var spellGrid = SingleComposer.GetIconGrid("spellgrid");
            var slotGrid = SingleComposer.GetIconGrid("slotgrid");
            var tooltip = SingleComposer.GetSpellTooltip("tooltip");
            if (spellGrid == null || slotGrid == null) return;

            // Sepia plates and ink labels: the default dark slot would read as a hole punched in the parchment.
            spellGrid.LabelFont = CairoFont.WhiteDetailText().WithColor(Ink);
            spellGrid.BadgeFont = CairoFont.WhiteDetailText().WithColor(Parchment);
            spellGrid.DefaultFrameColor = BookBorder;
            spellGrid.DefaultPlateColor = Plate;
            spellGrid.BadgeBackColor = BadgeBack;
            spellGrid.Drag = drag;
            spellGrid.CanDragFrom = i => i >= 0 && i < shown.Count;
            spellGrid.OnCellClick = (index, _) =>
            {
                if (index < 0 || index >= shown.Count) return;
                picked = shown[index];
                FillSpellGrid();
                UpdateBindText();
            };
            spellGrid.OnCellDoubleClick = index =>
            {
                if (index < 0 || index >= shown.Count) return;
                AssignToFirstFreeSlot(shown[index]);
                FillSlotGrid();
            };
            spellGrid.OnCellHover = index => ShowTooltip(tooltip, index >= 0 && index < shown.Count ? shown[index] : null);

            slotGrid.BadgeFont = CairoFont.WhiteDetailText().WithColor(Parchment);
            slotGrid.DefaultFrameColor = BookBorder;
            slotGrid.DefaultPlateColor = Plate;
            slotGrid.BadgeBackColor = BadgeBack;
            slotGrid.Drag = drag;
            slotGrid.AcceptsDrop = true;
            // The slot grid draws the cursor ghost for both grids: the spell grid lives inside BeginClip, so a ghost
            // it drew was scissored to the list and disappeared as soon as you dragged towards these slots.
            drag.GhostRenderer = slotGrid;
            slotGrid.OnDrop = (slot, id) =>
            {
                if (slot < hotbar.SlotCount) hotbar.SetBinding(slot, id);
                FillSlotGrid();
            };
            slotGrid.OnCellClick = (slot, button) =>
            {
                if (slot >= hotbar.SlotCount) return;
                // Left click drops the picked skill, or clears when nothing is picked; right click always clears.
                hotbar.SetBinding(slot, button == EnumMouseButton.Right ? null : picked);
                FillSlotGrid();
            };
            slotGrid.OnCellHover = index =>
            {
                string? id = index >= 0 && index < hotbar.SlotCount ? hotbar.Slots[index] : null;
                ShowTooltip(tooltip, id);
            };
        }

        private void ShowTooltip(GuiElementSpellTooltip? tooltip, string? spellId)
        {
            if (tooltip == null) return;
            var mod = canrpgclassesModSystem.ClientInstance;
            if (mod == null || string.IsNullOrEmpty(spellId) || !mod.Spells.TryGet(spellId!, out var spell) || spell == null)
            {
                tooltip.Clear();
                return;
            }
            tooltip.SetLines(spellId!, SpellTooltip.BuildLines(capi.World?.Player?.Entity, spell));
        }

        private void FillClassBadge(RpgClassDef? cls, string classId)
        {
            var grid = SingleComposer.GetIconGrid("classbadge");
            if (grid == null) return;
            var icons = canrpgclassesModSystem.ClientInstance?.Icons;
            grid.DefaultFrameColor = BookBorder;
            grid.Cells = new List<IconGridCell>
            {
                new() { Id = classId, Icon = icons?.GetTex(IconLoader.PathFor(cls?.IconName, classId)), IconFallback = "?" }
            };
        }

        private void FillSpellGrid()
        {
            var grid = SingleComposer.GetIconGrid("spellgrid");
            if (grid == null) return;

            var mod = canrpgclassesModSystem.ClientInstance;
            var icons = mod?.Icons;

            shown = hotbar.AvailableSpells()
                .Where(id => filter.Length == 0 ||
                             SpellName(mod, id).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            var cells = new List<IconGridCell>();
            foreach (var id in shown)
            {
                cells.Add(new IconGridCell
                {
                    Id = id,
                    Icon = icons?.GetTex(IconLoader.PathFor(IconStem(mod, id))),
                    IconFallback = Abbrev(SpellName(mod, id)),
                    Label = SpellName(mod, id),
                    FrameColor = picked == id ? Picked : SchoolColor(mod, id)
                });
            }
            // Pad out to the composed grid size: cells beyond the filtered list exist but draw nothing, so
            // filtering never changes the layout.
            while (cells.Count < gridCols * gridRows) cells.Add(new IconGridCell { Hidden = true });

            grid.Cells = cells;

            SingleComposer.GetDynamicText("emptyhint")
                ?.SetNewText(shown.Count == 0 ? Lang.Get("canrpgclasses:ui-no-skills") : "");
        }

        private void FillSlotGrid()
        {
            var grid = SingleComposer.GetIconGrid("slotgrid");
            if (grid == null) return;

            var mod = canrpgclassesModSystem.ClientInstance;
            var icons = mod?.Icons;

            var cells = new List<IconGridCell>();
            for (int i = 0; i < hotbar.SlotCount; i++)
            {
                string? id = hotbar.Slots[i];
                cells.Add(new IconGridCell
                {
                    Id = id,
                    Icon = string.IsNullOrEmpty(id) ? null : icons?.GetTex(IconLoader.PathFor(IconStem(mod, id!))),
                    IconFallback = string.IsNullOrEmpty(id) ? null : Abbrev(SpellName(mod, id!)),
                    Badge = HotkeyText(i),
                    FrameColor = string.IsNullOrEmpty(id) ? null : SchoolColor(mod, id!)
                });
            }
            grid.Cells = cells;

            SingleComposer.GetDynamicText("slotlabel")
                ?.SetNewText(Lang.Get("canrpgclasses:ui-hotbar-slots", hotbar.SlotCount));
        }

        private void OnScroll(float value)
        {
            gridBounds.fixedY = gridStartY - value;
            gridBounds.CalcWorldBounds();
        }

        private void OnFilterChanged(string value)
        {
            if (value == filter) return;
            filter = value;
            FillSpellGrid();
        }

        private string[] SetCodes() => Enumerable.Range(0, hotbar.Sets.Count).Select(i => i.ToString()).ToArray();
        private string[] SetNames() => hotbar.Sets.Select(s => s.Name).ToArray();

        private string ActiveSetName()
        {
            int i = hotbar.ActiveSetIndex;
            return i >= 0 && i < hotbar.Sets.Count ? hotbar.Sets[i].Name : "";
        }

        private void OnSetSelected(string code, bool selected)
        {
            if (!int.TryParse(code, out int index)) return;
            hotbar.SwitchSet(index);
            Compose();
        }

        private void OnSetRenamed(string value)
        {
            int i = hotbar.ActiveSetIndex;
            if (i >= 0 && i < hotbar.Sets.Count && hotbar.Sets[i].Name != value) hotbar.RenameSet(i, value);
        }

        private bool NewSet() { hotbar.AddSet(); Compose(); return true; }

        private bool DeleteSet() { hotbar.DeleteSet(hotbar.ActiveSetIndex); Compose(); return true; }

        private bool ChangeSlotCount(int delta)
        {
            hotbar.SetSlotCount(hotbar.SlotCount + delta);
            Compose();
            return true;
        }

        private void AssignToFirstFreeSlot(string id)
        {
            for (int i = 0; i < hotbar.SlotCount; i++)
                if (string.IsNullOrEmpty(hotbar.Slots[i])) { hotbar.SetBinding(i, id); return; }
        }

        private string BindText()
        {
            var mod = canrpgclassesModSystem.ClientInstance;
            var held = capi.World?.Player?.InventoryManager?.ActiveHotbarSlot?.Itemstack;
            if (held == null) return Lang.Get("canrpgclasses:ui-bind-nohand");

            var player = capi.World?.Player?.Entity;
            string? bound = player != null
                ? Core.Items.BehaviorSpellContainer.GetUsableBoundSpell(held, TalentState.CurrentClass(player))
                : null;
            return bound != null
                ? Lang.Get("canrpgclasses:ui-bind-current", SpellName(mod, bound))
                : Lang.Get("canrpgclasses:ui-bind-none");
        }

        /// <summary>What clicking a slot will do right now - which skill is picked up, or that a click clears.</summary>
        private string PickHint()
        {
            var mod = canrpgclassesModSystem.ClientInstance;
            return picked != null
                ? Lang.Get("canrpgclasses:ui-picked", SpellName(mod, picked))
                : Lang.Get("canrpgclasses:ui-clear-hint");
        }

        private void UpdateBindText()
        {
            SingleComposer.GetDynamicText("bindtext")?.SetNewText(BindText());
            SingleComposer.GetDynamicText("pickhint")?.SetNewText(PickHint());
        }

        private bool BindPicked()
        {
            if (picked != null) SendBind(picked);
            return true;
        }

        private bool ClearBind() { SendBind(""); return true; }

        private static void SendBind(string spellId)
            => canrpgclassesModSystem.ClientInstance?.ClientChannel?.SendPacket(
                new Core.Net.BindSpellPacket { SpellId = spellId });

        private static string SpellName(canrpgclassesModSystem? mod, string id)
            => mod != null && mod.Spells.TryGet(id, out var s) && s != null ? s.DisplayName : Local(id);

        private static string IconStem(canrpgclassesModSystem? mod, string id)
        {
            if (mod != null && mod.Spells.TryGet(id, out var s) && s != null && !string.IsNullOrEmpty(s.IconName))
                return s.IconName!;
            return Local(id);
        }

        private static string Local(string id)
        {
            int c = id.IndexOf(':');
            return c >= 0 ? id.Substring(c + 1) : id;
        }

        private static double[]? SchoolColor(canrpgclassesModSystem? mod, string id)
        {
            if (mod == null || !mod.Spells.TryGet(id, out var s) || s == null) return null;
            return s.School switch
            {
                SpellSchool.PhysicalMelee => new double[] { 0.85, 0.35, 0.30, 1 },
                SpellSchool.PhysicalRanged => new double[] { 0.80, 0.65, 0.30, 1 },
                SpellSchool.Fire => new double[] { 0.95, 0.45, 0.15, 1 },
                SpellSchool.Frost => new double[] { 0.40, 0.75, 0.95, 1 },
                SpellSchool.Arcane => new double[] { 0.70, 0.40, 0.90, 1 },
                SpellSchool.Holy => new double[] { 0.95, 0.90, 0.55, 1 },
                SpellSchool.Nature => new double[] { 0.40, 0.80, 0.40, 1 },
                SpellSchool.Shadow => new double[] { 0.45, 0.35, 0.55, 1 },
                _ => null
            };
        }

        /// <summary>The key currently bound to this slot's cast hotkey (reflects rebinds), or the slot number.</summary>
        private string HotkeyText(int index)
        {
            if (index < 0 || index >= SpellHotbar.HotkeyCodes.Length) return (index + 1).ToString();
            var hk = capi.Input?.GetHotKeyByCode(SpellHotbar.HotkeyCodes[index]);
            string? key = hk?.CurrentMapping?.PrimaryAsString();
            return string.IsNullOrEmpty(key) ? (index + 1).ToString() : key!;
        }

        private static string Abbrev(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            var parts = name.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return char.ToUpperInvariant(parts[0][0]).ToString() + char.ToUpperInvariant(parts[1][0]);
            return name.Length >= 3 ? name.Substring(0, 3) : name;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (listenerId != 0) { capi.Event.UnregisterGameTickListener(listenerId); listenerId = 0; }
        }
    }
}
