namespace canrpgclasses.Client
{
    /// <summary>Open/close surface shared by the mod's panels, so the mod system can drive them all through one
    /// generic toggle helper (see canrpgclassesModSystem.ToggleGui) instead of a copy of the same open/close dance
    /// per window. Implemented once, by <see cref="CanrpgDialog"/> over GuiDialog.</summary>
    public interface IToggleableGui
    {
        bool IsOpened { get; }
        void Open();
        void Close();
    }
}
