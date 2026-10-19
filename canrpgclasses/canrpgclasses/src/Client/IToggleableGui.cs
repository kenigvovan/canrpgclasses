namespace canrpgclasses.Client
{
    /// <summary>Open/close surface for canrpgclassesModSystem.ToggleGui; implemented by <see cref="CanrpgDialog"/>.</summary>
    public interface IToggleableGui
    {
        bool IsOpened { get; }
        void Open();
        void Close();
    }
}
