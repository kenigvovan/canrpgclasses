using canrpgclasses.Core.Spells;

namespace canrpgclasses.Client
{
    /// <summary>The colour each magic school is drawn in, shared by the spell hotbar and the character sheet so a
    /// school reads the same everywhere.</summary>
    public static class SchoolPalette
    {
        public static (float R, float G, float B) Rgb(SpellSchool school) => school switch
        {
            SpellSchool.PhysicalMelee => (0.85f, 0.35f, 0.30f),
            SpellSchool.PhysicalRanged => (0.80f, 0.65f, 0.30f),
            SpellSchool.Fire => (0.95f, 0.45f, 0.15f),
            SpellSchool.Frost => (0.40f, 0.75f, 0.95f),
            SpellSchool.Arcane => (0.70f, 0.40f, 0.90f),
            SpellSchool.Holy => (0.95f, 0.90f, 0.55f),
            SpellSchool.Nature => (0.40f, 0.80f, 0.40f),
            SpellSchool.Shadow => (0.45f, 0.35f, 0.55f),
            _ => (0.6f, 0.6f, 0.6f)
        };

        public static string Hex(SpellSchool school)
        {
            var (r, g, b) = Rgb(school);
            return VtmlText.Hex(r, g, b);
        }
    }
}
