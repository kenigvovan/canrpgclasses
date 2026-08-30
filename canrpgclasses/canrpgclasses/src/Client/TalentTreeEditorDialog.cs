using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Client;
using canrpgclasses.Core.Content;
using canrpgclasses.Core.Net;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Admin-only talent-tree editor; the whole tree goes out as one <see cref="TalentTreeEditPacket"/> on Save
    /// and nothing is applied locally. Code-defined talents are drawn but locked - their behaviour lives in a C#
    /// hook a JSON entry can't carry, so an overwrite would quietly disable them.
    /// </summary>
    public class TalentTreeEditorDialog : CanrpgDialog
    {
        private const string ComposerKey = "canrpgtreeeditor";

        // The grid is only what the editor draws - a tree can hold more, so the size grows to fit what is there.
        private const int MinTiers = 7;
        private const int MinColumns = 4;
        private const double CellW = 132;
        /// <summary>Characters a cell caption fits. AddSmallButton draws in a fixed font that can't be scaled
        /// down, so the caption is cut to size instead of being allowed to run past the button.</summary>
        private const int CellChars = 15;
        private const double CellH = 34;
        private const double RowH = 24;
        private const double PanelW = 300;
        /// <summary>Height the field panel needs when a talent is selected: seven label+input blocks plus the
        /// Delete/Save rows and a line of status.</summary>
        private const double PanelHeight = 7 * (20 + RowH + 8) + 3 * (RowH + 10) + 40;

        private string classId = "";
        private int treeIndex;

        /// <summary>The tree being edited: data talents only, since those are the ones a save may replace.</summary>
        private readonly List<TalentModel> working = new();
        /// <summary>Code-defined talents in the same tree, drawn as locked cells.</summary>
        private readonly List<Talent> locked = new();

        private int selTier = -1, selColumn = -1;
        /// <summary>Whether the stat dropdown sits on "Type a key...", which reveals the free-text field.</summary>
        private bool customStat;
        /// <summary>Last number typed in the per-rank field, held until a stat is picked to attach it to.</summary>
        private float typedStatValue;
        /// <summary>A save of ours is in flight - the next content sync is its result.</summary>
        private bool awaitingSync;
        /// <summary>Source of the "copy an existing tree" pair of dropdowns.</summary>
        private string? copyClassId;
        private int copyTree;
        private int tiers = MinTiers, columns = MinColumns;
        private string status = "";

        public TalentTreeEditorDialog(ICoreClientAPI capi) : base(capi) { }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            if (string.IsNullOrEmpty(classId)) classId = ClassIds().FirstOrDefault() ?? "";
            Reload();
            Compose();
        }

        private static canrpgclassesModSystem? Mod => canrpgclassesModSystem.ClientInstance;

        private static List<string> ClassIds()
            => Mod?.Classes.All.Values.Select(c => c.Id).OrderBy(id => id).ToList() ?? new List<string>();

        private int TreeCount() => Math.Max(1, Mod?.Classes.Get(classId)?.TreeCount ?? 1);

        /// <summary>Re-reads the tree from the registries - the state the server last confirmed. Any unsaved edit
        /// is dropped on purpose: after a save the registries ARE the truth, and before one there is nothing worth
        /// keeping that the admin didn't just abandon by switching trees.</summary>
        private void Reload()
        {
            working.Clear();
            locked.Clear();
            selTier = selColumn = -1;

            var mod = Mod;
            if (mod == null || string.IsNullOrEmpty(classId)) return;

            foreach (var t in mod.Talents.ForClass(classId))
            {
                if (t.TreeIndex != treeIndex) continue;
                if (t is ContentTalent) working.Add(ToModel(t));
                else locked.Add(t);
            }

            tiers = MinTiers;
            columns = MinColumns;
            foreach (var t in working) { tiers = Math.Max(tiers, t.tier + 1); columns = Math.Max(columns, t.column + 1); }
            foreach (var t in locked) { tiers = Math.Max(tiers, t.Tier + 1); columns = Math.Max(columns, t.Column + 1); }
        }

        private TalentModel ToModel(Talent t)
        {
            var m = new TalentModel
            {
                id = t.Id,
                @class = t.ClassId,
                tree = t.TreeIndex,
                tier = t.Tier,
                column = t.Column,
                maxRank = t.MaxRank,
                name = t.DisplayName,
                description = t.Description,
                icon = t.IconName,
                requires = t.RequiresTalent,
                grantsSpell = t.GrantsSpellId
            };
            // Stats aren't a Talent property (they live in ApplyStats), so they are read back off the content
            // talent itself - otherwise saving a talent would silently blank whatever stats it had.
            if (t is ContentTalent ct && ct.Stats.Count > 0)
                m.stats = ct.Stats.Select(s => new TalentStatModel { stat = s.Stat, perRank = s.PerRank }).ToList();
            return m;
        }

        private TalentModel? At(int tier, int column)
            => working.FirstOrDefault(t => t.tier == tier && t.column == column);

        private Talent? LockedAt(int tier, int column)
            => locked.FirstOrDefault(t => t.Tier == tier && t.Column == column);

        private TalentModel? Selected => selTier < 0 ? null : At(selTier, selColumn);

        /// <summary>Dropdown entry standing for "nothing" - a talent may grant no spell, need no prerequisite and
        /// set no stat.</summary>
        private const string None = " none";

        /// <summary>Every spell, plus the "none" entry. The label carries the id too, since that is what the
        /// talent stores and what a content file would name.</summary>
        private static List<(string id, string name)> SpellChoices()
        {
            var list = new List<(string, string)> { (None, "(none)") };
            var mod = Mod;
            if (mod != null)
                foreach (var s in mod.Spells.All.Values.OrderBy(s => s.DisplayName))
                    list.Add((s.Id, $"{s.DisplayName}  ({s.LocalId})"));
            return list;
        }

        /// <summary>Talents that may act as this one's prerequisite: the same tree, minus itself. Cross-tree
        /// prerequisites aren't offered - a tree is meant to be climbable on its own.</summary>
        private List<(string id, string name)> RequirementChoices(TalentModel self)
        {
            var list = new List<(string, string)> { (None, "(none)") };
            foreach (var t in working)
                if (t != self && !string.IsNullOrEmpty(t.id))
                    list.Add((t.id!, string.IsNullOrEmpty(t.name) ? t.id! : $"{t.name}  ({t.id})"));
            foreach (var t in locked)
                list.Add((t.Id, $"{t.DisplayName}  ({t.Id})"));
            return list;
        }

        private static string[] StatDropdownCodes()
            => new[] { None }.Concat(StatCatalog.Entries.Select(e => e.Key)).Append(StatCatalog.Custom).ToArray();

        private static string[] StatDropdownNames()
            => new[] { "(none)" }.Concat(StatCatalog.Entries.Select(e => e.Label)).Append("Type a key...").ToArray();

        private void Compose()
        {
            var classes = ClassIds();
            if (!classes.Contains(classId) && classes.Count > 0) classId = classes[0];
            int treeCount = TreeCount();
            treeIndex = Math.Clamp(treeIndex, 0, treeCount - 1);

            var cls = Mod?.Classes.Get(classId);
            var treeCodes = Enumerable.Range(0, treeCount).Select(i => i.ToString()).ToArray();
            var treeNames = Enumerable.Range(0, treeCount).Select(i => cls?.TreeName(i) ?? "Tree " + (i + 1)).ToArray();

            double gridW = columns * (CellW + 6);
            double y = 30;

            var classDrop = ElementBounds.Fixed(0, y, 160, RowH);
            var treeDrop = ElementBounds.Fixed(168, y, 180, RowH);
            var reloadBtn = ElementBounds.Fixed(356, y, 90, RowH);
            var headerRule = ElementBounds.Fixed(0, y + RowH + 10, gridW + PanelW + 20, 2);

            double gridY = y + RowH + 24;
            double panelX = gridW + 28;

            // The right panel is taller than a short tree's grid, so the window is sized to whichever side is
            // longer - otherwise the Save button would sit outside the dialog background.
            double contentH = Math.Max(tiers * (CellH + 6), PanelHeight);
            var bgBounds = ElementBounds.Fixed(0, 0, gridW + PanelW + 70, gridY + contentH + 60);

            // Inset plates behind the grid and the panel, the same way the class picker frames its two columns.
            var gridPlate = ElementBounds.Fixed(-6, gridY - 8, gridW + 6, tiers * (CellH + 6) + 12);
            var panelPlate = ElementBounds.Fixed(panelX - 10, gridY - 8, PanelW + 20, contentH + 12);

            ClearComposers();
            var compo = capi.Gui.CreateCompo(ComposerKey,
                    ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
                .AddShadedDialogBG(bgBounds, true, 5.0, 0.75f)
                .AddDialogTitleBar("RpgClasses Tree Editor", () => TryClose())
                .BeginChildElements(ElementBounds.Fixed(24, GuiStyle.TitleBarHeight + 10, gridW + PanelW + 30,
                        bgBounds.fixedHeight))
                    .AddStaticText(cls?.DisplayName ?? classId, EditorStyle.Heading(),
                        ElementBounds.Fixed(0, 4, gridW + PanelW, 22))
                    .AddDropDown(classes.ToArray(), classes.ToArray(), Math.Max(0, classes.IndexOf(classId)),
                        OnClassSelected, classDrop, "class")
                    .AddDropDown(treeCodes, treeNames, treeIndex, OnTreeSelected, treeDrop, "tree")
                    .AddSmallButton("Reload", () => { Reload(); Compose(); return true; }, reloadBtn)
                    .AddStaticCustomDraw(headerRule, EditorStyle.Rule)
                    .AddInset(gridPlate, 3, 0.85f)
                    .AddInset(panelPlate, 3, 0.85f);

            AddGrid(compo, gridY);
            AddEditorPanel(compo, panelX, gridY);

            SingleComposer = compo.EndChildElements().Compose();

            FillEditorValues();
        }

        private void AddGrid(GuiComposer compo, double gridY)
        {
            for (int tier = 0; tier < tiers; tier++)
            {
                for (int col = 0; col < columns; col++)
                {
                    double x = col * (CellW + 6);
                    double cy = gridY + tier * (CellH + 6);
                    var bounds = ElementBounds.Fixed(x, cy, CellW, CellH);

                    var model = At(tier, col);
                    var built = LockedAt(tier, col);
                    int t = tier, c = col; // captured per cell

                    // A locked (code-defined) talent is marked with a leading dot rather than a word, so the
                    // marker doesn't eat the half of the caption that carries the name.
                    string label = model != null
                        ? Shorten(string.IsNullOrEmpty(model.name) ? model.id ?? "?" : model.name!, CellChars)
                        : built != null ? "* " + Shorten(built.DisplayName, CellChars - 2)
                        : "+";

                    // The selected cell is drawn in the heavier style - it is the only visual cue that the panel
                    // on the right belongs to this cell.
                    var style = (tier == selTier && col == selColumn) ? EnumButtonStyle.MainMenu : EnumButtonStyle.Small;
                    compo.AddSmallButton(label, () => OnCellClicked(t, c), bounds, style, "cell_" + tier + "_" + col);
                }
            }
        }

        private void AddEditorPanel(GuiComposer compo, double x, double y)
        {
            double row = y;

            var built = selTier >= 0 ? LockedAt(selTier, selColumn) : null;
            if (built != null)
            {
                // Room for two lines: a talent name is free text and can be longer than the panel is wide.
                compo.AddStaticText(built.DisplayName, EditorStyle.Heading(),
                    ElementBounds.Fixed(x, row, PanelW, 44));
                row += 48;
                compo.AddStaticText("Defined in code, so it can't be edited here - its behaviour is a C# hook that a data entry can't carry.",
                    EditorStyle.Body(), ElementBounds.Fixed(x, row, PanelW, 60));
                row += 66;
                AddSaveRow(compo, x, row);
                return;
            }

            var m = Selected;
            if (m == null)
            {
                // Short heading, hint underneath: a heading long enough to wrap would run into the block below it.
                compo.AddStaticText(selTier < 0 ? "No cell selected" : "Empty cell",
                    EditorStyle.Heading(), ElementBounds.Fixed(x, row, PanelW, 22));
                row += 24;
                compo.AddStaticText("Click a cell to edit or create a talent.",
                    EditorStyle.Body(), ElementBounds.Fixed(x, row, PanelW, 20));
                row += 24;
                if (selTier >= 0)
                {
                    compo.AddSmallButton("Create talent here", CreateTalent,
                        ElementBounds.Fixed(x, row, PanelW, RowH));
                    row += RowH + 8;
                }
                else
                {
                    AddCopySource(compo, x, ref row);
                }
                AddSaveRow(compo, x, row);
                return;
            }

            Label(compo, x, ref row, "Id (permanent - it keys spent points)");
            compo.AddTextInput(ElementBounds.Fixed(x, row, PanelW, RowH), v => m.id = v.Trim(),
                CairoFont.WhiteDetailText(), "f_id");
            row += RowH + 8;

            Label(compo, x, ref row, "Name");
            compo.AddTextInput(ElementBounds.Fixed(x, row, PanelW, RowH), v => m.name = v,
                CairoFont.WhiteDetailText(), "f_name");
            row += RowH + 8;

            Label(compo, x, ref row, "Description");
            compo.AddTextInput(ElementBounds.Fixed(x, row, PanelW, RowH), v => m.description = v,
                CairoFont.WhiteDetailText(), "f_desc");
            row += RowH + 8;

            Label(compo, x, ref row, "Max ranks");
            compo.AddNumberInput(ElementBounds.Fixed(x, row, 80, RowH),
                v => { if (int.TryParse(v, out int r)) m.maxRank = r; }, CairoFont.WhiteDetailText(), "f_rank");
            row += RowH + 8;

            // Spell, prerequisite and stat are all picked from what exists rather than typed: an id that is off
            // by a character registers a talent that grants nothing, and the mistake only shows up in the log.
            Label(compo, x, ref row, "Grants spell (a talent may grant none)");
            var spells = SpellChoices();
            compo.AddDropDown(spells.Select(s => s.id).ToArray(), spells.Select(s => s.name).ToArray(),
                Math.Max(0, spells.FindIndex(s => s.id == (m.grantsSpell ?? None))),
                (code, _) => { m.grantsSpell = code == None ? null : code; }, ElementBounds.Fixed(x, row, PanelW, RowH),
                "f_grants");
            row += RowH + 8;

            Label(compo, x, ref row, "Requires talent (must be maxed first)");
            var reqs = RequirementChoices(m);
            compo.AddDropDown(reqs.Select(s => s.id).ToArray(), reqs.Select(s => s.name).ToArray(),
                Math.Max(0, reqs.FindIndex(s => s.id == (m.requires ?? None))),
                (code, _) => { m.requires = code == None ? null : code; }, ElementBounds.Fixed(x, row, PanelW, RowH),
                "f_requires");
            row += RowH + 8;

            Label(compo, x, ref row, "Stat per rank (a talent may set none)");
            var stat = m.stats is { Count: > 0 } ? m.stats[0] : null;
            var statCodes = StatDropdownCodes();
            int statSel = customStat
                ? Array.IndexOf(statCodes, StatCatalog.Custom)
                : Math.Max(0, Array.IndexOf(statCodes, stat?.stat ?? None));
            compo.AddDropDown(statCodes, StatDropdownNames(), statSel, (code, _) => OnStatSelected(m, code),
                    ElementBounds.Fixed(x, row, 190, RowH), "f_stat")
                .AddNumberInput(ElementBounds.Fixed(x + 198, row, 100, RowH), v => SetStatValue(m, v),
                    CairoFont.WhiteDetailText(), "f_statval");
            row += RowH + 8;

            if (customStat)
            {
                compo.AddTextInput(ElementBounds.Fixed(x, row, PanelW, RowH), v => SetStatName(m, v),
                    CairoFont.WhiteDetailText(), "f_statname");
                row += RowH + 8;
            }
            row += 2;

            compo.AddSmallButton("Delete talent", DeleteTalent, ElementBounds.Fixed(x, row, PanelW, RowH));
            row += RowH + 8;
            AddSaveRow(compo, x, row);
        }

        /// <summary>"Start from an existing tree": pick any class and one of its trees and copy its layout in as
        /// a starting point. Shown only with no cell selected, since it replaces the whole working tree.</summary>
        private void AddCopySource(GuiComposer compo, double x, ref double row)
        {
            var classes = Mod?.Classes.All.Values.OrderBy(c => c.Id).ToList();
            if (classes == null || classes.Count == 0) return;

            // One block with room for the wrap: two one-line labels here each ran to two lines and collided.
            row += 6;
            compo.AddStaticText("Or start from an existing tree. Layout, ranks and granted spells are copied; "
                    + "a code talent's behaviour stays in C# and does not come along.",
                CairoFont.WhiteDetailText(), ElementBounds.Fixed(x, row, PanelW, 52));
            row += 58;

            if (copyClassId == null || classes.All(c => c.Id != copyClassId)) copyClassId = classes[0].Id;
            var copyCls = Mod?.Classes.Get(copyClassId!);
            int treeCount = Math.Max(1, copyCls?.TreeCount ?? 1);
            copyTree = Math.Clamp(copyTree, 0, treeCount - 1);

            var ids = classes.Select(c => c.Id).ToArray();
            var treeCodes = Enumerable.Range(0, treeCount).Select(i => i.ToString()).ToArray();
            var treeNames = Enumerable.Range(0, treeCount)
                .Select(i => copyCls?.TreeName(i) ?? "Tree " + (i + 1)).ToArray();

            compo.AddDropDown(ids, ids, Math.Max(0, Array.IndexOf(ids, copyClassId)),
                    (code, _) => { copyClassId = code; copyTree = 0; Compose(); },
                    ElementBounds.Fixed(x, row, 130, RowH), "f_copyclass")
                .AddDropDown(treeCodes, treeNames, copyTree,
                    (code, _) => { if (int.TryParse(code, out int t)) { copyTree = t; Compose(); } },
                    ElementBounds.Fixed(x + 138, row, 160, RowH), "f_copytree");
            row += RowH + 6;

            compo.AddSmallButton("Copy that tree here", CopyTree, ElementBounds.Fixed(x, row, PanelW, RowH));
            row += RowH + 8;
        }

        /// <summary>Save + status, placed at whatever y the panel above it ended on - the panel's height depends
        /// on what is selected, so a fixed offset would sooner or later be drawn on top of a field.</summary>
        private void AddSaveRow(GuiComposer compo, double x, double y)
        {
            compo.AddStaticCustomDraw(ElementBounds.Fixed(x, y + 4, PanelW, 2), EditorStyle.Rule)
                .AddSmallButton("Save tree", SaveTree, ElementBounds.Fixed(x, y + 12, PanelW, RowH));
            if (status.Length > 0)
                compo.AddStaticText(status, EditorStyle.Status(status.Contains("didn't") || status.Contains("Not ")),
                    ElementBounds.Fixed(x, y + 12 + RowH + 6, PanelW, 44));
        }

        private static void Label(GuiComposer compo, double x, ref double row, string text)
        {
            compo.AddStaticText(text, EditorStyle.FieldLabel(), ElementBounds.Fixed(x, row, PanelW, 18));
            row += 20;
        }

        /// <summary>Text inputs only exist once composed, so their values are filled in afterwards.</summary>
        private void FillEditorValues()
        {
            var m = Selected;
            if (m == null) return;

            SingleComposer.GetTextInput("f_id").SetValue(m.id ?? "");
            SingleComposer.GetTextInput("f_name").SetValue(m.name ?? "");
            SingleComposer.GetTextInput("f_desc").SetValue(m.description ?? "");
            SingleComposer.GetNumberInput("f_rank").SetValue(m.maxRank.ToString());

            var stat = m.stats is { Count: > 0 } ? m.stats[0] : null;
            if (customStat) SingleComposer.GetTextInput("f_statname").SetValue(stat?.stat ?? "");
            SingleComposer.GetNumberInput("f_statval")
                .SetValue((stat?.perRank ?? 0f).ToString(CultureInfo.InvariantCulture));
        }

        private static string? Blank(string v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        private void OnStatSelected(TalentModel m, string code)
        {
            customStat = code == StatCatalog.Custom;
            if (code == None) m.stats = null;
            else if (!customStat) SetStatName(m, code);
            Compose();
        }

        private void SetStatName(TalentModel m, string value)
        {
            string? name = Blank(value);
            if (name == null) { m.stats = null; return; }
            m.stats ??= new List<TalentStatModel> { new() };
            m.stats[0].stat = name;
            m.stats[0].perRank = typedStatValue;
        }

        /// <summary>Remembers the number and applies it to the stat the talent already has. It must NOT create
        /// one: a retained number input reports its value while the form is being filled in, so creating here
        /// gave every talent a nameless stat entry - which the server then refused, taking the whole tree with it.
        /// The entry is created when a stat is actually picked.</summary>
        private void SetStatValue(TalentModel m, string value)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return;
            typedStatValue = v;
            if (m.stats is { Count: > 0 }) m.stats[0].perRank = v;
        }

        private bool OnCellClicked(int tier, int column)
        {
            selTier = tier;
            selColumn = column;
            // The free-text stat field belongs to the talent it was opened for, not to the next one clicked.
            var picked = At(tier, column)?.stats;
            customStat = picked is { Count: > 0 }
                         && !StatCatalog.Entries.Any(e => e.Key == picked[0].stat);
            Compose();
            return true;
        }

        private void OnClassSelected(string code, bool selected)
        {
            if (code == classId) return;
            classId = code;
            treeIndex = 0;
            Reload();
            Compose();
        }

        private void OnTreeSelected(string code, bool selected)
        {
            if (!int.TryParse(code, out int idx) || idx == treeIndex) return;
            treeIndex = idx;
            Reload();
            Compose();
        }

        private bool CreateTalent()
        {
            if (selTier < 0) return true;
            working.Add(new TalentModel
            {
                id = NextId(),
                @class = classId,
                tree = treeIndex,
                tier = selTier,
                column = selColumn,
                maxRank = 1,
                name = "New talent"
            });
            Compose();
            return true;
        }

        /// <summary>A free id in this class's namespace. Ids are how spent points are stored, so this is a starting
        /// point the admin is expected to rename before saving - not a name anyone should keep.</summary>
        private string NextId()
        {
            var mod = Mod;
            for (int i = 1; i < 1000; i++)
            {
                string candidate = $"{classId}:custom{i}";
                bool taken = working.Any(t => string.Equals(t.id, candidate, StringComparison.OrdinalIgnoreCase))
                             || mod?.Talents.Get(candidate) != null;
                if (!taken) return candidate;
            }
            return classId + ":custom";
        }

        /// <summary>Copies another tree's talents into the working tree as a starting layout. Ids are re-based on
        /// this class (a talent id is global), and prerequisites are re-pointed at the copies - a copy that still
        /// required the original would gate this tree on a talent in someone else's.</summary>
        private bool CopyTree()
        {
            var mod = Mod;
            if (mod == null || copyClassId == null) return true;

            var source = mod.Talents.ForClassTree(copyClassId, copyTree);
            if (source.Length == 0) { status = "That tree has no talents."; Compose(); return true; }

            var idMap = new Dictionary<string, string>();
            var copies = new List<TalentModel>();
            int fromCode = 0;

            foreach (var t in source)
            {
                string localId = t.Id.Contains(':') ? t.Id.Substring(t.Id.IndexOf(':') + 1) : t.Id;
                string newId = FreeId($"{classId}:{localId}");
                idMap[t.Id] = newId;

                var copy = new TalentModel
                {
                    id = newId,
                    @class = classId,
                    tree = treeIndex,
                    tier = t.Tier,
                    column = t.Column,
                    maxRank = t.MaxRank,
                    name = t.DisplayName,
                    description = t.Description,
                    icon = t.IconName,
                    grantsSpell = t.GrantsSpellId,
                    requires = t.RequiresTalent
                };

                // Stats only exist as data on a content talent; a code talent's effect is its ApplyStats override.
                if (t is ContentTalent ct && ct.Stats.Count > 0)
                    copy.stats = ct.Stats.Select(s => new TalentStatModel { stat = s.Stat, perRank = s.PerRank }).ToList();
                else if (t is not ContentTalent && string.IsNullOrEmpty(t.GrantsSpellId))
                    fromCode++;

                copies.Add(copy);
            }

            foreach (var c in copies)
                c.requires = c.requires != null && idMap.TryGetValue(c.requires, out var mapped) ? mapped : null;

            working.Clear();
            working.AddRange(copies);
            selTier = selColumn = -1;
            tiers = Math.Max(MinTiers, copies.Max(c => c.tier) + 1);
            columns = Math.Max(MinColumns, copies.Max(c => c.column) + 1);

            status = fromCode > 0
                ? $"Copied {copies.Count} talent(s). {fromCode} of them came from code and do nothing yet - give each a spell or a stat."
                : $"Copied {copies.Count} talent(s). Nothing is saved until you press Save tree.";
            Compose();
            return true;
        }

        /// <summary>The given id, or the first free variant of it - a talent id is global, so a copy can't reuse
        /// one that already exists.</summary>
        private string FreeId(string preferred)
        {
            var mod = Mod;
            bool Taken(string id) => working.Any(t => string.Equals(t.id, id, StringComparison.OrdinalIgnoreCase))
                                     || mod?.Talents.Get(id) != null;

            if (!Taken(preferred)) return preferred;
            for (int i = 2; i < 100; i++)
                if (!Taken(preferred + i)) return preferred + i;
            return preferred + "_copy";
        }

        private bool DeleteTalent()
        {
            var m = Selected;
            if (m != null) working.Remove(m);
            Compose();
            return true;
        }

        private bool SaveTree()
        {
            var mod = Mod;
            if (mod?.ClientChannel == null) return true;

            // A stat row with no name is not a stat - drop it rather than sending something the server will
            // refuse, which would cost the whole tree, not just that talent.
            foreach (var t in working)
            {
                t.stats?.RemoveAll(s => string.IsNullOrWhiteSpace(s.stat));
                if (t.stats is { Count: 0 }) t.stats = null;
            }

            // The same checks the server runs, so an obvious mistake is caught before the round trip. The server
            // still re-checks everything - this is a convenience, not the gate.
            string? refused = ContentEditGuard.Reject(classId, treeIndex, working, mod.Classes, mod.Talents);
            if (refused != null)
            {
                status = "Not sent: " + refused;
                Compose();
                return true;
            }

            mod.ClientChannel.SendPacket(new TalentTreeEditPacket
            {
                ClassId = classId,
                TreeIndex = treeIndex,
                TalentsJson = Newtonsoft.Json.JsonConvert.SerializeObject(working)
            });

            status = "Sent.";
            awaitingSync = true;
            return true;
        }

        /// <summary>The server applied a content edit and the registries have been rebuilt. Only reloads after a
        /// save of our own - otherwise an edit from another admin would throw away what is being typed here.</summary>
        public void OnContentChanged()
        {
            if (!IsOpened() || !awaitingSync) return;
            awaitingSync = false;

            int saved = Mod?.Talents.ForClassTree(classId, treeIndex).Count(t => t is ContentTalent) ?? 0;
            Reload();
            status = saved == 0 && working.Count == 0
                ? "The server registered nothing - check the chat line above and the server log."
                : $"Saved: {saved} talent(s) in this tree.";
            Compose();
        }

        // Cut with plain dots: the game font has no ellipsis glyph.
        private static string Shorten(string s, int max)
            => s.Length <= max ? s : s.Substring(0, max - 2).TrimEnd() + "..";
    }
}
