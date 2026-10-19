using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Items;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Core.Spells
{
    /// <summary>Which spells an entity may cast (class base + talent unlocks), shared by spellbook and server gate.
    /// Excludes item-bound spells on purpose: a weapon binding is only a trigger, not ownership.</summary>
    public static class SpellAccess
    {
        /// <summary>The full spell-id list the entity may use, in a stable order (base, then granted).</summary>
        public static List<string> Available(Entity? e, canrpgclassesModSystem? mod)
        {
            var list = new List<string>();
            if (e == null || mod == null) return list;

            var cls = mod.Classes.Get(TalentState.CurrentClass(e));
            if (cls != null)
                foreach (var id in cls.BaseSpells)
                    if (!list.Contains(id)) list.Add(id);

            foreach (var id in TalentState.GrantedSpells(e, mod.Talents))
                if (!list.Contains(id)) list.Add(id);

            list.RemoveAll(id => mod.Spells.TryGet(id, out var s) && !s.Enabled);
            return list;
        }

        /// <summary>Whether the entity may cast this (full-id) spell - server-authoritative gate against a forged
        /// request. Cost/cooldown/etc. are still enforced separately in EBSpellCaster.TryCast.</summary>
        public static bool CanCast(Entity? e, canrpgclassesModSystem? mod, string fullSpellId)
            => !string.IsNullOrEmpty(fullSpellId) && Available(e, mod).Contains(fullSpellId);
    }
}
