using Vintagestory.API.Client;

namespace canrpgclasses.Client.Gui
{
    /// <summary>
    /// Drag between <see cref="GuiElementIconGrid"/>s in one dialog. Release is deferred a render pass since
    /// the composer delivers mouse-up to elements in undefined order.
    /// </summary>
    public class IconDragState
    {
        public bool Active;

        public string? Id;

        public LoadedTexture? Icon;

        /// <summary>Grid the drag started from. It draws the icon following the cursor unless
        /// <see cref="GhostRenderer"/> names someone else.</summary>
        public object? Source;

        /// <summary>Overrides the ghost drawer when the source sits in a <c>BeginClip</c> region and would be
        /// scissored. Configuration, so <see cref="Clear"/> keeps it.</summary>
        public object? GhostRenderer;

        /// <summary>A target already handled the drop this frame; ignore further drop attempts.</summary>
        public bool Consumed;

        public bool Released;

        public void Begin(object source, string id, LoadedTexture? icon)
        {
            Active = true;
            Source = source;
            Id = id;
            Icon = icon;
            Consumed = false;
            Released = false;
        }

        public void Clear()
        {
            Active = false;
            Source = null;
            Id = null;
            Icon = null;
            Consumed = false;
            Released = false;
        }
    }
}
