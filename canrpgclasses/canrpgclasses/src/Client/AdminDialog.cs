using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using canrpgclasses.Core.Net;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Admin panel (controlserver only): pick an online player and grant/set/remove XP and level. Sends
    /// <see cref="AdminProgressionPacket"/>; the server validates the privilege and applies it to the target.
    /// Opened with O or <c>/canrpgadmin</c>. English-only, like the debug commands.
    /// </summary>
    public class AdminDialog : CanrpgDialog
    {
        private const string ComposerKey = "canrpgadmin";

        private string[] playerUids = System.Array.Empty<string>();
        private string? targetUid;
        private int xpAmount = 100;
        private int levelValue = 1;
        private long listenerId;

        public AdminDialog(ICoreClientAPI capi) : base(capi)
        {
            // The online-player list changes while the panel sits open; a slow poll keeps it current without
            // rebuilding anything the rest of the time.
            listenerId = capi.Event.RegisterGameTickListener(_ => RefreshPlayers(), 2000);
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Compose();
        }

        private void RefreshPlayers()
        {
            if (!IsOpened()) return;
            var uids = CollectPlayers(out _);
            if (uids.Length == playerUids.Length) return;
            Compose();
        }

        private string[] CollectPlayers(out string[] names)
        {
            var uidList = new List<string>();
            var nameList = new List<string>();
            foreach (var p in capi.World.AllOnlinePlayers)
            {
                if (string.IsNullOrEmpty(p?.PlayerUID)) continue;
                uidList.Add(p.PlayerUID);
                nameList.Add(p.PlayerName);
            }
            names = nameList.ToArray();
            return uidList.ToArray();
        }

        private void Compose()
        {
            playerUids = CollectPlayers(out var playerNames);
            if (targetUid == null && playerUids.Length > 0) targetUid = playerUids[0];
            int selected = System.Array.IndexOf(playerUids, targetUid);
            if (selected < 0) selected = 0;

            var row = ElementBounds.Fixed(0, 30, 300, 26);
            var dropBounds = row;
            var xpInput = row.BelowCopy(0, 10).WithFixedWidth(120);
            var xpButton = xpInput.RightCopy(10).WithFixedWidth(160);
            var lvlInput = xpInput.BelowCopy(0, 6);
            var lvlButton = lvlInput.RightCopy(10).WithFixedWidth(160);
            var plusButton = lvlInput.BelowCopy(0, 10).WithFixedWidth(90);
            var minusButton = plusButton.RightCopy(8);
            var resetButton = minusButton.RightCopy(8).WithFixedWidth(102);
            var balanceButton = plusButton.BelowCopy(0, 12).WithFixedWidth(300);
            var classButton = balanceButton.BelowCopy(0, 6).WithFixedWidth(300);
            var spellButton = classButton.BelowCopy(0, 6).WithFixedWidth(300);
            var treeButton = spellButton.BelowCopy(0, 6).WithFixedWidth(300);

            var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(dropBounds, xpInput, xpButton, lvlInput, lvlButton,
                plusButton, minusButton, resetButton, balanceButton, classButton, spellButton, treeButton);

            var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

            ClearComposers();
            SingleComposer = capi.Gui.CreateCompo(ComposerKey, dialogBounds)
                .AddShadedDialogBG(bgBounds, true, 5.0, 0.75f)
                .AddDialogTitleBar("RpgClasses Admin", () => TryClose())
                .BeginChildElements(bgBounds)
                    .AddStaticText("Target player", CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 6, 300, 22))
                    .AddDropDown(playerUids, playerNames, selected, OnPlayerSelected, dropBounds, "players")
                    .AddNumberInput(xpInput, v => int.TryParse(v, out xpAmount), CairoFont.WhiteSmallText(), "xp")
                    .AddSmallButton("Add XP", () => Send(AdminAction.AddXp, xpAmount), xpButton)
                    .AddNumberInput(lvlInput, v => int.TryParse(v, out levelValue), CairoFont.WhiteSmallText(), "lvl")
                    .AddSmallButton("Set Level", () => Send(AdminAction.SetLevel, levelValue), lvlButton)
                    .AddSmallButton("+1 Level", () => Send(AdminAction.AddLevel, 1), plusButton)
                    .AddSmallButton("-1 Level", () => Send(AdminAction.AddLevel, -1), minusButton)
                    .AddSmallButton("Reset (Lv 1)", () => Send(AdminAction.SetLevel, 1), resetButton)
                    .AddSmallButton("Balance editor...", OpenBalanceEditor, balanceButton)
                    .AddSmallButton("Class editor...", OpenClassEditor, classButton)
                    .AddSmallButton("Spell editor...", OpenSpellEditor, spellButton)
                    .AddSmallButton("Talent tree editor...", OpenTreeEditor, treeButton)
                .EndChildElements()
                .Compose();

            SingleComposer.GetNumberInput("xp").SetValue(xpAmount.ToString());
            SingleComposer.GetNumberInput("lvl").SetValue(levelValue.ToString());
        }

        private void OnPlayerSelected(string code, bool selected) => targetUid = code;

        private bool OpenBalanceEditor()
        {
            canrpgclassesModSystem.ClientInstance?.OpenBalanceAdmin();
            return true;
        }

        private bool OpenClassEditor()
        {
            canrpgclassesModSystem.ClientInstance?.OpenClassEditor();
            return true;
        }

        private bool OpenSpellEditor()
        {
            canrpgclassesModSystem.ClientInstance?.OpenSpellEditor();
            return true;
        }

        private bool OpenTreeEditor()
        {
            canrpgclassesModSystem.ClientInstance?.OpenTreeEditor();
            return true;
        }

        private bool Send(AdminAction action, long value)
        {
            var mod = canrpgclassesModSystem.ClientInstance;
            if (mod?.ClientChannel == null || string.IsNullOrEmpty(targetUid)) return true;
            mod.ClientChannel.SendPacket(new AdminProgressionPacket
            {
                TargetUid = targetUid!,
                Action = (int)action,
                Value = value
            });
            return true;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (listenerId != 0) { capi.Event.UnregisterGameTickListener(listenerId); listenerId = 0; }
        }
    }
}
