using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;

namespace canrpgclasses.Core.Spells
{
    /// <summary>
    /// Discovers and stores spell instances. One instance per spell type is created at load time and
    /// shared (spells are immutable config, runtime state lives on entity behaviors).
    /// Scan pattern mirrors effectshud's <c>ScanAndRegisterEffects</c>.
    /// </summary>
    public class SpellRegistry
    {
        private readonly Dictionary<string, Spell> spells = new Dictionary<string, Spell>();

        public IReadOnlyDictionary<string, Spell> All => spells;
        public int Count => spells.Count;

        /// <summary>Stash effect ids of spells whose stash stacks are consumed on a landed melee hit
        /// (slice_and_dice style: StashEffectId + a MeleeImpact stash trigger). Precomputed at scan so the
        /// per-swing trigger (EBSpellCaster.DidAttack) doesn't rescan every registered spell on every hit.</summary>
        public IReadOnlyList<string> MeleeStashEffectIds => meleeStashEffectIds;
        private volatile List<string> meleeStashEffectIds = new List<string>();

        public void ScanAssembly(Assembly assembly, ILogger? logger = null)
        {
            foreach (var type in canrpgclasses.Core.ModExtensions.Types(assembly, logger))
            {
                if (type.IsAbstract) continue;

                var attr = type.GetCustomAttribute<SpellRegistrationAttribute>();
                if (attr == null || !type.IsSubclassOf(typeof(Spell))) continue;

                if (Activator.CreateInstance(type) is not Spell spell) continue;
                if (string.IsNullOrEmpty(spell.Id))
                {
                    logger?.Warning("[canrpgclasses] spell {0} has no id, skipping", type.FullName);
                    continue;
                }

                spells[spell.Id] = spell;
                logger?.Notification("[canrpgclasses] registered spell {0}", spell.Id);
            }

            RebuildDerived();
        }

        /// <summary>Registers one spell built at runtime rather than discovered by attribute - the JSON content
        /// loader's way in. Registration is by id, so a later call replaces an earlier spell of the same id.
        /// Call <see cref="RebuildDerived"/> once after a batch.</summary>
        public void Register(Spell spell)
        {
            if (spell != null && !string.IsNullOrEmpty(spell.Id)) spells[spell.Id] = spell;
        }

        /// <summary>Recomputes the lookups derived from the spell set. Separate from the scan so JSON content can
        /// be registered in between.</summary>
        public void RebuildDerived()
        {
            // Rebuild-then-swap so a concurrent per-swing reader never sees a half-built list - the same
            // volatile-swap pattern as DamageModifiers.
            var melee = new List<string>();
            foreach (var spell in spells.Values)
            {
                if (string.IsNullOrEmpty(spell.Deliver.StashEffectId)) continue;
                foreach (var t in spell.Deliver.StashTriggers)
                    if (t.Type == TriggerType.MeleeImpact) { melee.Add(spell.Deliver.StashEffectId!); break; }
            }
            meleeStashEffectIds = melee;
        }

        public Spell? Get(string id) => spells.TryGetValue(id, out var s) ? s : null;

        public bool TryGet(string id, out Spell spell) => spells.TryGetValue(id, out spell!);
    }
}
