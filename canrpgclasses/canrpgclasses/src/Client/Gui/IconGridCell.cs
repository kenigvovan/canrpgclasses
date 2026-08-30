using Vintagestory.API.Client;

namespace canrpgclasses.Client.Gui
{
    /// <summary>
    /// One cell of a <see cref="GuiElementIconGrid"/>. Deliberately dumb: the owning dialog rebuilds these from
    /// live state (never holding on to <c>Spell</c>/<c>Talent</c> objects, which the balance-config sync
    /// recreates), and the grid only draws what it is given.
    /// </summary>
    public class IconGridCell
    {
        /// <summary>What this cell stands for - a spell or talent id. Null/empty means an empty slot, which still
        /// draws its plate and can be dropped onto.</summary>
        public string? Id;

        public LoadedTexture? Icon;

        /// <summary>Two-or-three letter stand-in drawn when there is no icon asset.</summary>
        public string? IconFallback;

        /// <summary>Caption under the icon. Null when the grid was built without label space.</summary>
        public string? Label;

        /// <summary>Small text in the top-left corner - a hotkey letter, or "2/3" for a talent rank.</summary>
        public string? Badge;

        /// <summary>Border colour (Cairo RGBA). Null = the grid's default border.</summary>
        public double[]? FrameColor;

        /// <summary>Tint of the plate behind the icon (Cairo RGBA). Null = the neutral dark plate. This is what
        /// makes a talent's state readable at a glance without staring at the border colour.</summary>
        public double[]? PlateColor;

        /// <summary>Icon tint (Cairo RGBA). Null = undimmed. Used to grey out what can't be taken yet.</summary>
        public double[]? IconTint;

        /// <summary>Draw the border twice, one pixel out - a heavier frame for the cells that want the eye
        /// (a talent you can spend a point on right now).</summary>
        public bool Emphasize;

        /// <summary>Cells that are only a placeholder in the layout (a hole in a talent tree) draw nothing and
        /// don't respond to the mouse.</summary>
        public bool Hidden;

        public bool IsEmpty => string.IsNullOrEmpty(Id);
    }
}
