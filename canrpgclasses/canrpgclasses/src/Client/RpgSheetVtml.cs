using System.Text;
using Vintagestory.API.Config;

namespace canrpgclasses.Client
{
    /// <summary>Renders the character sheet model as VTML for the tab's richtext elements - kept apart from
    /// <see cref="RpgCharacterSheet"/> so numbers and presentation don't grow into each other.</summary>
    public static class RpgSheetVtml
    {
        /// <summary>Class name and level. Unspent talent points read green - the one line to act on.</summary>
        public static void Header(StringBuilder sb, in SheetHeader h)
        {
            sb.Append(VtmlText.Bold(VtmlText.Gold, h.ClassName)).Append("<br />");
            sb.Append(VtmlText.Color(VtmlText.Accent, Lang.Get("canrpgclasses:sheet-level", h.Level)));
            sb.Append(VtmlText.Color(VtmlText.Muted, "   "));
            sb.Append(VtmlText.Color(VtmlText.RowLabel, Lang.Get("canrpgclasses:sheet-points")));
            sb.Append(VtmlText.Color(VtmlText.Muted, ": "));
            sb.Append(VtmlText.Color(h.AvailPoints > 0 ? VtmlText.Good : VtmlText.Neutral,
                h.AvailPoints + " / " + h.TotalPoints));
        }

        public static void XpLabel(StringBuilder sb, in SheetHeader h)
        {
            sb.Append(VtmlText.Color(VtmlText.Muted, h.MaxLevel
                ? Lang.Get("canrpgclasses:ui-max-level")
                : Lang.Get("canrpgclasses:sheet-xp", h.XpInto, h.XpNext)));
        }

        public static void Section(StringBuilder sb, SheetSection sec)
        {
            sb.Append(VtmlText.Bold(VtmlText.Gold, sec.Title));

            if (sec.Rows.Count == 0)
            {
                sb.Append("<br />").Append(VtmlText.Color(VtmlText.Inactive, Lang.Get("canrpgclasses:sheet-empty")));
                return;
            }

            foreach (var row in sec.Rows)
            {
                sb.Append("<br />").Append(Indent(row.Indent));
                sb.Append(VtmlText.Color(row.LabelHex ?? VtmlText.RowLabel, row.Label));
                if (row.Value.Length == 0) continue; // a sub-heading inside a section carries no value

                sb.Append(VtmlText.Color(VtmlText.Muted, ": "));
                sb.Append(VtmlText.Color(Tone(row.Tone), row.Value));
            }
        }

        private static string Tone(SheetTone tone) => tone switch
        {
            SheetTone.Good => VtmlText.Good,
            SheetTone.Bad => VtmlText.Bad,
            SheetTone.Inactive => VtmlText.Inactive,
            SheetTone.Label => VtmlText.RowLabel,
            _ => VtmlText.Neutral
        };

        /// <summary>Nested rows step in with spaces; swap in a visible marker here if the layout swallows them.</summary>
        private static string Indent(int level) => level switch
        {
            0 => "",
            1 => "   ",
            _ => "      "
        };
    }
}
