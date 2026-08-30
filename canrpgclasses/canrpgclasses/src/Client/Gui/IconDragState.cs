using Vintagestory.API.Client;

namespace canrpgclasses.Client.Gui
{
    /// <summary>
    /// Shared drag between two <see cref="GuiElementIconGrid"/>s in one dialog; vanilla has no generic
    /// drag&amp;drop. The release is deferred to the next render pass because the composer delivers mouse-up to
    /// every element in an undefined order - the source must not clear the drag before the target sees it.
    /// </summary>
    public class IconDragState
    {
        public bool Active;

        public string? Id;

        public LoadedTexture? Icon;

        /// <summary>Grid the drag started from. It draws the icon following the cursor unless
        /// <see cref="GhostRenderer"/> names someone else.</summary>
        public object? Source;

        /// <summary>Grid that draws the cursor ghost, when it must not be the source: a grid inside a
        /// <c>BeginClip</c> region is scissored to it, so its ghost vanishes when the cursor leaves the list.
        /// Configuration, not per-drag state, so <see cref="Clear"/> leaves it alone.</summary>
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
