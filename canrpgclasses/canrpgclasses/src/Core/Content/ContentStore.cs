using System;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace canrpgclasses.Core.Content
{
    /// <summary>In-game authored content, kept in the server's mod-config so content packs can update without
    /// losing local edits.</summary>
    public static class ContentStore
    {
        private const string ContentFile = "canrpgclasses-content.json";

        /// <summary>Authored content or null; loaded after asset files, so edits win.</summary>
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

        /// <summary>Replaces one tree's talents; a talent missing from the list is deleted.</summary>
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

        /// <summary>Adds or replaces one authored class; its talents and spells are kept.</summary>
        public static void ReplaceClass(ClassModel model)
        {
            if (string.IsNullOrWhiteSpace(model.id)) return;
            Current ??= new ContentFileModel();
            Current.classes ??= new System.Collections.Generic.List<ClassModel>();

            Current.classes.RemoveAll(c => string.Equals(c.id, model.id, StringComparison.OrdinalIgnoreCase));
            Current.classes.Add(model);
        }

        /// <summary>Drops an authored class with its talents, which would otherwise dangle.</summary>
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

        /// <summary>Drops an authored spell. References to it are left alone and reported on the next load.</summary>
        public static void RemoveSpell(string spellId)
            => Current?.spells?.RemoveAll(s => string.Equals(s.id, spellId, StringComparison.OrdinalIgnoreCase));

        /// <summary>Server → client sync payload.</summary>
        public static string Serialize()
            => Current == null ? "" : JsonConvert.SerializeObject(Current);

        /// <summary>Client side: adopt the server's content. The caller rebuilds the registries.</summary>
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
