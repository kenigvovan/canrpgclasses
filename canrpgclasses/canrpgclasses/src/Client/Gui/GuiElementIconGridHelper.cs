using Vintagestory.API.Client;

namespace canrpgclasses.Client.Gui
{
    /// <summary>Composer sugar for <see cref="GuiElementIconGrid"/>, matching how vanilla exposes its own
    /// elements (AddSkillItemGrid / GetSkillItemGrid).</summary>
    public static class GuiElementIconGridHelper
    {
        public static GuiComposer AddIconGrid(this GuiComposer composer, ElementBounds bounds, int cols, int rows,
            double cellSize, double labelHeight = 0, double padding = 4, string? key = null)
        {
            if (!composer.Composed)
            {
                composer.AddInteractiveElement(
                    new GuiElementIconGrid(composer.Api, bounds, cols, rows, cellSize, labelHeight, padding), key);
            }
            return composer;
        }

        /// <summary>Null when no grid exists under that key, which is normal.</summary>
        public static GuiElementIconGrid? GetIconGrid(this GuiComposer composer, string key)
            => composer.GetElement(key) as GuiElementIconGrid;
    }
}
