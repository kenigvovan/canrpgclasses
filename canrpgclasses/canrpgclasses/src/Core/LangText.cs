using Vintagestory.API.Config;

namespace canrpgclasses.Core
{
    /// <summary>Lang-first text: a lang-file translation always wins, the code string is only a fallback.</summary>
    public static class LangText
    {
        public static string? Get(string key, string? fallback)
        {
            string full = "canrpgclasses:" + key;
            // GetUnformatted: Lang.Get runs string.Format and throws on our "{0}" templates, which DescArgs fill later.
            return Lang.HasTranslation(full) ? Lang.GetUnformatted(full) : fallback;
        }

        /// <summary>Turns an id like "rogue:nimble" into "Nimble" for a last-resort display name.</summary>
        public static string Humanize(string id)
        {
            if (string.IsNullOrEmpty(id)) return id ?? "";
            int i = id.IndexOf(':');
            string local = (i >= 0 ? id.Substring(i + 1) : id).Replace('_', ' ').Replace('-', ' ').Trim();
            if (local.Length == 0) return id;
            return char.ToUpperInvariant(local[0]) + local.Substring(1);
        }
    }
}
