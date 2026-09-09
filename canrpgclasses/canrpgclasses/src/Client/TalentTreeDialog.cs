using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using canrpgclasses.Client.Gui;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Net;
using canrpgclasses.Core.Progression;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Talent window: the class's three trees as grids of ranked icons with prerequisite arrows, plus the bonus
    /// summary, affinities and saved builds. A spend sends a <see cref="TalentSpendPacket"/> and the synced-back
    /// rank triggers the redraw - nothing is applied locally.
    /// </summary>
    public class TalentTreeDialog : CanrpgDialog
    {
        private const string ComposerKey = "canrpgtalents";

        private const double CellSize = 50;
        private const double CellPad = 24;   // wide gutters: the arrows are drawn in them
        private const double TreeGap = 24;

        // Border colours by state: gold = maxed, green = spendable, blue = taken, grey = locked.
        private static readonly double[] Gold = { 0.82, 0.66, 0.28, 1 };
        private static readonly double[] Green = { 0.50, 0.95, 0.50, 1 };
        private static readonly double[] Blue = { 0.55, 0.70, 0.95, 1 };
        private static readonly double[] Grey = { 0.45, 0.45, 0.50, 1 };
        private static readonly double[] Dim = { 0.50, 0.50, 0.55, 0.7 };

        // The same four states as a plate tint - muted versions, so the icon still reads on top but the state is
        // obvious from across the window rather than only at the border.
        private static readonly double[] PlateGold = { 0.36, 0.28, 0.10, 0.85 };
        private static readonly double[] PlateGreen = { 0.16, 0.30, 0.16, 0.85 };
        private static readonly double[] PlateBlue = { 0.14, 0.20, 0.32, 0.85 };
        private static readonly double[] PlateLocked = { 0.09, 0.09, 0.11, 0.80 };

        private static readonly double[] Accent = EditorStyle.Accent;

        // Row colours for the two summary panels live in VtmlText, shared with the character sheet.
        private const string RowLabel = VtmlText.RowLabel;
        private const string Good = VtmlText.Good;
        private const string Bad = VtmlText.Bad;
        private const string Neutral = VtmlText.Neutral;
        private const string Inactive = VtmlText.Inactive;
        private static readonly double[] ArrowMet = { 1.00, 1.00, 1.00, 0.97 };
        private static readonly double[] ArrowUnmet = { 0.58, 0.58, 0.64, 0.85 };

        private readonly TalentLoadouts loadouts;

        /// <summary>Per tree: the talents laid out in cell order, null where the grid has a hole. Sized per class
        /// (<see cref="RpgClassDef.TreeCount"/>) rather than to a fixed three, so a class - ours or an add-on's -
        /// can carry a fourth tree and the window simply gets wider.</summary>
        private List<Talent?>[] treeCells = Array.Empty<List<Talent?>>();
        private int[] treeCols = Array.Empty<int>();
        private int treeCount = RpgClassDef.DefaultTreeCount;
        private int treeRows = 1;

        private int selLoadout;
        private string rankSignature = "";
        private long listenerId;

        public TalentTreeDialog(ICoreClientAPI capi) : base(capi)
        {
            loadouts = canrpgclassesModSystem.ClientInstance?.Loadouts ?? new TalentLoadouts(capi);
            // The per-tree arrays are (re)built by LayoutTrees once the player's class is known.
            listenerId = capi.Event.RegisterGameTickListener(_ => Refresh(), 500);
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Compose();
        }

        /// <summary>Ranks only change through the server, so the cheapest reliable trigger is a signature of the
        /// player's spent ranks. When it moves, the arrows and cell states are stale and the dialog recomposes.</summary>
        private string RankSignature(Entity player, canrpgclassesModSystem mod, string classId)
        {
            var sb = new StringBuilder();
            foreach (var t in mod.Talents.ForClass(classId)) sb.Append(TalentState.Rank(player, t.Id)).Append(',');
            return sb.ToString();
        }

        private void Refresh()
        {
            if (!IsOpened()) return;
            var player = capi.World?.Player?.Entity;
            var mod = canrpgclassesModSystem.ClientInstance;
            if (player == null || mod == null) return;

            string classId = TalentState.CurrentClass(player);
            string sig = RankSignature(player, mod, classId);
            if (sig != rankSignature) { Compose(); return; }

            UpdateHeader(player, mod, classId);
            UpdateSummary(player, mod, classId);
        }

        private void Compose()
        {
            var player = capi.World?.Player?.Entity;
            var mod = canrpgclassesModSystem.ClientInstance;
            if (player == null || mod == null) return;

            string classId = TalentState.CurrentClass(player);
            RpgClassDef? cls = mod.Classes.Get(classId);
            rankSignature = RankSignature(player, mod, classId);

            var prog = player.GetBehavior<EBProgression>();
            int level = prog?.Level ?? 1;
            int totalPoints = prog?.TotalTalentPoints ?? 0;

            LayoutTrees(mod, classId, cls);

            double y = 0;
            var headerBounds = ElementBounds.Fixed(0, y, 700, 28);
            var xpBarBounds = ElementBounds.Fixed(0, y + 30, 360, 12);
            var xpTextBounds = ElementBounds.Fixed(0, y + 44, 360, 18);
            var pointsBounds = ElementBounds.Fixed(370, y + 28, 330, 22);
            y += 68;
            double headerRuleY = y - 6;

            // Trees side by side, each its own grid so a tree's arrows stay inside it, and each on its own inset
            // panel so three grids don't read as one undifferentiated field of icons.
            var treeBounds = new ElementBounds[treeCount];
            var treeTitleBounds = new ElementBounds[treeCount];
            var treeCountBounds = new ElementBounds[treeCount];
            var treePanelBounds = new ElementBounds[treeCount];
            double gridH = treeRows * (CellSize + CellPad);
            double x = 0;
            for (int t = 0; t < treeCount; t++)
            {
                double w = treeCols[t] * (CellSize + CellPad);
                treePanelBounds[t] = ElementBounds.Fixed(x - 6, y - 4, w + 12, gridH + 34);
                treeTitleBounds[t] = ElementBounds.Fixed(x, y, w - 40, 22);
                treeCountBounds[t] = ElementBounds.Fixed(x + w - 40, y, 40, 22);
                treeBounds[t] = ElementBounds.Fixed(x, y + 26, w, gridH);
                x += w + TreeGap;
            }
            double treesW = Math.Max(700, x - TreeGap);
            y += 34 + gridH + 14;
            double treesRuleY = y - 8;

            double halfW = treesW / 2 - 10;
            // Two lines of title: these headings are full sentences ("Gear bonuses (from equipped weapon/armor)")
            // and wrap, so a one-line slot would let them run over the body text underneath.
            const double panelTitleH = 42;
            // Room for every row a fully-specced character can show (about fifteen) rather than the handful a
            // fresh one has - the panel is a fixed block, so anything past its height would simply be cut off.
            const double panelBodyH = 260;
            var summaryTitleBounds = ElementBounds.Fixed(0, y, halfW, panelTitleH);
            var summaryBounds = ElementBounds.Fixed(4, y + panelTitleH + 2, halfW - 8, panelBodyH);
            var summaryPanelBounds = ElementBounds.Fixed(-6, y - 4, halfW + 12, panelTitleH + panelBodyH + 10);
            var affinityTitleBounds = ElementBounds.Fixed(treesW / 2 + 10, y, halfW, panelTitleH);
            var affinityBounds = ElementBounds.Fixed(treesW / 2 + 14, y + panelTitleH + 2, halfW - 8, panelBodyH);
            var affinityPanelBounds = ElementBounds.Fixed(treesW / 2 + 4, y - 4, halfW + 12, panelTitleH + panelBodyH + 10);
            y += panelTitleH + panelBodyH + 18;
            double summaryRuleY = y - 8;

            var list = loadouts.For(classId);
            if (selLoadout >= list.Count) selLoadout = Math.Max(0, list.Count - 1);

            var buildLabelBounds = ElementBounds.Fixed(0, y + 4, 90, 22);
            var buildDropBounds = ElementBounds.Fixed(92, y, 180, 26);
            var buildApplyBounds = ElementBounds.Fixed(278, y, 90, 26);
            var buildDelBounds = ElementBounds.Fixed(372, y, 90, 26);
            var buildSaveBounds = ElementBounds.Fixed(466, y, 110, 26);
            var buildNameBounds = ElementBounds.Fixed(0, y + 32, 260, 26);
            var respecBounds = ElementBounds.Fixed(treesW - 130, y + 32, 130, 26);
            y += 66;

            ClearComposers();
            var compo = capi.Gui.CreateCompo(ComposerKey,
                    ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
                .AddShadedDialogBG(ElementBounds.Fixed(0, 0, treesW + 40, y + 75), true, 5.0, 0.75f)
                .AddDialogTitleBar(Lang.Get("canrpgclasses:ui-talents-title"), () => TryClose())
                // Content starts below the title bar - at y=30 the first line of the header ran under it.
                .BeginChildElements(ElementBounds.Fixed(20, GuiStyle.TitleBarHeight + 12, treesW, y))
                    .AddDynamicText(HeaderText(cls, classId, level), CairoFont.WhiteMediumText(), headerBounds, "header")
                    .AddStatbar(xpBarBounds, GuiStyle.XPBarColor, "xpbar")
                    .AddDynamicText("", CairoFont.WhiteDetailText(), xpTextBounds, "xptext")
                    .AddDynamicText(PointsText(player, totalPoints),
                        CairoFont.WhiteSmallText().WithColor(Gold), pointsBounds, "points")
                    .AddStaticCustomDraw(ElementBounds.Fixed(0, headerRuleY, treesW, 2), EditorStyle.Rule);

            for (int t = 0; t < treeCount; t++)
            {
                int inTree = TalentState.PointsInTree(player, mod.Talents, t);
                compo.AddInset(treePanelBounds[t], 3, 0.85f);
                compo.AddStaticText(cls?.TreeName(t) ?? "Tree " + (t + 1),
                    CairoFont.WhiteSmallishText().WithColor(Accent), treeTitleBounds[t]);
                compo.AddDynamicText(inTree.ToString(),
                    CairoFont.WhiteSmallishText().WithColor(Gold).WithOrientation(EnumTextOrientation.Right),
                    treeCountBounds[t], "treecount" + t);
                compo.AddIconGrid(treeBounds[t], treeCols[t], treeRows, CellSize, 0, CellPad, "tree" + t);
            }

            compo.AddStaticCustomDraw(ElementBounds.Fixed(0, treesRuleY, treesW, 2), EditorStyle.Rule)

                .AddInset(summaryPanelBounds, 3, 0.85f)
                .AddStaticText(Lang.Get("canrpgclasses:ui-sum-title"),
                    CairoFont.WhiteSmallishText().WithColor(Accent), summaryTitleBounds)
                // Richtext, not dynamic text: these blocks colour each row on its own (a bonus green, a malus red,
                // an inactive gear set grey), and a dynamic-text element is one colour throughout.
                .AddRichtext("", CairoFont.WhiteDetailText(), summaryBounds, "summary")

                .AddInset(affinityPanelBounds, 3, 0.85f)
                .AddStaticText(Lang.Get("canrpgclasses:ui-gear-bonuses"),
                    CairoFont.WhiteSmallishText().WithColor(Accent), affinityTitleBounds)
                .AddRichtext("", CairoFont.WhiteDetailText(), affinityBounds, "affinities")

                .AddStaticCustomDraw(ElementBounds.Fixed(0, summaryRuleY, treesW, 2), EditorStyle.Rule)
                .AddStaticText(Lang.Get("canrpgclasses:ui-build"), CairoFont.WhiteSmallText(), buildLabelBounds)
                .AddIf(list.Count > 0)
                    .AddDropDown(Enumerable.Range(0, list.Count).Select(i => i.ToString()).ToArray(),
                        list.Select(b => b.Name).ToArray(), selLoadout, OnBuildSelected, buildDropBounds, "builds")
                    .AddSmallButton(Lang.Get("canrpgclasses:ui-build-apply"), ApplyBuild, buildApplyBounds)
                    .AddSmallButton(Lang.Get("canrpgclasses:ui-set-del"), DeleteBuild, buildDelBounds)
                    .AddTextInput(buildNameBounds, OnBuildRenamed, CairoFont.WhiteSmallText(), "buildname")
                .EndIf()
                .AddSmallButton(Lang.Get("canrpgclasses:ui-build-save"), SaveBuild, buildSaveBounds)
                .AddSmallButton(Lang.Get("canrpgclasses:ui-respec"), Respec, respecBounds)
                .AddSpellTooltip(ElementBounds.Fixed(0, 0, 1, 1), "tooltip");

            // Cells and arrow drawing must be in place before Compose: the arrows are baked into the grid's
            // static layer.
            for (int t = 0; t < treeCount; t++) FillTree(compo, player, mod, cls, classId, t, totalPoints);

            SingleComposer = compo.EndChildElements().Compose();

            if (list.Count > 0 && selLoadout < list.Count)
                SingleComposer.GetTextInput("buildname").SetValue(list[selLoadout].Name);

            UpdateHeader(player, mod, classId);
            UpdateSummary(player, mod, classId);
        }

        /// <summary>Places each tree's talents into a rectangular cell grid by (Column, Tier). Rows are sized to
        /// the deepest talent the class actually defines and shared across all three trees, so they line up and
        /// there are no empty rows at the bottom.</summary>
        private void LayoutTrees(canrpgclassesModSystem mod, string classId, RpgClassDef? cls)
        {
            int maxTier = 0;
            foreach (var t in mod.Talents.ForClass(classId)) if (t.Tier > maxTier) maxTier = t.Tier;
            treeRows = maxTier + 1;

            // A talent whose TreeIndex is past the class's declared trees would otherwise be laid out nowhere and
            // silently vanish from the window - widen to fit it instead, and let the class definition be the
            // authority on names only.
            treeCount = Math.Max(1, cls?.TreeCount ?? RpgClassDef.DefaultTreeCount);
            foreach (var t in mod.Talents.ForClass(classId)) if (t.TreeIndex + 1 > treeCount) treeCount = t.TreeIndex + 1;

            if (treeCells.Length != treeCount)
            {
                treeCells = new List<Talent?>[treeCount];
                treeCols = new int[treeCount];
            }

            for (int tree = 0; tree < treeCount; tree++)
            {
                var talents = mod.Talents.ForClassTree(classId, tree).ToList();
                int cols = 1;
                foreach (var t in talents) if (t.Column + 1 > cols) cols = t.Column + 1;
                treeCols[tree] = cols;

                var cells = new List<Talent?>(new Talent?[cols * treeRows]);
                foreach (var t in talents)
                {
                    int index = t.Tier * cols + t.Column;
                    if (index >= 0 && index < cells.Count) cells[index] = t;
                }
                treeCells[tree] = cells;
            }
        }

        private void FillTree(GuiComposer compo, Entity player, canrpgclassesModSystem mod, RpgClassDef? cls,
            string classId, int tree, int totalPoints)
        {
            var grid = compo.GetIconGrid("tree" + tree);
            if (grid == null) return;

            var icons = mod.Icons;
            var cells = treeCells[tree];

            // The rank counter goes in the gutter under each talent, not on top of its art.
            grid.BadgeBelow = true;

            grid.Cells = cells.Select(t =>
            {
                if (t == null) return new IconGridCell { Hidden = true };

                int rank = TalentState.Rank(player, t.Id);
                bool maxed = rank >= t.MaxRank;
                bool canSpend = TalentState.CanSpend(player, t, mod.Talents, totalPoints, out _);
                bool locked = rank == 0 && !canSpend;

                return new IconGridCell
                {
                    Id = t.Id,
                    Icon = icons?.GetTex(IconLoader.PathFor(TalentIconName(t, mod))),
                    IconFallback = Abbrev(t.DisplayName),
                    Badge = $"{rank}/{t.MaxRank}",
                    IconTint = locked ? Dim : null,
                    FrameColor = maxed ? Gold : canSpend ? Green : rank > 0 ? Blue : Grey,
                    PlateColor = maxed ? PlateGold : canSpend ? PlateGreen : rank > 0 ? PlateBlue : PlateLocked,
                    // Heavier frame on the two states worth acting on: what you can spend into now, and what is
                    // already finished.
                    Emphasize = canSpend || maxed
                };
            }).ToList();

            grid.OnComposeBackground = (ctx, rect) => DrawArrows(ctx, rect, player, mod, tree);

            var tooltip = compo.GetSpellTooltip("tooltip");
            // The mouse button is unused here, but it cannot be named _ : that would capture the out discard below.
            grid.OnCellClick = (index, button) =>
            {
                var t = index >= 0 && index < cells.Count ? cells[index] : null;
                if (t == null) return;
                if (!TalentState.CanSpend(player, t, mod.Talents, totalPoints, out _)) return;
                mod.ClientChannel?.SendPacket(new TalentSpendPacket { TalentId = t.Id });
            };
            grid.OnCellHover = index =>
            {
                var t = index >= 0 && index < cells.Count ? cells[index] : null;
                ShowTooltip(tooltip, player, mod, cls, t, tree);
            };
        }

        private void ShowTooltip(GuiElementSpellTooltip? tooltip, Entity player, canrpgclassesModSystem mod,
            RpgClassDef? cls, Talent? talent, int tree)
        {
            if (tooltip == null) return;
            if (talent == null) { tooltip.Clear(); return; }

            int rank = TalentState.Rank(player, talent.Id);
            var lines = new List<SpellTooltip.Line>
            {
                new(talent.DisplayName + "   " + Lang.Get("canrpgclasses:ui-rank", rank, talent.MaxRank),
                    SpellTooltip.Name)
            };

            // "Learn a skill" talents read differently from passive stat talents: a cyan banner plus the granted
            // spell's real numbers, so it's obvious the talent unlocks an ability.
            if (!string.IsNullOrEmpty(talent.GrantsSpellId) && mod.Spells.TryGet(talent.GrantsSpellId!, out var granted)
                && granted != null)
            {
                lines.Add(new SpellTooltip.Line(Lang.Get("canrpgclasses:ui-tt-learns"), new double[] { 0.45, 0.85, 1, 1 }));
                lines.AddRange(SpellTooltip.BuildLines(player, granted));
            }
            else if (!string.IsNullOrEmpty(talent.Description))
            {
                lines.Add(new SpellTooltip.Line(talent.Description, SpellTooltip.Body, true));
            }

            if (talent.Tier > 0)
                lines.Add(new SpellTooltip.Line(
                    Lang.Get("canrpgclasses:ui-requires-points", talent.Tier * TalentState.PointsPerTier, cls?.TreeName(tree)),
                    SpellTooltip.Meta));
            if (!string.IsNullOrEmpty(talent.RequiresTalent))
            {
                var req = mod.Talents.Get(talent.RequiresTalent!);
                if (req != null)
                    lines.Add(new SpellTooltip.Line(
                        Lang.Get("canrpgclasses:ui-requires-talent", req.DisplayName), SpellTooltip.Meta));
            }

            if (talent.RequiresAttributes != null)
                foreach (var kv in talent.RequiresAttributes)
                {
                    var attr = Core.Attributes.RpgAttributes.Get(kv.Key);
                    if (attr == null) continue;
                    lines.Add(new SpellTooltip.Line(
                        Lang.Get("canrpgclasses:ui-requires-attribute", attr.DisplayName, kv.Value.ToString("0.##")),
                        SpellTooltip.Meta));
                }

            tooltip.SetLines(talent.Id + "@" + rank, lines);
        }

        /// <summary>Prerequisite arrows, from each talent that has a RequiresTalent to its dependent. White when
        /// the prerequisite is satisfied (maxed), grey otherwise, so locked chains read at a glance.</summary>
        private void DrawArrows(Context ctx, System.Func<int, (double X, double Y, double W, double H)> rect,
            Entity player, canrpgclassesModSystem mod, int tree)
        {
            var cells = treeCells[tree];
            var byId = new Dictionary<string, int>();
            for (int i = 0; i < cells.Count; i++) if (cells[i] != null) byId[cells[i]!.Id] = i;

            foreach (var talent in cells)
            {
                if (talent == null || string.IsNullOrEmpty(talent.RequiresTalent)) continue;
                if (!byId.TryGetValue(talent.Id, out int depIndex)) continue;
                if (!byId.TryGetValue(talent.RequiresTalent!, out int reqIndex)) continue;

                var reqT = mod.Talents.Get(talent.RequiresTalent!);
                if (reqT == null) continue;

                // Only neighbouring cells get an arrow. Several talents in a tree can share one distant
                // prerequisite (four Feral talents all require Ursine Form, three rows up and two columns over);
                // routing those produces long runs through the gutters that read as arrows between whatever cells
                // they happen to pass, which is worse than no arrow at all. The requirement is spelled out in the
                // talent's tooltip either way.
                int dRow = Math.Abs(talent.Tier - reqT.Tier);
                int dCol = Math.Abs(talent.Column - reqT.Column);
                if (dRow > 1 || dCol > 1 || (dRow == 0 && dCol == 0)) continue;

                var dep = rect(depIndex);
                var req = rect(reqIndex);
                bool satisfied = TalentState.Rank(player, reqT.Id) >= reqT.MaxRank;
                var color = satisfied ? ArrowMet : ArrowUnmet;
                double thickness = satisfied ? 3 : 2;

                if (talent.Tier == reqT.Tier)
                {
                    // Same row: join the facing side edges.
                    bool reqLeft = req.X < dep.X;
                    DrawArrow(ctx, new[]
                    {
                        (reqLeft ? req.X + req.W : req.X, req.Y + req.H / 2),
                        (reqLeft ? dep.X : dep.X + dep.W, dep.Y + dep.H / 2)
                    }, color, thickness);
                    continue;
                }

                double rcx = req.X + req.W / 2, dcx = dep.X + dep.W / 2;
                bool depBelow = dep.Y > req.Y;
                double fromY = depBelow ? req.Y + req.H : req.Y;
                double toY = depBelow ? dep.Y : dep.Y + dep.H;

                if (Math.Abs(rcx - dcx) < 1)
                {
                    // Same column: a plain vertical run. Two of these can never share a lane, so nothing to route.
                    DrawArrow(ctx, new[] { (rcx, fromY), (dcx, toY) }, color, thickness);
                    continue;
                }

                // Different column: route orthogonally - down out of the prerequisite, across the gutter between
                // the rows, then down into the dependent. Diagonals were the whole problem: they cut across cells
                // and across each other at arbitrary angles, so two links near each other turned into a cross-
                // hatch. Right-angle runs stay inside the gutters, and where two of them do share a gutter they
                // overlap as one clean line instead of an X.
                double gutterY = (fromY + toY) / 2;
                DrawArrow(ctx, new[] { (rcx, fromY), (rcx, gutterY), (dcx, gutterY), (dcx, toY) }, color, thickness);
            }
        }

        /// <summary>An arrow along a polyline - one segment for a straight run, three for an orthogonal detour.
        /// Soft outer glow, solid body, brighter core, head on the final segment.</summary>
        private static void DrawArrow(Context ctx, (double X, double Y)[] points, double[] color, double thickness)
        {
            if (points.Length < 2) return;

            var a = points[0];
            var b = points[^1];

            (double ux, double uy) Dir((double X, double Y) p, (double X, double Y) q)
            {
                double dx = q.X - p.X, dy = q.Y - p.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                return len < 1e-6 ? (0, 0) : (dx / len, dy / len);
            }

            var start = Dir(a, points[1]);
            var end = Dir(points[^2], b);
            if (start.ux == 0 && start.uy == 0) return;
            if (end.ux == 0 && end.uy == 0) return;

            // Inset the ends so tail and head sit in the gutter, clear of the cell frames.
            var path = ((double X, double Y)[])points.Clone();
            path[0] = (a.X + start.ux * 3, a.Y + start.uy * 3);
            path[^1] = (b.X - end.ux * 4, b.Y - end.uy * 4);

            void Line(double w, double[] c, double alphaMul)
            {
                ctx.SetSourceRGBA(c[0], c[1], c[2], c[3] * alphaMul);
                ctx.LineWidth = w;
                // Round joins and caps: a right-angle corner drawn with the default mitre looks notched, and each
                // of the three passes below would notch it differently.
                ctx.LineJoin = LineJoin.Round;
                ctx.LineCap = LineCap.Round;
                ctx.MoveTo(path[0].X, path[0].Y);
                for (int i = 1; i < path.Length; i++) ctx.LineTo(path[i].X, path[i].Y);
                ctx.Stroke();
            }

            var core = new[] { Math.Min(1, color[0] + 0.35), Math.Min(1, color[1] + 0.35), Math.Min(1, color[2] + 0.35), color[3] };
            Line(thickness + 4, color, 0.30);
            Line(thickness, color, 1.0);
            Line(Math.Max(1, thickness * 0.45), core, 1.0);

            const double head = 12, halfW = 4.5;
            double hx = -end.uy, hy = end.ux;
            void Head(double back, double half, double[] c, double alphaMul)
            {
                double px = b.X - end.ux * back, py = b.Y - end.uy * back;
                ctx.SetSourceRGBA(c[0], c[1], c[2], c[3] * alphaMul);
                ctx.MoveTo(b.X, b.Y);
                ctx.LineTo(px + hx * half, py + hy * half);
                ctx.LineTo(px - hx * half, py - hy * half);
                ctx.ClosePath();
                ctx.Fill();
            }

            Head(head + 2, halfW + 1.6, color, 0.30);
            Head(head, halfW, color, 1.0);
            Head(head * 0.55, halfW * 0.42, core, 1.0);
        }

        private static string HeaderText(RpgClassDef? cls, string classId, int level)
            => $"{cls?.DisplayName ?? classId}    " + Lang.Get("canrpgclasses:ui-level", level);

        private static string PointsText(Entity player, int totalPoints)
            => Lang.Get("canrpgclasses:ui-talent-points", TalentState.AvailablePoints(player, totalPoints), totalPoints);

        private void UpdateHeader(Entity player, canrpgclassesModSystem mod, string classId)
        {
            var prog = player.GetBehavior<EBProgression>();
            int level = prog?.Level ?? 1;
            int totalPoints = prog?.TotalTalentPoints ?? 0;
            var cls = mod.Classes.Get(classId);

            SingleComposer.GetDynamicText("header")?.SetNewText(HeaderText(cls, classId, level));
            SingleComposer.GetDynamicText("points")?.SetNewText(PointsText(player, totalPoints));

            float xpInto = prog?.XpIntoLevel ?? 0;
            float xpNext = prog?.XpForNextLevel ?? 0;
            SingleComposer.GetStatbar("xpbar")?.SetValues(xpInto, 0, xpNext > 0 ? xpNext : 1);
            SingleComposer.GetDynamicText("xptext")?.SetNewText(xpNext > 0
                ? $"XP {xpInto:0} / {xpNext:0}"
                : Lang.Get("canrpgclasses:ui-max-level"));

            for (int t = 0; t < treeCount; t++)
                SingleComposer.GetDynamicText("treecount" + t)
                    ?.SetNewText(TalentState.PointsInTree(player, mod.Talents, t).ToString());
        }

        private void UpdateSummary(Entity player, canrpgclassesModSystem mod, string classId)
        {
            var cls = mod.Classes.Get(classId);
            int level = player.GetBehavior<EBProgression>()?.Level ?? 1;

            var bodyFont = CairoFont.WhiteDetailText();

            var sb = new StringBuilder();
            foreach (var row in TalentBonusSummary.Build(capi, player, mod, classId, cls, level))
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(Vtml(RowLabel, row.Label));
                if (row.Value.Length > 0)
                    sb.Append("  ").Append(Vtml(row.Sign > 0 ? Good : row.Sign < 0 ? Bad : Neutral, row.Value));
            }
            SingleComposer.GetRichtext("summary")?.SetNewText(sb.ToString(), bodyFont);

            // "[x]" / "[ ]" rather than a filled/hollow dot: the game font has no such glyphs.
            sb.Clear();
            foreach (var (line, active) in TalentBonusSummary.GearAffinities(capi, cls))
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(Vtml(active ? Good : Inactive, (active ? "[x] " : "[ ] ") + line));
            }
            SingleComposer.GetRichtext("affinities")?.SetNewText(sb.ToString(), bodyFont);
        }

        private static string Vtml(string hexColor, string text) => VtmlText.Color(hexColor, text);

        private string CurrentClassId()
        {
            var player = capi.World?.Player?.Entity;
            return player != null ? TalentState.CurrentClass(player) : "";
        }

        private void OnBuildSelected(string code, bool selected)
        {
            if (!int.TryParse(code, out int index)) return;
            selLoadout = index;
            var list = loadouts.For(CurrentClassId());
            if (index >= 0 && index < list.Count) SingleComposer.GetTextInput("buildname").SetValue(list[index].Name);
        }

        private void OnBuildRenamed(string value)
        {
            string classId = CurrentClassId();
            var list = loadouts.For(classId);
            if (selLoadout >= 0 && selLoadout < list.Count && list[selLoadout].Name != value)
                loadouts.Rename(classId, selLoadout, value);
        }

        private bool ApplyBuild()
        {
            var list = loadouts.For(CurrentClassId());
            if (selLoadout < 0 || selLoadout >= list.Count) return true;
            var b = list[selLoadout];
            canrpgclassesModSystem.ClientInstance?.ClientChannel?.SendPacket(
                new TalentLoadoutPacket { Ids = b.Ids, Ranks = b.Ranks });
            return true;
        }

        private bool DeleteBuild()
        {
            loadouts.Delete(CurrentClassId(), selLoadout);
            Compose();
            return true;
        }

        private bool SaveBuild()
        {
            var player = capi.World?.Player?.Entity;
            var mod = canrpgclassesModSystem.ClientInstance;
            if (player != null && mod != null) loadouts.AddFromCurrent(CurrentClassId(), player, mod);
            Compose();
            return true;
        }

        private bool Respec()
        {
            canrpgclassesModSystem.ClientInstance?.ClientChannel?.SendPacket(new TalentRespecPacket());
            return true;
        }

        /// <summary>Icon for a talent: its own IconName, else the granted spell's icon, else none.</summary>
        private static string TalentIconName(Talent talent, canrpgclassesModSystem mod)
        {
            string? name = talent.IconName;
            if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(talent.GrantsSpellId)
                && mod.Spells.TryGet(talent.GrantsSpellId!, out var s) && s != null)
                name = string.IsNullOrEmpty(s.IconName) ? s.LocalId : s.IconName;
            return name ?? "";
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
