using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace canrpgclasses.Core.Content
{
    /// <summary>
    /// Collects what went wrong while reading content files, so a mod author gets a straight answer instead of a
    /// silently missing spell. Problems never abort the load - the entry falls back to a sane default.
    /// </summary>
    public class ContentReport
    {
        private readonly List<string> problems = new();
        private readonly ILogger? logger;

        public ContentReport(ILogger? logger) { this.logger = logger; }

        public IReadOnlyList<string> Problems => problems;
        public bool HasProblems => problems.Count > 0;

        /// <summary>The first problem, for a single-line reply (the tree editor's result message).</summary>
        public string? FirstProblem => problems.Count > 0 ? problems[0] : null;

        public void Warn(string where, string message)
        {
            string line = $"{where}: {message}";
            problems.Add(line);
            logger?.Warning("[canrpgclasses] content - {0}", line);
        }

        /// <summary>Parses an enum by name, case-insensitively. An empty value quietly takes the default (the
        /// field was simply omitted); a wrong one is reported with the list of valid names.</summary>
        public T Enum<T>(string? value, T fallback, string where, string field) where T : struct
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            if (System.Enum.TryParse<T>(value, ignoreCase: true, out var parsed)) return parsed;

            Warn(where, $"'{value}' is not a valid {field} - use one of: {string.Join(", ", System.Enum.GetNames(typeof(T)))}");
            return fallback;
        }

        /// <summary>A one-line summary for the reload command and the server log.</summary>
        public string Summary(int classes, int spells, int talents)
            => problems.Count == 0
                ? $"loaded {classes} class(es), {spells} spell(s), {talents} talent(s) from content files"
                : $"loaded {classes} class(es), {spells} spell(s), {talents} talent(s) with {problems.Count} problem(s) - see the log";
    }
}
