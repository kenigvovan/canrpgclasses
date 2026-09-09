using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace canrpgclasses.Client
{
    /// <summary>Our own tab in the vanilla character dialog: class header, experience bar and the sheet in sections.
    /// The dialog keeps the size of its FIRST tab and never grows for ours, so everything lives in a clipped
    /// container with a scrollbar, restacked by <see cref="Relayout"/> when a section changes height.</summary>
    public class RpgSheetTab : ModSystem
    {
        private const string MainComposerKey = "playercharacter";
        private const string ContainerKey = "canrpgclasses-sheet-content";
        private const string ScrollbarKey = "canrpgclasses-sheet-scroll";

        /// <summary>Fallback panel width, used only if the dialog hasn't sized itself yet.</summary>
        private const double FallbackWidth = 300.0;
        private const double ScrollbarWidth = 16.0;
        private const double BarHeight = 12.0;
        private const double RuleHeight = 2.0;

        private ICoreClientAPI capi = null!;
        private GuiDialogCharacterBase? dlg;
        private GuiTab? myTab;
        private int myTabIndex = -1;
        private int curTab;

        // One composition's worth of elements; `owner` is which composition, so a stale set is never touched.
        private GuiComposer? owner;
        private GuiElementRichtext? headerElem;
        private GuiElementRichtext? xpTextElem;
        private GuiElementStatbar? xpBar;
        private readonly List<GuiElementRichtext> sectionElems = new();
        private readonly List<(GuiElement Elem, double GapBefore)> slots = new();

        private ElementBounds? contentBounds;
        private double viewHeight;
        private double lastTotalHeight = -1.0;
        private float lastXpValue = -1f;
        private float lastXpMax = -1f;
        private bool needsLayout;

        // Built once per composition: CairoFont.White*() allocates, and the tab refreshes twice a second.
        private CairoFont headerFont = null!;
        private CairoFont bodyFont = null!;

        // Markup as last written: SetNewText bakes a surface and uploads a texture, so unchanged sections are skipped.
        private readonly List<string> lastMarkup = new();
        private readonly List<SheetSection> sections = new();
        private readonly StringBuilder sb = new();

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            dlg = api.Gui.LoadedGuis.Find(d => d is GuiDialogCharacterBase) as GuiDialogCharacterBase;
            if (dlg == null)
            {
                api.Logger.Warning("[canrpgclasses] character dialog not found; RPG tab not added.");
                return;
            }

            dlg.TabClicked += idx => curTab = idx;
            dlg.OnOpened += EnsureTabAdded;

            api.Event.RegisterGameTickListener(OnRefresh, 500);
            // After every mod's StartClientSide (so our tab index is final), before the dialog can be opened.
            api.Event.RegisterCallback(_ => EnsureTabAdded(), 1);
        }

        private void EnsureTabAdded()
        {
            if (dlg == null) return;
            if (myTabIndex >= 0) { if (myTab != null) myTab.Name = Lang.Get("canrpgclasses:ui-rpgtab"); return; }

            // DataInt must equal our handler's index in RenderTabHandlers - that is how clicks are routed.
            myTabIndex = dlg.RenderTabHandlers.Count;
            myTab = new GuiTab { Name = Lang.Get("canrpgclasses:ui-rpgtab"), DataInt = myTabIndex };
            dlg.Tabs.Add(myTab);
            dlg.RenderTabHandlers.Add(ComposeTab);
        }

        private void ComposeTab(GuiComposer compo)
        {
            owner = null;
            slots.Clear();
            sectionElems.Clear();
            lastMarkup.Clear();
            lastTotalHeight = -1.0;
            lastXpValue = -1f;
            lastXpMax = -1f;

            var player = capi.World?.Player?.Entity;

            // Sized by the dialog's first tab, which is narrow - hardcoding a width parks the scrollbar outside it.
            var parent = compo.CurParentBounds;
            double panelW = Math.Max(160.0, parent?.fixedWidth > 0 ? parent.fixedWidth : FallbackWidth);
            viewHeight = Math.Max(120.0, (parent?.fixedHeight ?? 420.0) - 34.0);
            double contentW = Math.Max(80.0, panelW - ScrollbarWidth - 6.0);

            var clip = ElementBounds.Fixed(0.0, 25.0, contentW, viewHeight);
            contentBounds = clip.ForkContainingChild(0.0, 0.0, 0.0, 0.0);
            var scrollBounds = ElementBounds.Fixed(contentW + 4.0, 25.0, ScrollbarWidth, viewHeight);

            compo.BeginClip(clip)
                    .AddContainer(contentBounds, ContainerKey)
                .EndClip()
                .AddVerticalScrollbar(OnScroll, scrollBounds, ScrollbarKey);

            var container = compo.GetContainer(ContainerKey);
            if (container == null) return;
            container.unscaledCellSpacing = 0; // we place every element ourselves; the default spacing only skews the total

            headerFont = CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2);
            bodyFont = CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.2);

            var header = player != null ? RpgCharacterSheet.BuildHeader(player) : default;

            // Slot order here must match the indices Update() writes to: 0 header, 1 XP label, 2+i sections.
            string headerMarkup = Markup(b => RpgSheetVtml.Header(b, header));
            headerElem = AddRichtext(container, contentW, headerFont, headerMarkup);
            lastMarkup.Add(headerMarkup);
            slots.Add((headerElem, 0.0));

            xpBar = new GuiElementStatbar(capi, ElementBounds.Fixed(0.0, 0.0, contentW, BarHeight),
                GuiStyle.XPBarColor, false, false) { ShowValueOnHover = false };
            container.Add(xpBar);
            slots.Add((xpBar, 6.0));
            ApplyXp(header);

            string xpMarkup = Markup(b => RpgSheetVtml.XpLabel(b, header));
            xpTextElem = AddRichtext(container, contentW, bodyFont, xpMarkup);
            lastMarkup.Add(xpMarkup);
            slots.Add((xpTextElem, 2.0));

            if (player != null) RpgCharacterSheet.BuildSections(player, sections);
            for (int i = 0; i < RpgCharacterSheet.SectionCount; i++)
            {
                // interactive: true - a static custom-draw bakes into the container texture at its first position.
                var rule = new GuiElementCustomDraw(capi,
                    ElementBounds.Fixed(0.0, 0.0, contentW, RuleHeight), EditorStyle.RuleLocal, true);
                container.Add(rule);
                slots.Add((rule, 10.0));

                int idx = i;
                string markup = i < sections.Count ? Markup(b => RpgSheetVtml.Section(b, sections[idx])) : "";
                var sect = AddRichtext(container, contentW, bodyFont, markup);
                sectionElems.Add(sect);
                lastMarkup.Add(markup);
                slots.Add((sect, 6.0));
            }

            owner = compo;
            Relayout(compo);
            // Heights measured before Compose() can be off, so the first refresh lays out once more.
            needsLayout = true;
        }

        private GuiElementRichtext AddRichtext(GuiElementContainer container, double width, CairoFont font, string markup)
        {
            var elem = new GuiElementRichtext(capi, VtmlUtil.Richtextify(capi, markup, font),
                ElementBounds.Fixed(0.0, 0.0, width, 1.0));
            container.Add(elem);
            return elem;
        }

        private string Markup(Action<StringBuilder> build)
        {
            sb.Clear();
            build(sb);
            return sb.ToString();
        }

        private void OnScroll(float value)
        {
            if (contentBounds == null) return;
            contentBounds.fixedY = -value;
            contentBounds.CalcWorldBounds();
        }

        /// <summary>Restacks the elements top to bottom: richtext writes its own height, so everything below it
        /// moves and the scroll range is refreshed.</summary>
        private void Relayout(GuiComposer compo)
        {
            double y = 0.0;
            foreach (var (elem, gap) in slots)
            {
                // Richtext measures itself here (BeforeCalcBounds -> CalcHeightAndPositions).
                elem.BeforeCalcBounds();

                y += gap;
                elem.Bounds.fixedY = y;
                elem.Bounds.CalcWorldBounds();
                y += elem.Bounds.fixedHeight;
            }

            if (contentBounds != null)
            {
                contentBounds.fixedHeight = y;
                contentBounds.CalcWorldBounds();
            }

            if (Math.Abs(y - lastTotalHeight) < 0.5) return;
            lastTotalHeight = y;

            var bar = compo.GetScrollbar(ScrollbarKey);
            if (bar == null) return;
            bar.SetHeights((float)viewHeight, (float)y);
            // The bar clamps itself when the content shrinks; follow it, or the sheet stays scrolled past its end.
            OnScroll(bar.CurrentYPosition);
        }

        private void OnRefresh(float dt)
        {
            if (dlg == null) return;
            EnsureTabAdded(); // idempotent backstop, in case the dialog was built before we ran
            if (!dlg.IsOpened() || curTab != myTabIndex) return;

            var player = capi.World?.Player?.Entity;
            if (player == null) return;

            var compo = dlg.Composers?[MainComposerKey];
            if (compo == null || compo != owner || headerElem == null) return;

            var header = RpgCharacterSheet.BuildHeader(player);
            bool changed = Update(headerElem, 0, Markup(b => RpgSheetVtml.Header(b, header)), headerFont);
            changed |= Update(xpTextElem, 1, Markup(b => RpgSheetVtml.XpLabel(b, header)), bodyFont);
            ApplyXp(header);

            RpgCharacterSheet.BuildSections(player, sections);
            for (int i = 0; i < sectionElems.Count && i < sections.Count; i++)
            {
                int idx = i;
                changed |= Update(sectionElems[i], 2 + i, Markup(b => RpgSheetVtml.Section(b, sections[idx])), bodyFont);
            }

            if (changed || needsLayout)
            {
                needsLayout = false;
                Relayout(compo);
            }
        }

        /// <summary>Feeds the bar, skipping unchanged values. At max level it sits full rather than empty.</summary>
        private void ApplyXp(in SheetHeader header)
        {
            if (xpBar == null) return;
            bool maxed = header.MaxLevel;
            float value = maxed ? 1f : header.XpInto;
            float max = maxed ? 1f : header.XpNext;
            if (Math.Abs(value - lastXpValue) < 0.5f && Math.Abs(max - lastXpMax) < 0.5f) return;

            lastXpValue = value;
            lastXpMax = max;
            xpBar.SetValues(value, 0f, max);
        }

        /// <summary>Writes new markup only when it differs, so an unchanged sheet costs nothing.</summary>
        private bool Update(GuiElementRichtext? elem, int slot, string markup, CairoFont font)
        {
            if (elem == null) return false;
            while (lastMarkup.Count <= slot) lastMarkup.Add("");
            if (lastMarkup[slot] == markup) return false;

            lastMarkup[slot] = markup;
            elem.SetNewText(markup, font);
            return true;
        }
    }
}
