using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;

using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Core.Classes
{
    /// <summary>Strips what the old class left on its owner before a class change: cast, aura or form, the buffs its
    /// own spells apply, empowers, stealth, combo, pet and totem. Effects from other players' spells stay.</summary>
    public static class ClassCleanup
    {
        public static void Strip(Entity entity, canrpgclassesModSystem mod)
        {
            if (entity is not EntityAgent agent || entity.Api.Side != EnumAppSide.Server) return;

            if (agent.GetBehavior<EBSpellCaster>()?.IsCasting == true) RpgCombatApi.Interrupt(agent);

            // Forms are model swaps held by the aura slot; clearing the slot makes EBAuras drop allies' copies too.
            ModelSwapEffects.CancelActive(entity);
            entity.WatchedAttributes.SetString(AttrKeys.ActiveAura, "");

            foreach (string id in OwnEffectIds(entity, mod)) SpellExecutor.RemoveEffect(entity, id);

            var caster = agent.GetBehavior<EBSpellCaster>();
            caster?.ClearEmpowerHudBuffs();
            var wa = entity.WatchedAttributes;
            wa.RemoveAttribute(SpellExecutor.EmpowerDamageKey);
            wa.RemoveAttribute(SpellExecutor.EmpowerMissingDamageKey);
            wa.RemoveAttribute(SpellExecutor.EmpowerKnockbackKey);
            wa.RemoveAttribute(SpellExecutor.EmpowerSchoolKey);
            wa.RemoveAttribute(SpellExecutor.EmpowerComboKey);
            wa.RemoveAttribute(SpellExecutor.EmpowerUntilKey);
            wa.RemoveAttribute(SpellExecutor.EmpowerSpellKey);

            SpellExecutor.BreakCasterInvisibility(entity);
            ResourceState.ResetCombo(entity);

            Hunter.HunterPetSystem.DismissActive(agent);
            long totemId = wa.GetLong(Shaman.ShamanTotemSystem.OwnerTotemIdKey, 0);
            if (totemId != 0) entity.World.GetEntityById(totemId)?.Die(EnumDespawnReason.Removed);
            wa.RemoveAttribute(Shaman.ShamanTotemSystem.OwnerTotemIdKey);
        }

        /// <summary>Effects the old class's spells (base and talent-granted) put on a target, so on their caster too.</summary>
        private static HashSet<string> OwnEffectIds(Entity entity, canrpgclassesModSystem mod)
        {
            var ids = new HashSet<string>();
            foreach (string spellId in SpellAccess.Available(entity, mod))
            {
                if (!mod.Spells.TryGet(spellId, out var spell) || spell == null) continue;
                if (!string.IsNullOrEmpty(spell.ConsumesCasterEffectId)) ids.Add(spell.ConsumesCasterEffectId!);
                if (!string.IsNullOrEmpty(spell.Deliver.StashEffectId)) ids.Add(spell.Deliver.StashEffectId!);
                foreach (var imp in spell.Impacts)
                {
                    if (!string.IsNullOrEmpty(imp.StatusEffectId)) ids.Add(imp.StatusEffectId!);
                    if (!string.IsNullOrEmpty(imp.ZoneStatusEffectId)) ids.Add(imp.ZoneStatusEffectId!);
                }
            }
            // Only buffs: a debuff of the same id would have come from an enemy of this class.
            var active = entity.GetBehavior<EBEffects>()?.activeEffects;
            ids.RemoveWhere(id => active == null || !active.TryGetValue(id, out var eff) || eff == null || !eff.positive);
            return ids;
        }
    }
}
