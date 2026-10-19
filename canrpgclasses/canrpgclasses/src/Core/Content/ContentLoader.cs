using System;
using Vintagestory.API.Common;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Core.Content
{
    /// <summary>Registers <c>config/content/*.json</c> from any mod, then <see cref="ContentStore"/> on top. Runs after
    /// the assembly scan, so data can replace a compiled definition of the same id.</summary>
    public static class ContentLoader
    {
        /// <summary>Under <c>config</c> because asset categories are collected before mod code runs, so a custom one
        /// is never scanned. <see cref="Config.BalanceConfig"/> skips this subfolder.</summary>
        public const string AssetPath = "config/content/";

        public static ContentReport Load(ICoreAPI api, SpellRegistry spells, RpgClassRegistry classes,
                                         TalentRegistry talents, ILogger? logger = null)
        {
            var report = new ContentReport(logger);
            int nClasses = 0, nSpells = 0, nTalents = 0;

            // Every mod's assets, not just ours: that is what lets a content pack ship its own class.
            foreach (var asset in api.Assets.GetMany(AssetPath))
            {
                if (!asset.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

                ContentFileModel? file;
                try { file = asset.ToObject<ContentFileModel>(); }
                catch (Exception e)
                {
                    report.Warn(asset.Location.ToString(), "not valid JSON - " + e.Message);
                    continue;
                }
                if (file == null) continue;

                Register(file, asset.Location.ToString(), spells, classes, talents, report,
                         ref nClasses, ref nSpells, ref nTalents);
            }

            // In-game edits win over shipped files. On a client this is whatever the server last sent.
            if (ContentStore.Current is { } authored)
                Register(authored, "in-game editor", spells, classes, talents, report,
                         ref nClasses, ref nSpells, ref nTalents);

            if (nClasses + nSpells + nTalents > 0 || report.HasProblems)
                logger?.Notification("[canrpgclasses] {0}", report.Summary(nClasses, nSpells, nTalents));

            Verify(spells, classes, talents, report);
            return report;
        }

        private static void Register(ContentFileModel file, string origin, SpellRegistry spells,
                                     RpgClassRegistry classes, TalentRegistry talents, ContentReport report,
                                     ref int nClasses, ref int nSpells, ref int nTalents)
        {
            if (file.classes != null)
                foreach (var m in file.classes)
                {
                    if (!RequireId(m.id, origin, "class", report)) continue;
                    classes.Register(new ContentClassDef(m.id!, m, report));
                    nClasses++;
                }

            if (file.spells != null)
                foreach (var m in file.spells)
                {
                    if (!RequireId(m.id, origin, "spell", report)) continue;
                    spells.Register(new ContentSpell(m.id!, m, report));
                    nSpells++;
                }

            if (file.talents != null)
                foreach (var m in file.talents)
                {
                    if (!RequireId(m.id, origin, "talent", report)) continue;
                    if (string.IsNullOrEmpty(m.@class))
                    {
                        report.Warn(m.id!, "talent has no 'class' - it would never show in any tree");
                        continue;
                    }
                    talents.Register(new ContentTalent(m.id!, m, report));
                    nTalents++;
                }
        }

        private static bool RequireId(string? id, string origin, string kind, ContentReport report)
        {
            if (!string.IsNullOrWhiteSpace(id)) return true;
            report.Warn(origin, $"a {kind} entry has no 'id' and was skipped");
            return false;
        }

        /// <summary>Cross-reference checks that only make sense once everything is loaded (dangling spell/talent/tree refs).</summary>
        private static void Verify(SpellRegistry spells, RpgClassRegistry classes, TalentRegistry talents,
                                   ContentReport report)
        {
            foreach (var cls in classes.All.Values)
                foreach (var spellId in cls.BaseSpells)
                    if (spells.Get(spellId) == null)
                        report.Warn(cls.Id, $"baseSpells names '{spellId}', which no spell defines");

            foreach (var t in talents.All.Values)
            {
                var owner = classes.Get(t.ClassId);
                if (owner == null)
                {
                    report.Warn(t.Id, $"belongs to class '{t.ClassId}', which no class defines");
                }
                else if (t.TreeIndex < 0 || t.TreeIndex >= owner.TreeCount)
                {
                    report.Warn(t.Id, $"sits in tree {t.TreeIndex}, but '{t.ClassId}' has {owner.TreeCount}");
                }

                if (!string.IsNullOrEmpty(t.GrantsSpellId) && spells.Get(t.GrantsSpellId!) == null)
                    report.Warn(t.Id, $"grants '{t.GrantsSpellId}', which no spell defines");

                if (!string.IsNullOrEmpty(t.RequiresTalent) && talents.Get(t.RequiresTalent!) == null)
                    report.Warn(t.Id, $"requires '{t.RequiresTalent}', which no talent defines");
            }
        }
    }
}
