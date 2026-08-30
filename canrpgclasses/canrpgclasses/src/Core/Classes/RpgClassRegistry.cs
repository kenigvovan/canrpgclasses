using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;

namespace canrpgclasses.Core.Classes
{
    /// <summary>Discovers and stores playable class definitions (mirrors SpellRegistry).</summary>
    public class RpgClassRegistry
    {
        private readonly Dictionary<string, RpgClassDef> classes = new Dictionary<string, RpgClassDef>();

        public IReadOnlyDictionary<string, RpgClassDef> All => classes;
        public int Count => classes.Count;

        public void ScanAssembly(Assembly assembly, ILogger? logger = null)
        {
            foreach (var type in ModExtensions.Types(assembly, logger))
            {
                if (type.IsAbstract) continue;
                var attr = type.GetCustomAttribute<RpgClassRegistrationAttribute>();
                if (attr == null || !type.IsSubclassOf(typeof(RpgClassDef))) continue;
                if (Activator.CreateInstance(type) is not RpgClassDef def || string.IsNullOrEmpty(def.Id)) continue;
                classes[def.Id] = def;
            }
            logger?.Notification("[canrpgclasses] {0} class(es) registered", classes.Count);
        }

        /// <summary>Registers one class built at runtime rather than discovered by attribute - the JSON content
        /// loader's way in. Registration is by id, so a later call replaces an earlier class of the same id.</summary>
        public void Register(RpgClassDef def)
        {
            if (def != null && !string.IsNullOrEmpty(def.Id)) classes[def.Id] = def;
        }

        public RpgClassDef? Get(string id) => id != null && classes.TryGetValue(id, out var c) ? c : null;
    }
}
