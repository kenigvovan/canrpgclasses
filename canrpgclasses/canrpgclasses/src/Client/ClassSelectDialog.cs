using System.Collections.Generic;
using System.Linq;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using canrpgclasses.Client.Gui;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Economy;
using canrpgclasses.Core.Net;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Class picker / changer: lists the registered classes and shows what a change will cost, then sends a
    /// <see cref="SelectClassPacket"/> - the server enforces the re-pick policy. Also opens automatically for a
    /// player who has not picked yet.
    /// </summary>
    public class ClassSelectDialog : CanrpgDialog
    {
        private const string ComposerKey = "canrpgclassselect";

        private const double ListWidth = 300;
        private const double PreviewWidth = 400;
        private const double RowH = 44;
        private const double IconSize = 36;

        private static readonly double[] Gold = { 0.95, 0.88, 0.55, 1 };
        private static readonly double[] Accent = { 0.55, 0.82, 1.00, 1 };
        private static readonly double[] Muted = { 0.68, 0.66, 0.62, 1 };

        private static readonly double[] PlateCurrent = { 0.30, 0.26, 0.10, 0.85 };
        private static readonly double[] PlateSelected = { 0.14, 0.22, 0.32, 0.85 };

        /// <summary>Icon tint for a class the player's vanilla character class bars them from (ClassRestrictions).</summary>
        private static readonly double[] LockedTint = { 0.45, 0.42, 0.40, 0.55 };

        /// <summary>Shrinks a font until the text fits one line of the given (unscaled) width. A list row has a
        /// fixed height, so a wrapped caption would run over the row below - which the "(current)" and
        /// "(unavailable)" tags made easy to hit. Vanilla's AddStaticTextAutoFontSize measures bounds the
        /// composer has not calculated at that point, so the width is passed in instead.</summary>
        private static CairoFont Fitted(CairoFont font, string text, double unscaledWidth)
        {
            double max = ElementBounds.scaled(unscaledWidth);
            double w = font.GetTextExtents(text).Width;
            if (w > max && w > 0) font.UnscaledFontsize *= max / w;
            return font;
        }

        private static void Rule(Context ctx, ImageSurface surface, ElementBounds bounds)
        {
            ctx.SetSourceRGBA(GuiStyle.DialogBorderColor[0], GuiStyle.DialogBorderColor[1],
                GuiStyle.DialogBorderColor[2], 0.65);
            ctx.LineWidth = 1;
            ctx.MoveTo(bounds.drawX, bounds.drawY);
            ctx.LineTo(bounds.drawX + bounds.OuterWidth, bounds.drawY);
            ctx.Stroke();
        }

        private string selected = "";
        private List<RpgClassDef> classes = new();
        private List<string> previewSpellIds = new();
        private long listenerId;

        public ClassSelectDialog(ICoreClientAPI capi) : base(capi)
        {
            // The cost hint counts down (re-pick cooldown), so it is refreshed rather than left stale.
            listenerId = capi.Event.RegisterGameTickListener(_ => RefreshCostHint(), 1000);
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            var entity = capi.World?.Player?.Entity;
            if (entity != null && string.IsNullOrEmpty(selected)) selected = TalentState.CurrentClass(entity);
            Compose();
        }

        private void RefreshCostHint()
        {
            if (!IsOpened()) return;
            var entity = capi.World?.Player?.Entity;
            if (entity == null) return;
            SingleComposer.GetDynamicText("costhint")?.SetNewText(CostHint(entity));
        }

        private void Compose()
        {
            var mod = canrpgclassesModSystem.ClientInstance;
            var entity = capi.World?.Player?.Entity;
            if (mod == null || entity == null) return;

            classes = mod.Classes.All.Values.ToList();
            string current = TalentState.CurrentClass(entity);
            bool chosen = ClassChange.HasChosen(entity);
            var cls = string.IsNullOrEmpty(selected) ? null : mod.Classes.Get(selected);

            double totalW = ListWidth + PreviewWidth + 20;
            double listH = classes.Count * RowH;
            double contentH = System.Math.Max(listH, 460);

            const double headerH = 26;
            double y0 = headerH + 14;                       // below the header and its rule
            double previewX = ListWidth + 20;

            var headerBounds = ElementBounds.Fixed(0, 0, totalW, headerH);
            var headerRuleBounds = ElementBounds.Fixed(0, headerH + 4, totalW, 2);
            var listPanelBounds = ElementBounds.Fixed(-6, y0 - 6, ListWidth + 12, contentH + 12);
            var previewPanelBounds = ElementBounds.Fixed(previewX - 6, y0 - 6, PreviewWidth + 12, contentH + 12);
            var gridBounds = ElementBounds.Fixed(0, y0, IconSize, listH);

            double footerY = y0 + contentH + 14;
            var footerRuleBounds = ElementBounds.Fixed(0, footerY - 8, totalW, 2);
            var costBounds = ElementBounds.Fixed(0, footerY, totalW, 24);
            var confirmBounds = ElementBounds.Fixed(0, footerY + 28, 200, 26);

            var bgBounds = ElementBounds.Fixed(0, 0, totalW + 40, footerY + 100);

            ClearComposers();
            var compo = capi.Gui.CreateCompo(ComposerKey,
                    ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
                .AddShadedDialogBG(bgBounds, true, 5.0, 0.75f)
                .AddDialogTitleBar(Lang.Get("canrpgclasses:ui-classselect-title"), () => TryClose())
                // Below the title bar: at y=30 the header line ran under it.
                .BeginChildElements(ElementBounds.Fixed(20, GuiStyle.TitleBarHeight + 12, totalW, footerY + 60))
                    .AddStaticText(chosen
                            ? Lang.Get("canrpgclasses:ui-classselect-current", mod.Classes.Get(current)?.DisplayName ?? current)
                            : Lang.Get("canrpgclasses:ui-classselect-nopick"),
                        CairoFont.WhiteSmallishText().WithColor(Gold), headerBounds)
                    .AddStaticCustomDraw(headerRuleBounds, Rule)
                    .AddInset(listPanelBounds, 3, 0.85f)
                    .AddInset(previewPanelBounds, 3, 0.85f)
                    .AddIconGrid(gridBounds, 1, classes.Count, IconSize, 0, RowH - IconSize, "classgrid");

            // The class name and role sit next to each grid cell rather than inside it - a two-line caption is
            // wider than the icon and would not fit the cell's own label slot.
            for (int i = 0; i < classes.Count; i++)
            {
                var c = classes[i];
                bool isCurrent = chosen && c.Id == current;
                bool isSelected = c.Id == selected;
                bool locked = IsLocked(entity, c.Id);
                string name = c.DisplayName
                    + (isCurrent ? "  " + Lang.Get("canrpgclasses:ui-classselect-currenttag") : "")
                    + (locked ? "  " + Lang.Get("canrpgclasses:ui-classselect-lockedtag") : "");
                double y = y0 + i * RowH;
                double capW = ListWidth - IconSize - 14;
                compo.AddStaticText(name,
                    Fitted(CairoFont.WhiteSmallishText().WithColor(locked ? GuiStyle.DisabledTextColor
                        : isCurrent ? Gold : isSelected ? Accent : GuiStyle.DialogDefaultTextColor), name, capW),
                    ElementBounds.Fixed(IconSize + 10, y, capW, 22));
                if (!string.IsNullOrEmpty(c.Role))
                    compo.AddStaticText(c.Role, Fitted(CairoFont.WhiteDetailText().WithColor(Muted), c.Role, capW),
                        ElementBounds.Fixed(IconSize + 10, y + 21, capW, 18));
            }

            // ---- preview ----
            double py = y0;
            if (cls == null)
            {
                // No class highlighted: nothing to preview, and no skill grid composed for FillSpellGrid to fill.
                previewSpellIds.Clear();
                compo.AddStaticText(Lang.Get("canrpgclasses:ui-classselect-previewhint"),
                    CairoFont.WhiteDetailText().WithColor(Muted),
                    ElementBounds.Fixed(previewX, py, PreviewWidth, 24));
            }
            else
            {
                compo.AddStaticText(cls.DisplayName, CairoFont.WhiteMediumText().WithColor(Gold),
                    ElementBounds.Fixed(previewX, py, PreviewWidth, 30));
                py += 30;
                if (!string.IsNullOrEmpty(cls.Role))
                {
                    compo.AddStaticText(cls.Role, CairoFont.WhiteSmallText().WithColor(Accent),
                        ElementBounds.Fixed(previewX, py, PreviewWidth, 20));
                    py += 22;
                }
                compo.AddStaticCustomDraw(ElementBounds.Fixed(previewX, py, PreviewWidth, 2), Rule);
                py += 8;

                if (!string.IsNullOrEmpty(cls.Description))
                {
                    // Richtext, because it is the only vanilla text element that wraps.
                    compo.AddRichtext(cls.Description, CairoFont.WhiteDetailText(),
                        ElementBounds.Fixed(previewX, py, PreviewWidth, 150));
                    py += 156;
                }

                string res = cls.PrimaryResource != null ? cls.PrimaryResource.Id : "-";
                if (cls.UsesComboPoints) res += " + " + Lang.Get("canrpgclasses:ui-classselect-combo");
                compo.AddStaticText(Lang.Get("canrpgclasses:ui-classselect-resource"),
                    CairoFont.WhiteDetailText().WithColor(Muted), ElementBounds.Fixed(previewX, py, 130, 20));
                compo.AddStaticText(res, CairoFont.WhiteDetailText(),
                    ElementBounds.Fixed(previewX + 134, py, PreviewWidth - 134, 20));
                py += 24;

                compo.AddStaticText(Lang.Get("canrpgclasses:ui-classselect-trees"),
                    CairoFont.WhiteDetailText().WithColor(Muted), ElementBounds.Fixed(previewX, py, 130, 20));
                compo.AddStaticText(string.Join(", ", cls.TreeNames), CairoFont.WhiteDetailText(),
                    ElementBounds.Fixed(previewX + 134, py, PreviewWidth - 134, 20));
                py += 28;

                compo.AddStaticText(Lang.Get("canrpgclasses:ui-classselect-startskills"),
                    CairoFont.WhiteSmallText().WithColor(Accent), ElementBounds.Fixed(previewX, py, PreviewWidth, 20));
                py += 20;
                compo.AddStaticCustomDraw(ElementBounds.Fixed(previewX, py, PreviewWidth, 2), Rule);
                py += 8;

                previewSpellIds = cls.BaseSpells.ToList();
                // Wrap the starting skills instead of forcing them onto one row - a class with many of them would
                // otherwise run past the panel.
                const double skillCell = 34, skillPad = 6;
                int cols = System.Math.Max(1, (int)((PreviewWidth + skillPad) / (skillCell + skillPad)));
                int rows = System.Math.Max(1, (previewSpellIds.Count + cols - 1) / cols);
                compo.AddIconGrid(ElementBounds.Fixed(previewX, py, PreviewWidth, rows * (skillCell + skillPad)),
                    cols, rows, skillCell, 0, skillPad, "spellgrid");
            }

            compo.AddStaticCustomDraw(footerRuleBounds, Rule)
                .AddDynamicText(CostHint(entity), CairoFont.WhiteDetailText().WithColor(Muted), costBounds, "costhint");

            bool canAct = !string.IsNullOrEmpty(selected) && !(chosen && selected == current) && !IsLocked(entity, selected);
            string verb = Lang.Get(chosen ? "canrpgclasses:ui-classselect-change" : "canrpgclasses:ui-classselect-confirm");
            if (canAct) compo.AddSmallButton(verb, Confirm, confirmBounds);
            else compo.AddStaticText(verb, CairoFont.WhiteSmallText().WithColor(GuiStyle.DisabledTextColor), confirmBounds);

            compo.AddSpellTooltip(ElementBounds.Fixed(0, 0, 1, 1), "tooltip");

            SingleComposer = compo.EndChildElements().Compose();

            FillClassGrid(current, chosen);
            FillSpellGrid();
        }

        /// <summary>Whether the server's vanilla-class rules bar this player from that class. The rules are the
        /// server's copy, pushed on join - if a client somehow has none, nothing is greyed out and the server's
        /// own check is what refuses the pick.</summary>
        private bool IsLocked(Entity? entity, string classId) =>
            !string.IsNullOrEmpty(classId) && !ClassRestrictions.Allowed(entity, classId, out _);

        private void FillClassGrid(string current, bool chosen)
        {
            var grid = SingleComposer.GetIconGrid("classgrid");
            if (grid == null) return;
            var icons = canrpgclassesModSystem.ClientInstance?.Icons;
            var entity = capi.World?.Player?.Entity;

            grid.Cells = classes.Select(c => new IconGridCell
            {
                Id = c.Id,
                Icon = icons?.GetTex(IconLoader.PathFor(c.IconName, c.Id)),
                IconFallback = c.DisplayName.Length > 0 ? c.DisplayName.Substring(0, 1) : "?",
                FrameColor = chosen && c.Id == current ? Gold : c.Id == selected ? Accent : null,
                PlateColor = chosen && c.Id == current ? PlateCurrent : c.Id == selected ? PlateSelected : null,
                IconTint = IsLocked(entity, c.Id) ? LockedTint : null,
                Emphasize = c.Id == selected
            }).ToList();

            grid.SelectedIndex = classes.FindIndex(c => c.Id == selected);
            grid.OnCellClick = (index, _) => SelectClass(index);
            grid.OnCellDoubleClick = SelectClass;
        }

        private void SelectClass(int index)
        {
            if (index < 0 || index >= classes.Count) return;
            selected = classes[index].Id;
            Compose();
        }

        private void FillSpellGrid()
        {
            var mod = canrpgclassesModSystem.ClientInstance;
            if (mod == null || previewSpellIds.Count == 0) return;

            var grid = SingleComposer.GetIconGrid("spellgrid");
            if (grid == null) return;

            var icons = mod.Icons;
            grid.Cells = previewSpellIds.Select(id =>
            {
                mod.Spells.TryGet(id, out var spell);
                string iconName = spell != null && !string.IsNullOrEmpty(spell.IconName) ? spell.IconName! : Local(id);
                return new IconGridCell
                {
                    Id = id,
                    Icon = icons?.GetTex(IconLoader.PathFor(iconName)),
                    IconFallback = "?"
                };
            }).ToList();

            var tooltip = SingleComposer.GetSpellTooltip("tooltip");
            grid.OnCellHover = index =>
            {
                if (index < 0 || index >= previewSpellIds.Count) { tooltip.Clear(); return; }
                string id = previewSpellIds[index];
                if (!mod.Spells.TryGet(id, out var spell) || spell == null) { tooltip.Clear(); return; }
                tooltip.SetLines(id, SpellTooltip.BuildLines(capi.World?.Player?.Entity, spell));
            };
        }

        private static string Local(string id)
        {
            int c = id.IndexOf(':');
            return c >= 0 ? id.Substring(c + 1) : id;
        }

        private bool Confirm()
        {
            canrpgclassesModSystem.ClientInstance?.ClientChannel?.SendPacket(
                new SelectClassPacket { ClassId = selected });
            return true;
        }

        /// <summary>What changing will cost, per the synced policy. The first pick is always free.</summary>
        private string CostHint(Entity entity)
        {
            // A barred class is refused whatever the pick would cost, so say that instead of the price - and name
            // the axis that blocked it (character class or race), since the two are fixed for different reasons.
            if (!string.IsNullOrEmpty(selected) && !ClassRestrictions.Allowed(entity, selected, out string why))
                return why == ClassRestrictions.ReasonRace
                    ? Lang.Get("canrpgclasses:ui-classselect-locked-race", RaceName(entity))
                    : Lang.Get("canrpgclasses:ui-classselect-locked", VanillaClassName(entity));

            if (!ClassChange.HasChosen(entity)) return Lang.Get("canrpgclasses:ui-classselect-firstfree");
            if (entity.WatchedAttributes.GetBool(ClassChange.FreeKey, false))
                return Lang.Get("canrpgclasses:ui-classselect-freegrant");

            switch (ClassChange.Mode(capi))
            {
                case ReselectMode.Unrestricted:
                    return Lang.Get("canrpgclasses:ui-classselect-free");
                case ReselectMode.Cooldown:
                    long rem = ClassChange.CooldownRemainingMs(entity, capi);
                    return rem > 0
                        ? Lang.Get("canrpgclasses:ui-classselect-cooldown-active", FormatDuration(rem))
                        : Lang.Get("canrpgclasses:ui-classselect-cooldown-ready", ClassChange.CooldownDays(capi));
                case ReselectMode.AdminOnly:
                    return Lang.Get("canrpgclasses:ui-classselect-adminonly");
                case ReselectMode.Fee:
                    string txt = Lang.Get("canrpgclasses:ui-classselect-fee", ClassChange.Fee(capi).ToString("0"));
                    if (!EconomyBridge.Available(capi)) txt += " " + Lang.Get("canrpgclasses:ui-classselect-noeconomy");
                    return txt;
                default:
                    return "";
            }
        }

        /// <summary>The player's vanilla character class, translated. Vanilla names live under the game domain's
        /// <c>characterclass-&lt;code&gt;</c>; a class added by another mod may have no such entry, in which case the
        /// bare code is more useful than the raw key.</summary>
        private static string VanillaClassName(Entity entity)
        {
            string code = ClassRestrictions.VanillaClassOf(entity);
            if (code.Length == 0) return "-";
            string key = "characterclass-" + code;
            return Lang.HasTranslation(key) ? Lang.Get(key) : code;
        }

        /// <summary>The player's race (PlayerModelLib model code), translated when the race mod ships a lang entry
        /// under that code - there is no agreed key for model names, so the bare model name is the fallback.</summary>
        private static string RaceName(Entity entity)
        {
            string code = ClassRestrictions.RaceOf(entity);
            if (code.Length == 0) return "-";
            return Lang.GetIfExists(code) ?? Local(code);
        }

        private static string FormatDuration(long ms)
        {
            long s = ms / 1000;
            if (s >= 86400) return $"{s / 86400}d {(s % 86400) / 3600}h";
            if (s >= 3600) return $"{s / 3600}h {(s % 3600) / 60}m";
            if (s >= 60) return $"{s / 60}m";
            return $"{s}s";
        }

        public override void Dispose()
        {
            base.Dispose();
            if (listenerId != 0) { capi.Event.UnregisterGameTickListener(listenerId); listenerId = 0; }
        }
    }
}
