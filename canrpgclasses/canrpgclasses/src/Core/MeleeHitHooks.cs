using System;
using System.Collections.Generic;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.Core
{
    /// <summary>One on-melee-hit perk. Runs after every landed swing, so it must check its own talent rank and
    /// bail fast.</summary>
    public delegate void MeleeHitHook(Entity attacker, Entity target);

    /// <summary>Registry of talent on-melee-hit perks, same publish/apply pattern as
    /// <see cref="DamageModifiers"/>, so the core trigger never references class content by id.</summary>
    public static class MeleeHitHooks
    {
        private static volatile MeleeHitHook[] current = Array.Empty<MeleeHitHook>();

        /// <summary>Atomically replaces the registered hooks with a freshly scanned set.</summary>
        public static void Publish(IEnumerable<MeleeHitHook> hooks)
            => current = new List<MeleeHitHook>(hooks).ToArray();

        public static void Apply(Entity attacker, Entity target)
        {
            var hooks = current;
            for (int i = 0; i < hooks.Length; i++) hooks[i](attacker, target);
        }
    }
}
