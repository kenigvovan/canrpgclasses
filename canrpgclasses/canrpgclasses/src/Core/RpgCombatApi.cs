using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Control;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Core
{
    /// <summary>Stable entry points for add-on mods (NPC casters and the like), so they don't bind to internal classes.</summary>
    public static class RpgCombatApi
    {
        public const int ApiLevel = canrpgclassesModSystem.ExtensionApiLevel;

        /// <summary>Full spell id: a bare local id gets the canrpgclasses domain.</summary>
        public static string NormalizeSpellId(string id)
            => id.Contains(':') ? id : canrpgclassesModSystem.ModId + ":" + id;

        public static Spell? GetSpell(ICoreAPI api, string id)
            => canrpgclassesModSystem.For(api) is { } mod && mod.Spells.TryGet(NormalizeSpellId(id), out var spell) ? spell : null;

        public static bool TryCast(EntityAgent caster, string spellId, AimContext aim)
            => caster.GetBehavior<EBSpellCaster>()?.TryCast(NormalizeSpellId(spellId), aim) ?? false;

        public static bool IsCasting(Entity e) => e.GetBehavior<EBSpellCaster>()?.IsCasting ?? false;

        public static void Interrupt(Entity e, bool silent = true) => e.GetBehavior<EBSpellCaster>()?.Interrupt(silent);

        public static bool IsOnCooldown(Entity e, string key) => e.GetBehavior<EBSpellCooldowns>()?.IsOnCooldown(key) ?? false;

        public static bool IsAlly(Entity a, Entity b) => SpellExecutor.IsAlly(a, b);

        /// <summary>Friend/foe override for this side (API level 3). Return null for pairs the add-on doesn't own, so
        /// players, pets and parties keep the core rule. Unregister in the add-on's Dispose.</summary>
        public static void RegisterAllyResolver(ICoreAPI api, System.Func<Entity, Entity, bool?> resolver)
            => canrpgclassesModSystem.For(api)?.RegisterAllyResolver(resolver);

        public static void UnregisterAllyResolver(ICoreAPI api, System.Func<Entity, Entity, bool?> resolver)
            => canrpgclassesModSystem.For(api)?.UnregisterAllyResolver(resolver);

        /// <summary>WatchedAttributes flag for entities whose shape lacks the player animations: casts don't play the spell's
        /// body animation on them (API level 5).</summary>
        public const string SuppressSpellAnimationKey = "canrpgSuppressSpellAnim";

        /// <summary>Changes crowd control durations before diminishing returns (API level 5). Return the new seconds, 0 for
        /// immune. Every registered modifier runs in order. Unregister in the add-on's Dispose.</summary>
        public static void RegisterControlDurationModifier(ICoreAPI api, System.Func<Entity, ControlType, float, Entity?, float> modifier)
            => canrpgclassesModSystem.For(api)?.RegisterControlDurationModifier(modifier);

        public static void UnregisterControlDurationModifier(ICoreAPI api, System.Func<Entity, ControlType, float, Entity?, float> modifier)
            => canrpgclassesModSystem.For(api)?.UnregisterControlDurationModifier(modifier);

        public static bool PreventsCasting(Entity e) => ControlState.PreventsCasting(e);

        /// <summary>Stunned, rooted, transmuted or otherwise unable to act or move (API level 4).</summary>
        public static bool IsControlled(Entity e) => ControlState.PreventsActions(e) || ControlState.PreventsMovement(e);

        /// <summary>Whether the entity carries the named effectshud effect (API level 4).</summary>
        public static bool HasEffect(Entity e, string effectId) => SpellExecutor.HasAnyEffect(e, new[] { effectId });

        public static bool ApplyControl(Entity target, ControlType type, float seconds, Entity? source, bool breakOnDamage = false)
            => ControlState.Apply(target, type, seconds, source, breakOnDamage);
    }
}
