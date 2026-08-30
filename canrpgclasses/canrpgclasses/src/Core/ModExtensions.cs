using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;

namespace canrpgclasses.Core
{
    /// <summary>
    /// Which assemblies the registries scan: ours, plus every loaded mod that references it - the only way a
    /// foreign type could derive from <c>Spell</c>/<c>RpgClassDef</c>/<c>Talent</c>, so it's a precise and cheap
    /// filter. This is the mod's add-on extension point.
    /// </summary>
    public static class ModExtensions
    {
        private static Assembly Self => typeof(ModExtensions).Assembly;

        /// <summary>Ours first (so an add-on can deliberately override one of our ids by registering the same id
        /// - later scans win), then the add-ons in mod-load order. Assemblies that fail to enumerate their
        /// referenced assemblies are skipped rather than taking the load down.</summary>
        public static List<Assembly> ScanTargets(ICoreAPI? api, ILogger? logger = null)
        {
            var list = new List<Assembly> { Self };
            if (api?.ModLoader == null) return list;

            string selfName = Self.GetName().Name ?? "";
            var seen = new HashSet<Assembly> { Self };

            foreach (var mod in api.ModLoader.Mods)
            {
                foreach (var system in mod.Systems)
                {
                    var asm = system.GetType().Assembly;
                    if (!seen.Add(asm)) continue;
                    if (!References(asm, selfName)) continue;

                    list.Add(asm);
                    logger?.Notification("[canrpgclasses] scanning extension assembly {0} (mod {1})",
                        asm.GetName().Name, mod.Info?.ModID ?? "?");
                }
            }

            return list;
        }

        /// <summary>The loadable types of an assembly. A third-party assembly can fail to load some of its types
        /// (a missing optional dependency of its own); <see cref="ReflectionTypeLoadException"/> still carries the
        /// ones that did load, so we register those instead of aborting the whole scan.</summary>
        public static IEnumerable<Type> Types(Assembly asm, ILogger? logger = null)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException e)
            {
                logger?.Warning("[canrpgclasses] {0}: some types could not be loaded, registering the rest",
                    asm.GetName().Name);
                var loaded = new List<Type>();
                foreach (var t in e.Types) if (t != null) loaded.Add(t);
                return loaded;
            }
            catch (Exception e)
            {
                logger?.Warning("[canrpgclasses] could not scan {0}: {1}", asm.GetName().Name, e.Message);
                return Array.Empty<Type>();
            }
        }

        private static bool References(Assembly asm, string selfName)
        {
            if (selfName.Length == 0) return false;
            try
            {
                foreach (var reference in asm.GetReferencedAssemblies())
                    if (string.Equals(reference.Name, selfName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { /* dynamic or unreadable assembly - not an extension we can scan */ }
            return false;
        }
    }
}
