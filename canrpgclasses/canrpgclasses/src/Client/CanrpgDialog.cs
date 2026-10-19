using Vintagestory.API.Client;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Base for hotkey panels, toggled by the generic <c>ToggleGui&lt;T&gt;</c>. <see cref="ToggleKeyCombinationCode"/>
    /// stays null because hotkeys are registered by hand.
    /// </summary>
    public abstract class CanrpgDialog : GuiDialog, IToggleableGui
    {
        protected CanrpgDialog(ICoreClientAPI capi) : base(capi) { }

        public override string ToggleKeyCombinationCode => null!;

        public bool IsOpened => base.IsOpened();

        public void Open() => TryOpen();

        public void Close() => TryClose();
    }
}
