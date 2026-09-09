using Vintagestory.API.Client;
using canrpgclasses.Client.Render;
using Vintagestory.API.Common;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Makes the HUD hotbar clickable. Draws nothing: it exists to catch mouse clicks over the slots before they
    /// reach the world, and - while opened - to hold the cursor free so the player can aim at a slot at all.
    /// Clicks are accepted whenever the cursor is free for any reason (this dialog, vanilla's Alt, an open window).
    /// </summary>
    public class SpellClickInput : GuiDialog
    {
        private readonly SpellHotbar hotbar;
        private readonly HudLayout layout;
        private readonly HudSpellHotbarRenderer hud;
        private readonly Gui.IconDragState drag;

        public SpellClickInput(ICoreClientAPI capi, SpellHotbar hotbar, HudLayout layout,
            HudSpellHotbarRenderer hud, Gui.IconDragState drag)
            : base(capi)
        {
            this.hotbar = hotbar;
            this.layout = layout;
            this.hud = hud;
            this.drag = drag;
            capi.Gui.RegisterDialog(this);
        }

        public override string ToggleKeyCombinationCode => null!;

        /// <summary>A Dialog rather than a HUD so that <see cref="PrefersUngrabbedMouse"/> is honoured - only
        /// dialogs count towards the engine's mouse-grab decision.</summary>
        public override EnumDialogType DialogType => EnumDialogType.Dialog;

        /// <summary>Never takes keyboard focus: movement and the cast hotkeys must keep working.</summary>
        public override bool Focusable => false;

        /// <summary>After real windows (0.5) and the vanilla hotbar (1), so this never steals their clicks.</summary>
        public override double InputOrder => 1.05;

        public override bool PrefersUngrabbedMouse => true;

        /// <summary>Also while closed: a cursor freed by Alt or by someone else's window should still click slots.</summary>
        public override bool ShouldReceiveMouseEvents() => IsOpened() || !capi.Input.MouseGrabbed;

        // Set while the player themselves asked for a free cursor, so the spell wheel closing does not take it away.
        private bool pinned;

        /// <summary>Frees / re-grabs the cursor without opening a visible window.</summary>
        public void ToggleCursor()
        {
            if (IsOpened() && pinned) { pinned = false; TryClose(); return; }
            pinned = true;
            TryOpen(false);
        }

        /// <summary>Frees the cursor for as long as a transient consumer (the spell wheel) needs it.</summary>
        public void OpenCursor() { if (!IsOpened()) TryOpen(false); }

        public void CloseCursor() { if (IsOpened() && !pinned) TryClose(); }

        public override void OnMouseDown(MouseEvent args)
        {
            // The layout panel owns the mouse while it is open - that is where blocks are dragged.
            if (args.Handled || layout.EditMode) return;
            if (args.Button != EnumMouseButton.Left && args.Button != EnumMouseButton.Right) return;

            // A skill being dragged out of the spellbook must not cast on the way past a slot.
            if (drag.Active) return;

            var (bar, slot) = hud.SlotAt(args.X, args.Y);
            if (bar < 0) return;

            if (args.Button == EnumMouseButton.Left) hotbar.CastSlot(bar, slot);
            else hotbar.SetBinding(bar, slot, null);

            args.Handled = true;
        }

        /// <summary>Takes a skill dropped from the spellbook onto an on-screen bar, and otherwise swallows the
        /// release so it can't start a block break on the way out.</summary>
        public override void OnMouseUp(MouseEvent args)
        {
            if (args.Handled || layout.EditMode) return;

            var (bar, slot) = hud.SlotAt(args.X, args.Y);
            if (bar < 0) return;

            if (drag.Active && !drag.Consumed && !string.IsNullOrEmpty(drag.Id))
            {
                drag.Consumed = true;
                drag.Released = true;
                hotbar.SetBinding(bar, slot, drag.Id);
            }

            args.Handled = true;
        }
    }
}
