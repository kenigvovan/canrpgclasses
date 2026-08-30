using System;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace canrpgclasses.Core.Content
{
    /// <summary>
    /// Content authored in-game, kept in the server's mod-config folder and layered on top of asset files - the
    /// editor never writes into a mod folder, and a content pack can update without losing local edits.
    /// </summary>
    public static class ContentStore
    {
        private const string ContentFile = "canrpgclasses-content.json";

        /// <summary>The in-game authored content, or null when there is none. Read by <see cref="ContentLoader"/>
        /// after the asset files, so an edit wins over a shipped definition of the same id.</summary>
        public static ContentFileModel? Current { get; private set; }

        /// <summary>Server-side. Reads the authored content once at startup, before the registries are built.</summary>
        public static void Load(ICoreServerAPI api)
        {
            try { Current = api.LoadModConfig<ContentFileModel>(ContentFile); }
            catch (Exception e)
            {
                api.Logger.Warning("[canrpgclasses] bad authored-content file: {0}", e.Message);
                Current = null;
            }
        }

        /// <summary>Server-side. Persists the authored content after an in-game edit.</summary>
        public static void Save(ICoreServerAPI api)
        {
            if (Current == null) return;
            try { api.StoreModConfig(Current, ContentFile); }
            catch (Exception e) { api.Logger.Warning("[canrpgclasses] failed to save authored content: {0}", e.Message); }
        }

        /// <summary>Replaces the talents of one class tree with the given set, leaving every other tree alone.
        /// The editor sends a whole tree at once, which is also how a talent gets deleted - it simply isn't in
        /// the list any more.</summary>
        public static void ReplaceTree(string classId, int treeIndex, System.Collections.Generic.List<TalentModel> talents)
        {
            Current ??= new ContentFileModel();
            Current.talents ??= new System.Collections.Generic.List<TalentModel>();

            Current.talents.RemoveAll(t =>
                string.Equals(t.@class, classId, StringComparison.OrdinalIgnoreCase) && t.tree == treeIndex);

            foreach (var t in talents)
            {
                t.@class = classId;
                t.tree = treeIndex;
                Current.talents.Add(t);
            }
        }

        /// <summary>Adds or replaces one authored class. Talents and spells are kept - a class is edited far more
        /// often than it is created, and its tree contents don't belong to this record.</summary>
        public static void ReplaceClass(ClassModel model)
        {
            if (string.IsNullOrWhiteSpace(model.id)) return;
            Current ??= new ContentFileModel();
            Current.classes ??= new System.Collections.Generic.List<ClassModel>();

            Current.classes.RemoveAll(c => string.Equals(c.id, model.id, StringComparison.OrdinalIgnoreCase));
            Current.classes.Add(model);
        }

        /// <summary>Drops an authored class and everything authored that belonged to it. Talents are removed with
        /// it on purpose: left behind they would name a class nobody defines, and every load would report them.</summary>
        public static void RemoveClass(string classId)
        {
            if (Current == null) return;
            Current.classes?.RemoveAll(c => string.Equals(c.id, classId, StringComparison.OrdinalIgnoreCase));
            Current.talents?.RemoveAll(t => string.Equals(t.@class, classId, StringComparison.OrdinalIgnoreCase));
        }

        public static void ReplaceSpell(SpellModel model)
        {
            if (string.IsNullOrWhiteSpace(model.id)) return;
            Current ??= new ContentFileModel();
            Current.spells ??= new System.Collections.Generic.List<SpellModel>();

            Current.spells.RemoveAll(s => string.Equals(s.id, model.id, StringComparison.OrdinalIgnoreCase));
            Current.spells.Add(model);
        }

        /// <summary>Drops an authored spell. What referenced it - a class's starting kit, a talent that granted
        /// it - is left alone and simply reports the missing id on the next load, which is the honest outcome:
        /// silently editing other records to cover a deletion would hide the mistake.</summary>
        public static void RemoveSpell(string spellId)
            => Current?.spells?.RemoveAll(s => string.Equals(s.id, spellId, StringComparison.OrdinalIgnoreCase));

        /// <summary>Server → client sync payload.</summary>
        public static string Serialize()
            => Current == null ? "" : JsonConvert.SerializeObject(Current);

        /// <summary>Client side: adopt the server's authored content. Rebuilding the registries afterwards is the
        /// caller's job.</summary>
        public static void Adopt(string? json, ILogger? logger = null)
        {
            if (string.IsNullOrEmpty(json)) { Current = null; return; }
            try { Current = JsonConvert.DeserializeObject<ContentFileModel>(json!); }
            catch (Exception e)
            {
                logger?.Warning("[canrpgclasses] bad authored content from server: {0}", e.Message);
                Current = null;
            }
        }
    }
}
