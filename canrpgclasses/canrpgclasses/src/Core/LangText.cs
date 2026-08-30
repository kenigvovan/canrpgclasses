using Vintagestory.API.Config;

namespace canrpgclasses.Core
{
    /// <summary>
    /// Lang-first text resolution. User-facing names/descriptions live in the lang files
    /// (assets/canrpgclasses/lang/*.json); a registered translation always wins. The code-side
    /// string is just a dev default/fallback, and a humanized id is the last resort.
    /// </summary>
    public static class LangText
    {
        public static string? Get(string key, string? fallback)
        {
            string full = "canrpgclasses:" + key;
            // GetUnformatted, not Get: VS's Lang.Get runs string.Format on the value, which throws on our
            // "{0}" description templates (no args passed here). We substitute the numbers ourselves later
            // (Spell/Talent.DescArgs, GearAffinity.ResolvedDescription).
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
