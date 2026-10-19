using Vintagestory.API.Client;

namespace canrpgclasses.Client.Gui
{
    /// <summary>
    /// One cell of a <see cref="GuiElementIconGrid"/>. Holds no <c>Spell</c>/<c>Talent</c> references since the
    /// balance-config sync recreates them.
    /// </summary>
    public class IconGridCell
    {
        /// <summary>Spell or talent id. Empty means an empty slot that still accepts drops.</summary>
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

        /// <summary>Plate tint behind the icon (Cairo RGBA). Null = neutral dark plate.</summary>
        public double[]? PlateColor;

        /// <summary>Icon tint (Cairo RGBA). Null = undimmed. Used to grey out what can't be taken yet.</summary>
        public double[]? IconTint;

        /// <summary>Heavier double border, e.g. for a talent that can take a point now.</summary>
        public bool Emphasize;

        /// <summary>Layout placeholder: draws nothing, ignores the mouse.</summary>
        public bool Hidden;

        public bool IsEmpty => string.IsNullOrEmpty(Id);
    }
}
