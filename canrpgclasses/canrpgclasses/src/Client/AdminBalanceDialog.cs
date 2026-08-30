using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Client;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Net;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Admin-only live balance editor, listing every number <see cref="BalanceConfig"/> knows about straight from
    /// its dictionaries. Nothing is applied locally - the server validates, persists and broadcasts, and an
    /// explicit Apply batches the edits so a keystroke doesn't cost a registry rebuild.
    /// </summary>
    public class AdminBalanceDialog : CanrpgDialog
    {
        private const string ComposerKey = "canrpgbalanceadmin";

        private const double ListWidth = 260;
        private const double PanelWidth = 380;
        private const double PanelHeight = 340;
        private const double RowH = 24;

        /// <summary>Key rows shown at once. The value column is paged rather than scrolled on purpose:
        /// GuiElementTextInput.RenderInteractiveElements sets its own GL scissor and then clears the scissor flag
        /// entirely, so a number input inside a BeginClip container draws its text outside the clip (values
        /// smeared down the screen). Paging keeps every input inside the dialog, where the engine can draw it.</summary>
        private const int KeysPerPage = 8;

        private enum Tab { Spells, Talents, Affinities, Globals }

        private Tab tab = Tab.Spells;
        private string filter = "";
        private string? selectedId;
        private string newKeyName = "";
        private float newKeyValue;
        private int keyPage;

        /// <summary>Values typed since the last Apply, by config key. Empty means nothing to send.</summary>
        private readonly Dictionary<string, float> pending = new();

        private ElementBounds listContainerBounds = null!;
        private ElementBounds listClipBounds = null!;

        /// <summary>Keys drawn by the last <see cref="Compose"/>, in row order - their number inputs are filled in
        /// after Compose (a composer element only exists once composed).</summary>
        private readonly List<string> visibleKeys = new();

        public AdminBalanceDialog(ICoreClientAPI capi) : base(capi) { }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Compose();
        }

        private static IEnumerable<(string id, string label)> SpellEntries(canrpgclassesModSystem? mod) =>
            mod == null
                ? Array.Empty<(string, string)>()
                : mod.Spells.All.Values.Select(s => (s.LocalId, s.DisplayName)).OrderBy(e => e.Item2);

        private static IEnumerable<(string id, string label)> TalentEntries(canrpgclassesModSystem? mod) =>
            mod == null
                ? Array.Empty<(string, string)>()
                : mod.Talents.All.Values.Select(t => (t.Id, $"{t.DisplayName} ({t.Id})")).OrderBy(e => e.Item2);

        private static IEnumerable<(string id, string label)> AffinityEntries(canrpgclassesModSystem? mod)
        {
            if (mod == null) yield break;
            foreach (var cls in mod.Classes.All.Values)
                foreach (var aff in cls.GearAffinities)
                {
                    string desc = aff.ResolvedDescription;
                    yield return (cls.Id + ":" + aff.Key,
                        cls.Id + ": " + (string.IsNullOrEmpty(desc) ? aff.Key : desc));
                }
        }

        private List<(string id, string label)> CurrentEntries()
        {
            var mod = canrpgclassesModSystem.ClientInstance;
            IEnumerable<(string id, string label)> all = tab switch
            {
                Tab.Spells => SpellEntries(mod),
                Tab.Talents => TalentEntries(mod),
                Tab.Affinities => AffinityEntries(mod),
                _ => Array.Empty<(string, string)>()
            };
            return all.Where(e => filter.Length == 0 ||
                                  e.label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        private BalanceConfig.BalanceCategory Category => tab switch
        {
            Tab.Spells => BalanceConfig.BalanceCategory.Spell,
            Tab.Talents => BalanceConfig.BalanceCategory.Talent,
            Tab.Affinities => BalanceConfig.BalanceCategory.Affinity,
            _ => BalanceConfig.BalanceCategory.Global
        };

        private IReadOnlyDictionary<string, float> CurrentPairs()
        {
            if (tab == Tab.Globals) return BalanceConfig.GlobalPairs();
            if (string.IsNullOrEmpty(selectedId)) return new Dictionary<string, float>();
            return tab switch
            {
                Tab.Spells => BalanceConfig.SpellPairs(selectedId!),
                Tab.Talents => BalanceConfig.TalentPairs(selectedId!),
                _ => BalanceConfig.AffinityPairs(selectedId!)
            };
        }

        /// <summary>The id the packets carry: globals live under an empty id.</summary>
        private string EditId => tab == Tab.Globals ? "" : selectedId ?? "";

        /// <summary>The key rows to show, sorted. On the Globals tab the filter box has no id list to narrow, so it
        /// filters the key names instead - the fastest way to reach one number among the ~50 globals.</summary>
        private List<KeyValuePair<string, float>> CurrentKeys()
        {
            var pairs = CurrentPairs().OrderBy(p => p.Key).AsEnumerable();
            if (tab == Tab.Globals && filter.Length > 0)
                pairs = pairs.Where(p => p.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            return pairs.ToList();
        }

        private int PageCount(int keyCount) => Math.Max(1, (keyCount + KeysPerPage - 1) / KeysPerPage);

        private void Compose()
        {
            var entries = CurrentEntries();
            if (tab != Tab.Globals && (selectedId == null || entries.All(e => e.id != selectedId)))
                selectedId = entries.Count > 0 ? entries[0].id : null;

            var tabs = new[]
            {
                new GuiTab { Name = "Spells", DataInt = 0 },
                new GuiTab { Name = "Talents", DataInt = 1 },
                new GuiTab { Name = "Affinities", DataInt = 2 },
                new GuiTab { Name = "Globals", DataInt = 3 },
            };

            var tabBounds = ElementBounds.Fixed(0, 0, ListWidth + PanelWidth + 20, 28);
            var filterBounds = ElementBounds.Fixed(0, 36, ListWidth, RowH);

            listClipBounds = ElementBounds.Fixed(0, 36 + RowH + 6, ListWidth, PanelHeight - RowH - 6);
            listContainerBounds = listClipBounds.ForkContainingChild(0, 0, 0, -3);
            var listScrollBounds = listClipBounds.CopyOffsetedSibling(ListWidth + 3, 0, 0, 0).WithFixedWidth(20);

            double panelX = ListWidth + 40;
            var headerBounds = ElementBounds.Fixed(panelX, 36, PanelWidth, RowH);

            double keysY = 36 + RowH + 6;
            double keysH = KeysPerPage * (RowH + 4) + 6;
            var keysAreaBounds = ElementBounds.Fixed(panelX, keysY, PanelWidth, keysH);

            double navY = keysY + keysH + 8;
            double addY = navY + RowH + 8;
            var newNameBounds = ElementBounds.Fixed(panelX, addY, 170, RowH);
            var newValueBounds = ElementBounds.Fixed(panelX + 178, addY, 90, RowH);
            var addBounds = ElementBounds.Fixed(panelX + 276, addY, 104, RowH);
            var applyBounds = ElementBounds.Fixed(panelX, addY + RowH + 8, PanelWidth, RowH);

            var bgBounds = ElementBounds.Fixed(0, 0, ListWidth + PanelWidth + 100, PanelHeight + 130);

            ClearComposers();
            var compo = capi.Gui.CreateCompo(ComposerKey,
                    ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
                .AddShadedDialogBG(bgBounds, true, 5.0, 0.75f)
                .AddDialogTitleBar("RpgClasses Balance Editor", () => TryClose())
                .BeginChildElements(ElementBounds.Fixed(20, 30, ListWidth + PanelWidth + 60, PanelHeight + 90))
                    .AddHorizontalTabs(tabs, tabBounds, OnTabClicked,
                        CairoFont.WhiteSmallText(), CairoFont.WhiteSmallText().WithColor(GuiStyle.ActiveButtonTextColor), "tabs");

            // The filter box narrows the id list on the entry tabs; on Globals (no id list) it narrows the key names.
            compo.AddTextInput(filterBounds, OnFilterChanged, CairoFont.WhiteSmallText(), "filter");

            if (tab != Tab.Globals)
            {
                compo.AddInset(listClipBounds.FlatCopy().FixedGrow(3), 3, 0.85f)
                    .BeginClip(listClipBounds)
                        .AddContainer(listContainerBounds, "idlist")
                    .EndClip()
                    .AddVerticalScrollbar(v => OnScroll(listContainerBounds, listClipBounds, v), listScrollBounds, "idscroll");

                FillIdList(compo);
            }

            var keys = CurrentKeys();
            int pages = PageCount(keys.Count);
            keyPage = Math.Clamp(keyPage, 0, pages - 1);

            compo.AddStaticText(tab == Tab.Globals ? "Global values" : selectedId ?? "Select an entry on the left",
                    CairoFont.WhiteSmallText(), headerBounds)
                .AddInset(keysAreaBounds.FlatCopy().FixedGrow(3), 3, 0.85f);

            AddKeyRows(compo, keys, panelX, keysY);

            compo.AddSmallButton("<", () => ChangePage(-1), ElementBounds.Fixed(panelX, navY, 40, RowH))
                .AddStaticText($"Page {keyPage + 1}/{pages}  ({keys.Count} keys)", CairoFont.WhiteDetailText(),
                    ElementBounds.Fixed(panelX + 48, navY + 4, PanelWidth - 100, RowH))
                .AddSmallButton(">", () => ChangePage(1), ElementBounds.Fixed(panelX + PanelWidth - 40, navY, 40, RowH))
                .AddTextInput(newNameBounds, v => newKeyName = v, CairoFont.WhiteSmallText(), "newkey")
                .AddNumberInput(newValueBounds, v => float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out newKeyValue),
                    CairoFont.WhiteSmallText(), "newval")
                .AddSmallButton("Add key", AddKey, addBounds)
                .AddSmallButton("Apply changes", ApplyPending, applyBounds);

            SingleComposer = compo.EndChildElements().Compose();

            SingleComposer.GetHorizontalTabs("tabs").activeElement = (int)tab;

            // Fill the value inputs now that the elements exist. An unsent edit (pending) wins over the stored
            // value, so paging away and back doesn't silently drop what the admin typed.
            var stored = keys.ToDictionary(k => k.Key, k => k.Value);
            foreach (string key in visibleKeys)
            {
                if (!pending.TryGetValue(key, out float shown)) stored.TryGetValue(key, out shown);
                SingleComposer.GetNumberInput(InputKey(key)).SetValue(shown.ToString(CultureInfo.InvariantCulture));
            }

            if (tab != Tab.Globals)
            {
                listContainerBounds.CalcWorldBounds();
                listClipBounds.CalcWorldBounds();
                SingleComposer.GetScrollbar("idscroll")
                    .SetHeights((float)listClipBounds.fixedHeight, (float)listContainerBounds.fixedHeight);
            }

            var filterInput = SingleComposer.GetTextInput("filter");
            filterInput.SetValue(filter);
            // Typing in the filter rebuilds the dialog, which means rebuilding this element too - put the caret
            // back so the admin can keep typing.
            if (filter.Length > 0) SingleComposer.FocusElement(filterInput.TabIndex);
        }

        private static void OnScroll(ElementBounds container, ElementBounds clip, float value)
        {
            container.fixedY = 3 - value;
            container.CalcWorldBounds();
        }

        private void FillIdList(GuiComposer compo)
        {
            var container = compo.GetContainer("idlist");
            container.Clear();

            var rowBounds = ElementBounds.Fixed(0, 0, ListWidth - 12, RowH);
            foreach (var (id, label) in CurrentEntries())
            {
                string captured = id;
                container.Add(new GuiElementTextButton(capi, label, CairoFont.WhiteDetailText(),
                    CairoFont.WhiteDetailText().WithColor(GuiStyle.ActiveButtonTextColor),
                    () => SelectId(captured), rowBounds, EnumButtonStyle.Small));
                rowBounds = rowBounds.BelowCopy(0, 2);
            }
        }

        /// <summary>Composer element key for one value input. Unique per config key, so <see cref="Compose"/> can
        /// fetch it back after composing.</summary>
        private static string InputKey(string configKey) => "val_" + configKey;

        /// <summary>Adds the current page's key rows STRAIGHT into the composer (not into a clipped container):
        /// only a composer-owned input gets keyboard focus - a GuiElementContainer drops key events unless it is
        /// itself focusable, which it isn't by default, so inputs nested in one can be clicked but never typed in.</summary>
        private void AddKeyRows(GuiComposer compo, List<KeyValuePair<string, float>> keys, double panelX, double keysY)
        {
            visibleKeys.Clear();

            if (keys.Count == 0)
            {
                compo.AddStaticText(tab == Tab.Globals && filter.Length > 0
                        ? "No key matches the filter."
                        : "No numbers configured for this entry yet.",
                    CairoFont.WhiteDetailText(), ElementBounds.Fixed(panelX + 6, keysY + 4, PanelWidth - 20, RowH));
                return;
            }

            int start = keyPage * KeysPerPage;
            for (int i = start; i < Math.Min(start + KeysPerPage, keys.Count); i++)
            {
                string key = keys[i].Key;
                double y = keysY + (i - start) * (RowH + 4);

                compo.AddStaticText(key, CairoFont.WhiteDetailText(),
                        ElementBounds.Fixed(panelX + 6, y + 4, 210, RowH))
                    .AddNumberInput(ElementBounds.Fixed(panelX + 226, y, 120, RowH),
                        v => OnValueTyped(key, v), CairoFont.WhiteDetailText(), InputKey(key));

                visibleKeys.Add(key);
            }
        }

        private bool ChangePage(int delta)
        {
            keyPage += delta; // clamped in Compose, where the filtered key count is known
            Compose();
            return true;
        }

        private void OnTabClicked(int index)
        {
            tab = (Tab)index;
            selectedId = null;
            filter = "";
            newKeyName = "";
            newKeyValue = 0;
            keyPage = 0;
            pending.Clear(); // another tab is another category - unsent edits can't be carried across
            Compose();
        }

        private void OnFilterChanged(string value)
        {
            if (value == filter) return;
            filter = value;
            keyPage = 0;
            Compose();
        }

        private bool SelectId(string id)
        {
            selectedId = id;
            newKeyName = "";
            newKeyValue = 0;
            keyPage = 0;
            pending.Clear(); // pending is keyed by config key only - it belongs to the entry it was typed in
            Compose();
            return true;
        }

        private void OnValueTyped(string key, string text)
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) pending[key] = v;
            else pending.Remove(key);
        }

        private bool AddKey()
        {
            if (!string.IsNullOrWhiteSpace(newKeyName))
            {
                Send(newKeyName.Trim(), newKeyValue);
                newKeyName = "";
                newKeyValue = 0;
                SingleComposer.GetTextInput("newkey").SetValue("");
                SingleComposer.GetNumberInput("newval").SetValue("0");
            }
            return true;
        }

        private bool ApplyPending()
        {
            // Snapshot: each packet round-trips into a registry rebuild, which can change what CurrentPairs sees.
            foreach (var kv in pending.ToList()) Send(kv.Key, kv.Value);
            pending.Clear();
            // Re-read once the round trip (server applies -> persists -> broadcasts the merged config) has landed,
            // so the fields show the CONFIRMED numbers rather than what was typed.
            capi.Event.RegisterCallback(_ => { if (IsOpened()) Compose(); }, 400);
            return true;
        }

        private void Send(string key, float value)
            => canrpgclassesModSystem.ClientInstance?.ClientChannel?.SendPacket(new BalanceEditPacket
            {
                Category = (int)Category,
                Id = EditId,
                Key = key,
                Value = value
            });
    }
}
