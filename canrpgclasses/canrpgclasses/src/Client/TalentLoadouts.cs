using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Client
{
    /// <summary>A saved talent build: a name + the spent talents and their ranks (parallel arrays, ready to send
    /// as a <see cref="canrpgclasses.Core.Net.TalentLoadoutPacket"/>).</summary>
    public class TalentLoadout
    {
        public string Name = "Build 1";
        public string[] Ids = Array.Empty<string>();
        public int[] Ranks = Array.Empty<int>();
    }

    /// <summary>Client-side saved talent builds, kept PER CLASS (talents are class-specific). Capturing reads the
    /// current synced ranks; applying is server-validated. Persisted in its own config file.</summary>
    public class TalentLoadouts
    {
        private const string ConfigFile = "canrpgclasses-talents.json";
        private readonly ICoreClientAPI capi;
        private LoadoutFile file = new();

        public TalentLoadouts(ICoreClientAPI capi)
        {
            this.capi = capi;
            try { file = capi.LoadModConfig<LoadoutFile>(ConfigFile) ?? new LoadoutFile(); }
            catch { file = new LoadoutFile(); }
            file.PerClass ??= new Dictionary<string, List<TalentLoadout>>();
        }

        public List<TalentLoadout> For(string? classId)
        {
            string key = classId ?? "";
            if (!file.PerClass.TryGetValue(key, out var list) || list == null) { list = new List<TalentLoadout>(); file.PerClass[key] = list; }
            return list;
        }

        public void Save() { try { capi.StoreModConfig(file, ConfigFile); } catch { } }

        public void AddFromCurrent(string? classId, Entity entity, canrpgclassesModSystem mod)
        {
            string cls = classId ?? "";
            var ids = new List<string>();
            var ranks = new List<int>();
            foreach (var t in mod.Talents.ForClass(cls))
            {
                int r = TalentState.Rank(entity, t.Id);
                if (r > 0) { ids.Add(t.Id); ranks.Add(r); }
            }
            var list = For(cls);
            list.Add(new TalentLoadout { Name = "Build " + (list.Count + 1), Ids = ids.ToArray(), Ranks = ranks.ToArray() });
            Save();
        }

        public void Rename(string? classId, int i, string name)
        {
            var list = For(classId);
            if (i >= 0 && i < list.Count && !string.IsNullOrWhiteSpace(name)) { list[i].Name = name; Save(); }
        }

        public void Delete(string? classId, int i)
        {
            var list = For(classId);
            if (i >= 0 && i < list.Count) { list.RemoveAt(i); Save(); }
        }

        private class LoadoutFile
        {
            public Dictionary<string, List<TalentLoadout>> PerClass = new();
        }
    }
}
