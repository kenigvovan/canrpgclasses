using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using canrpgclasses.Client.Gui;
using canrpgclasses.Core.Net;
using canrpgclasses.Core.Talents;
using canrpgclasses.Hunter;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Hunter pet panel in three tabs: the pet (rename, health, orders sent as ordinary casts, tricks), the stable and
    /// the bestiary. Losing or summoning a pet, a stable change or a new bestiary entry rebuilds the dialog.
    /// </summary>
    public class PetPanelDialog : CanrpgDialog
    {
        private const string ComposerKey = "canrpgpetpanel";

        private string nameBuf = "";
        private string lastSyncedName = "";
        private bool hadPet;
        private string stableSig = "";
        private long listenerId;
        // 0 pet, 1 stable, 2 bestiary.
        private int tab;

        public PetPanelDialog(ICoreClientAPI capi) : base(capi)
        {
            listenerId = capi.Event.RegisterGameTickListener(_ => Refresh(), 500);
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            hadPet = Pet() != null;
            Compose();
        }

        private Entity? Pet()
        {
            var owner = capi.World?.Player?.Entity;
            if (owner == null) return null;
            long petId = owner.WatchedAttributes.GetLong(HunterPetSystem.OwnerPetIdKey, 0);
            if (petId == 0) return null;
            var pet = capi.World.GetEntityById(petId);
            return pet != null && pet.Alive ? pet : null;
        }

        private void Refresh()
        {
            if (!IsOpened()) return;

            var pet = Pet();
            if (pet != null != hadPet) { hadPet = pet != null; Compose(); return; }
            if (StableSignature() != stableSig) { Compose(); return; }
            if (pet == null) return;

            var health = pet.WatchedAttributes.GetTreeAttribute("health");
            float cur = health?.GetFloat("currenthealth") ?? 0f;
            float max = health?.GetFloat("maxhealth") ?? 1f;
            SingleComposer.GetStatbar("pethealth")?.SetValues(cur, 0, max);

            var mode = (PetMode)pet.WatchedAttributes.GetInt(HunterPetSystem.PetModeKey, (int)PetMode.Aggressive);
            SingleComposer.GetToggleButton("aggressive")?.SetValue(mode == PetMode.Aggressive);
            SingleComposer.GetToggleButton("defensive")?.SetValue(mode == PetMode.Defensive);
            SingleComposer.GetToggleButton("passive")?.SetValue(mode == PetMode.Passive);
            SingleComposer.GetToggleButton("stay")?.SetValue(
                pet.WatchedAttributes.GetBool(HunterPetSystem.PetStayKey, false));

            // Mirror the stored name only when it changes: pressing Rename defocuses the input a frame before
            // the button fires, so mirroring on unfocus would wipe the typed text.
            var owner = capi.World?.Player?.Entity;
            string synced = owner?.WatchedAttributes.GetString(HunterPetSystem.PetNameKey, "") ?? "";
            if (synced != lastSyncedName)
            {
                lastSyncedName = synced;
                nameBuf = synced;
                SingleComposer.GetTextInput("petname")?.SetValue(synced);
            }
        }

        private void Compose()
        {
            var owner = capi.World?.Player?.Entity;
            bool isHunter = owner != null && TalentState.CurrentClass(owner) == "hunter";
            var pet = Pet();

            const double w = 320, rowH = 26, gap = 6;
            double y = 0;

            // Heights are set once the content is laid out, before Compose reads them.
            var bgBounds = ElementBounds.Fixed(0, 0, w + 40, 120);
            var childBounds = ElementBounds.Fixed(20, 30, w, 80);
            var compo = capi.Gui.CreateCompo(ComposerKey,
                    ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
                .AddShadedDialogBG(bgBounds, true, 5.0, 0.75f)
                .AddDialogTitleBar(Lang.Get("canrpgclasses:ui-pet-title"), () => TryClose())
                .BeginChildElements(childBounds);

            if (!isHunter)
            {
                compo.AddStaticText(Lang.Get("canrpgclasses:ui-pet-nothunter"), CairoFont.WhiteSmallText(),
                    ElementBounds.Fixed(0, 0, w, rowH));
                y += rowH;
            }
            else
            {
                var tabs = new[]
                {
                    new GuiTab { Name = Lang.Get("canrpgclasses:ui-pet-tab-pet"), DataInt = 0 },
                    new GuiTab { Name = Lang.Get("canrpgclasses:ui-pet-tab-stable"), DataInt = 1 },
                    new GuiTab { Name = Lang.Get("canrpgclasses:ui-pet-tab-journal"), DataInt = 2 },
                };
                compo.AddHorizontalTabs(tabs, ElementBounds.Fixed(0, y, w, 28), OnTab, CairoFont.WhiteSmallText(),
                    CairoFont.WhiteSmallText().WithColor(GuiStyle.ActiveButtonTextColor), "tabs");
                y += 28 + gap * 2;

                if (tab == 1) y = ComposeStable(compo, y + gap, w, rowH, gap);
                else if (tab == 2) y = ComposeJournal(compo, y, w, rowH);
                else y = ComposePet(compo, owner!, pet, y, w, rowH, gap);
            }

            childBounds.fixedHeight = y;
            bgBounds.fixedHeight = y + 50;
            stableSig = StableSignature();

            ClearComposers();
            SingleComposer = compo.EndChildElements().Compose();
            if (SingleComposer.GetHorizontalTabs("tabs") is { } tabsElem) tabsElem.activeElement = tab;

            SingleComposer.GetTextInput("petname")?.SetValue(nameBuf);
            lastSyncedName = ""; // force the next Refresh to re-adopt the synced name into the new element
            Refresh();
        }

        private void OnTab(int index)
        {
            tab = index;
            Compose();
        }

        private double ComposePet(GuiComposer compo, Entity owner, Entity? pet, double y, double w, double rowH, double gap)
        {
            compo.AddStaticText(Lang.Get("canrpgclasses:ui-pet-name"), CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(0, y, w, rowH));
            y += rowH;
            compo.AddTextInput(ElementBounds.Fixed(0, y, 200, rowH), v => nameBuf = v,
                CairoFont.WhiteSmallText(), "petname");
            compo.AddSmallButton(Lang.Get("canrpgclasses:ui-pet-rename"), Rename,
                ElementBounds.Fixed(208, y, 112, rowH));
            y += rowH + gap * 2;

            if (pet == null)
            {
                compo.AddStaticText(Lang.Get("canrpgclasses:ui-pet-none"), CairoFont.WhiteSmallText(),
                    ElementBounds.Fixed(0, y, w, rowH));
                y += rowH + gap;
                compo.AddSmallButton(Lang.Get("canrpgclasses:ui-pet-summon"), () => Cast("summon_pet"),
                    ElementBounds.Fixed(0, y, 160, rowH));
                return y + rowH + gap;
            }

            compo.AddStatbar(ElementBounds.Fixed(0, y, w, 14), GuiStyle.HealthBarColor, "pethealth");
            y += 14 + gap * 2;

            compo.AddStaticText(Lang.Get("canrpgclasses:ui-pet-mode"), CairoFont.WhiteDetailText(),
                ElementBounds.Fixed(0, y, w, rowH));
            y += rowH;
            compo.AddToggleButton(Lang.Get("canrpgclasses:ui-pet-aggressive"), CairoFont.SmallButtonText(),
                _ => Cast("pet_aggressive"), ElementBounds.Fixed(0, y, 100, rowH), "aggressive");
            compo.AddToggleButton(Lang.Get("canrpgclasses:ui-pet-defensive"), CairoFont.SmallButtonText(),
                _ => Cast("pet_defensive"), ElementBounds.Fixed(108, y, 100, rowH), "defensive");
            compo.AddToggleButton(Lang.Get("canrpgclasses:ui-pet-passive"), CairoFont.SmallButtonText(),
                _ => Cast("pet_passive"), ElementBounds.Fixed(216, y, 100, rowH), "passive");
            y += rowH + gap;

            compo.AddToggleButton(Lang.Get("canrpgclasses:ui-pet-stay"), CairoFont.SmallButtonText(),
                _ => Cast("pet_stay"), ElementBounds.Fixed(0, y, 100, rowH), "stay");
            compo.AddSmallButton(Lang.Get("canrpgclasses:ui-pet-come"), () => Cast("pet_come"),
                ElementBounds.Fixed(108, y, 100, rowH));
            compo.AddSmallButton(Lang.Get("canrpgclasses:ui-pet-dismiss"), () => Cast("pet_dismiss"),
                ElementBounds.Fixed(216, y, 100, rowH));
            y += rowH + gap;

            // Tricks this pet's model can play and the bestiary has unlocked.
            var tricks = HunterPetSystem.Tricks(pet).FindAll(t => PetJournal.TrickUnlocked(owner, t));
            if (tricks.Count > 0)
            {
                y += gap;
                compo.AddStaticText(Lang.Get("canrpgclasses:ui-pet-tricks"), CairoFont.WhiteDetailText(),
                    ElementBounds.Fixed(0, y, w, rowH));
                y += rowH;
                for (int i = 0; i < tricks.Count; i++)
                {
                    string trick = tricks[i];
                    compo.AddSmallButton(Lang.Get("canrpgclasses:ui-pet-trick-" + trick), () => SendStable("trick:" + trick, 0),
                        ElementBounds.Fixed(i % 4 * 80, y + i / 4 * (rowH + gap), 76, rowH));
                }
                y += ((tricks.Count - 1) / 4 + 1) * (rowH + gap);
            }
            return y;
        }

        // ---- Journal ----

        private static readonly double[] RareGold = { 0.95, 0.78, 0.25, 1 };

        private double ComposeJournal(GuiComposer compo, double y, double w, double rowH)
        {
            var world = capi.World;
            var journal = PetJournal.Read(capi.World.Player.Entity);

            compo.AddStaticText(Lang.Get("canrpgclasses:ui-pet-journal-progress", PetJournal.Entries(journal), PetJournal.Total(world)),
                CairoFont.WhiteSmallText().WithColor(EditorStyle.Gold), ElementBounds.Fixed(0, y, w, rowH));
            y += rowH;
            if (PetJournal.NextEntryMilestone(capi, journal, out _) is { } next)
            {
                string nextText = Lang.Get("canrpgclasses:ui-pet-journal-next", next["entries"].AsInt(0), RewardText(next));
                var nextFont = CairoFont.WhiteDetailText().WithColor(EditorStyle.Muted);
                double nextH = TextHeight(nextFont, nextText, w);
                compo.AddStaticText(nextText, nextFont, ElementBounds.Fixed(0, y, w, nextH));
                y += nextH + 4;
            }

            var looks = PetJournal.JournaledLooks(world);
            var families = new List<string>();
            foreach (string look in looks)
            {
                string f = PetSpecies.FamilyOf(world, look);
                if (!families.Contains(f)) families.Add(f);
            }

            // "???" needs little room, so unknown rows give the hint most of the width.
            const double nameW = 150, unknownW = 50;
            foreach (string family in families)
            {
                y += 6;
                compo.AddStaticText(Lang.Get("canrpgclasses:pet-family-" + family), CairoFont.WhiteSmallText().WithColor(EditorStyle.Gold),
                    ElementBounds.Fixed(0, y, w, rowH));
                y += rowH;
                compo.AddStaticCustomDraw(ElementBounds.Fixed(0, y - 4, w, 2), EditorStyle.Rule);

                foreach (string look in looks)
                {
                    if (PetSpecies.FamilyOf(world, look) != family) continue;
                    int mask = PetJournal.Mask(journal, look);
                    double h;
                    if (mask == 0)
                    {
                        // Unknown until tamed: only a hint where to look. A long hint wraps, so the row grows with it.
                        var font = CairoFont.WhiteDetailText().WithColor(EditorStyle.Muted);
                        string hint = Lang.Get("canrpgclasses:pet-hint-" + PetSpecies.Hint(world, look));
                        h = System.Math.Max(rowH, TextHeight(font, hint, w - unknownW) + 6);
                        compo.AddStaticText("???", font, ElementBounds.Fixed(0, y + 3, unknownW, h));
                        compo.AddStaticText(hint, font, ElementBounds.Fixed(unknownW, y + 3, w - unknownW, h));
                    }
                    else
                    {
                        var font = CairoFont.WhiteDetailText();
                        string name = Lang.Get("canrpgclasses:pet-look-" + look);
                        h = System.Math.Max(rowH, TextHeight(font, name, nameW) + 6);
                        compo.AddStaticText(name, font, ElementBounds.Fixed(0, y + 3, nameW, h));
                        int colors = PetSpecies.ColorCount(world, look);
                        int[] rare = PetSpecies.RareColors(world, look);
                        compo.AddStaticCustomDraw(ElementBounds.Fixed(nameW, y, w - nameW, rowH), (ctx, _, b) => DrawCoats(ctx, b, colors, mask, rare));
                    }
                    y += h;
                }
            }
            return y + 6;
        }

        /// <summary>Unscaled height of the text wrapped to <paramref name="width"/>.</summary>
        private double TextHeight(CairoFont font, string text, double width)
            => capi.Gui.Text.GetMultilineTextHeight(font, text, GuiElement.scaled(width)) / RuntimeEnv.GUIScale;

        /// <summary>One circle per coat: filled once tamed, gold-ringed when rare.</summary>
        private static void DrawCoats(Cairo.Context ctx, ElementBounds b, int colors, int mask, int[] rare)
        {
            double r = GuiElement.scaled(5), step = GuiElement.scaled(14);
            double cy = b.drawY + b.InnerHeight / 2;
            for (int i = 0; i < colors; i++)
            {
                double cx = b.drawX + r + 1 + i * step;
                bool has = (mask >> i & 1) == 1;
                bool isRare = System.Array.IndexOf(rare, i) >= 0;
                ctx.NewPath();
                ctx.Arc(cx, cy, r, 0, System.Math.PI * 2);
                if (has)
                {
                    ctx.SetSourceRGBA(isRare ? RareGold[0] : 0.85, isRare ? RareGold[1] : 0.85, isRare ? RareGold[2] : 0.85, 1);
                    ctx.FillPreserve();
                }
                if (isRare) ctx.SetSourceRGBA(RareGold[0], RareGold[1], RareGold[2], 1);
                else ctx.SetSourceRGBA(0.6, 0.6, 0.6, 1);
                ctx.LineWidth = isRare ? 2 : 1;
                ctx.Stroke();
            }
        }

        private string RewardText(JsonObject m)
        {
            var parts = new List<string>();
            int slots = m["slots"].AsInt(0);
            if (slots > 0) parts.Add(Lang.Get("canrpgclasses:journal-reward-slots", slots));
            foreach (string t in m["tricks"].AsArray<string>() ?? System.Array.Empty<string>())
                parts.Add(Lang.Get("canrpgclasses:journal-reward-trick", Lang.Get("canrpgclasses:ui-pet-trick-" + t)));
            if (m["item"].AsString(null) is { } code && capi.World.GetItem(new AssetLocation(code)) is { } item)
                parts.Add(new ItemStack(item).GetName());
            return string.Join(", ", parts);
        }

        // ---- Stable ----
        // Read-only on the client: PetStable.Tree would create the tree locally, so an absent one is the starting wolf.
        private (int count, int active, ITreeAttribute? tree) StableData()
        {
            var tree = capi.World?.Player?.Entity?.WatchedAttributes.GetTreeAttribute(PetStable.Key);
            return tree == null ? (1, 0, null) : (tree.GetInt("count", 1), tree.GetInt("active", 0), tree);
        }

        private string StableSignature()
        {
            var (count, active, tree) = StableData();
            // Slots and bestiary entries too: a new tame or milestone redraws the open tab.
            var sb = new System.Text.StringBuilder().Append(count).Append('|').Append(active)
                .Append('|').Append(PetStable.MaxSlots(capi.World.Player.Entity))
                .Append('|').Append(PetJournal.Entries(PetJournal.Read(capi.World.Player.Entity)));
            for (int i = 0; i < count; i++)
                sb.Append('|').Append(tree?.GetTreeAttribute("s" + i)?.GetString("species", "")).Append(':')
                    .Append(tree?.GetTreeAttribute("s" + i)?.GetString("name", "")).Append(':')
                    .Append(tree?.GetTreeAttribute("s" + i)?.GetInt("color", 0));
            return sb.ToString();
        }

        private string SlotLabel(ITreeAttribute? tree, int i)
        {
            var slot = tree?.GetTreeAttribute("s" + i);
            string species = Lang.Get("canrpgclasses:pet-look-" + PetSpecies.Migrate(slot?.GetString("species", PetSpecies.Default) ?? PetSpecies.Default));
            string name = slot?.GetString("name", "") ?? "";
            if (tree == null) name = capi.World?.Player?.Entity?.WatchedAttributes.GetString(HunterPetSystem.PetNameKey, "") ?? "";
            if (PetStable.IsRare(slot)) species += ", " + Lang.Get("canrpgclasses:ui-pet-stable-rare");
            return name.Length > 0 ? name + " (" + species + ")" : species;
        }

        private double ComposeStable(GuiComposer compo, double y, double w, double rowH, double gap)
        {
            var (count, active, tree) = StableData();
            compo.AddStaticCustomDraw(ElementBounds.Fixed(0, y - gap, w, 2), EditorStyle.Rule);
            compo.AddStaticText(Lang.Get("canrpgclasses:ui-pet-stable", count, PetStable.MaxSlots(capi.World.Player.Entity)),
                CairoFont.WhiteSmallText().WithColor(EditorStyle.Gold), ElementBounds.Fixed(0, y, w, rowH));
            y += rowH;

            const double btnW = 76;
            for (int i = 0; i < count; i++)
            {
                int slot = i;
                bool isActive = i == active;
                string label = SlotLabel(tree, i);
                compo.AddStaticText(label,
                    CairoFont.WhiteSmallText().WithColor(isActive ? EditorStyle.Gold : GuiStyle.DialogDefaultTextColor),
                    ElementBounds.Fixed(0, y + 4, w - 2 * btnW - 12, rowH));
                // The active row is already gold; its slot holds the colour switch when the species has more than one.
                var slotTree = tree?.GetTreeAttribute("s" + i);
                string species = slotTree?.GetString("species", PetSpecies.Default) ?? PetSpecies.Default;
                int ownRare = slotTree?.GetInt("rareColor", -1) ?? -1;
                if (isActive && PetSpecies.SelectableColors(capi.World, species, ownRare) > 1)
                    compo.AddHoverButton(Lang.Get("canrpgclasses:ui-pet-stable-color"), () => SendStable("color", slot),
                        ElementBounds.Fixed(w - 2 * btnW - 6, y, btnW, rowH));
                else if (isActive)
                    compo.AddStaticText(Lang.Get("canrpgclasses:ui-pet-stable-active"),
                        CairoFont.WhiteDetailText().WithColor(EditorStyle.Muted),
                        ElementBounds.Fixed(w - 2 * btnW - 6, y + 6, btnW, rowH));
                else
                    compo.AddHoverButton(Lang.Get("canrpgclasses:ui-pet-stable-choose"), () => SendStable("select", slot),
                        ElementBounds.Fixed(w - 2 * btnW - 6, y, btnW, rowH));
                if (count > 1)
                    compo.AddHoverButton(Lang.Get("canrpgclasses:ui-pet-stable-release"), () => ConfirmRelease(slot, label),
                        ElementBounds.Fixed(w - btnW, y, btnW, rowH));
                y += rowH + gap;
            }
            return y;
        }

        private bool ConfirmRelease(int slot, string label)
        {
            new GuiDialogConfirm(capi, Lang.Get("canrpgclasses:ui-pet-stable-release-confirm", label), ok =>
            {
                if (ok) SendStable("release", slot);
            }).TryOpen();
            return true;
        }

        private bool SendStable(string action, int slot)
        {
            canrpgclassesModSystem.ClientInstance?.ClientChannel?.SendPacket(new PetStablePacket { Action = action, Slot = slot });
            return true;
        }

        private bool Rename()
        {
            canrpgclassesModSystem.ClientInstance?.ClientChannel?.SendPacket(
                new PetNamePacket { Name = nameBuf.Trim() });
            return true;
        }

        private bool Cast(string localId)
        {
            canrpgclassesModSystem.ClientInstance?.ClientChannel?.SendPacket(
                new SpellRequestPacket { SpellId = "canrpgclasses:" + localId });
            return true;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (listenerId != 0) { capi.Event.UnregisterGameTickListener(listenerId); listenerId = 0; }
        }
    }
}
