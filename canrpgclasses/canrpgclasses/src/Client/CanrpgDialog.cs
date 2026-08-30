using Vintagestory.API.Client;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Base for every panel the mod opens with a hotkey: adds <see cref="IToggleableGui"/> so one generic
    /// <c>ToggleGui&lt;T&gt;</c> drives them all. <see cref="ToggleKeyCombinationCode"/> stays null because
    /// hotkeys are registered by hand, routing keys, commands and buttons through one singleton.
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
