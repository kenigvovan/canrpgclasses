using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Resources;

using EBEffects = effectshud.src.EBEffectsAffected;
using EffectsHud = effectshud.src.effectshud;
using EffectBase = effectshud.src.Effect;

namespace canrpgclasses.Core.EB
{
    /// <summary>
    /// Server-side aura ticker (paladin stances): keeps an infinite effectshud effect on the caster and nearby
    /// party allies, removed when they leave range or the aura toggles off. On the first tick after (re)load each
    /// player wipes leftover aura effects, so logging out under an aura can't leave a phantom buff.
    /// </summary>
    public class EBAuras : EntityBehavior
    {
        public const string Name = "canrpgauras";
        public const string ActiveKey = Core.AttrKeys.ActiveAura;
        private const float TickInterval = 0.5f;

        private float accum;
        private bool cleanedOnLoad;
        private readonly HashSet<long> buffed = new();
        private string? appliedEffectId;

        public EBAuras(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        public override void OnGameTick(float dt)
        {
            if (entity.Api.Side != EnumAppSide.Server || !entity.Alive) return;

            if (!cleanedOnLoad) { cleanedOnLoad = true; ClearAuraEffects(entity); }

            accum += dt;
            if (accum < TickInterval) return;
            accum = 0f;

            string auraId = entity.WatchedAttributes.GetString(ActiveKey, "");
            if (string.IsNullOrEmpty(auraId) || entity is not EntityAgent self) { ClearAll(); return; }

            var mod = canrpgclassesModSystem.For(entity.Api);
            if (mod == null || !mod.Spells.TryGet(auraId, out var spell))
            {
                entity.WatchedAttributes.SetString(ActiveKey, "");
                ClearAll();
                return;
            }

            SpellImpact? aura = null;
            foreach (var i in spell.Impacts) if (i.Action == ImpactAction.ToggleAura) { aura = i; break; }
            if (aura == null || string.IsNullOrEmpty(aura.StatusEffectId)) { ClearAll(); return; }

            string effectId = aura.StatusEffectId!;
            if (appliedEffectId != null && appliedEffectId != effectId) ClearAll();
            appliedEffectId = effectId;

            // Upkeep: drain the caster's primary resource for the elapsed interval. If the pool can't cover it,
            // switch the aura off (mirrors ApplyToggleAura: clear the synced flag) and strip everyone's buff.
            if (aura.AuraUpkeepPerSecond > 0f)
            {
                var pool = ResourceState.PrimaryPool(self);
                // Talents reduce upkeep, but the cap keeps a stance from ever being free.
                float reduction = System.Math.Clamp(self.ReductionStat(StatKeys.AuraUpkeepReduction), 0f,
                    BalanceConfig.Global("auraUpkeepReductionCap", 0.9f));
                float cost = aura.AuraUpkeepPerSecond * TickInterval * (1f - reduction);
                if (pool == null || !ResourceState.Has(self, pool, cost))
                {
                    entity.WatchedAttributes.SetString(ActiveKey, "");
                    ClearAll();
                    return;
                }
                ResourceState.Spend(self, pool, cost);
            }

            float radius = spell.Range > 0 ? spell.Range : 8f;
            var center = self.Pos.XYZ;

            var current = new HashSet<long>();
            current.Add(self.EntityId);
            // Don't trust the in-memory `buffed` set alone - verify the effect is ACTUALLY on the caster. If an apply
            // silently failed (or a deserialized effect vanished across a relog), `buffed` would claim it's there and
            // this ticker would never re-apply, leaving a live aura (ActiveAura set, gates working) with no effect
            // doing the work (no stats, no form model, no lock re-assert). Checking the behavior each tick self-heals
            // that within 0.5s.
            bool selfHasEffect = self.GetBehavior<EBEffects>()?.HasEffect(effectId) ?? false;
            if (!buffed.Contains(self.EntityId) || !selfHasEffect) Apply(self, effectId);

            if (!aura.AuraSelfOnly)
                foreach (var e in self.World.GetEntitiesAround(center, radius, radius,
                             en => en != self && en.Alive && en is EntityPlayer))
                {
                    double dx = e.Pos.X - center.X, dz = e.Pos.Z - center.Z;
                    if (dx * dx + dz * dz > radius * radius) continue;   // round aura, not the square box
                    if (!SpellExecutor.IsAlly(self, e)) continue;        // party members only
                    current.Add(e.EntityId);
                    if (!buffed.Contains(e.EntityId)) Apply(e, effectId);
                }

            foreach (var id in buffed)
            {
                if (current.Contains(id)) continue;
                var e = self.World.GetEntityById(id);
                if (e != null) Remove(e, effectId);
            }
            buffed.Clear();
            buffed.UnionWith(current);
        }

        private void Apply(Entity target, string effectId)
        {
            var effMod = entity.Api.ModLoader.GetModSystem<EffectsHud>();
            if (effMod?.effects == null || !effMod.effects.TryGetValue(effectId, out var type))
            {
                // A missing registration would otherwise no-op silently while the ticker marks the target as buffed.
                entity.Api.Logger.Warning("[canrpgclasses] EBAuras.Apply: effect '{0}' not in the effectshud registry - aura does nothing", effectId);
                return;
            }
            if (System.Activator.CreateInstance(type) is not EffectBase eff) return;
            // Aura-improvement talents (Zealous Faith / Aura of Refuge / Retribution Vengeance): let the effect
            // bake the caster's stats into itself, so the boosted aura reaches every recipient (the effect runs on the
            // ally and can't read the caster's talents itself). Baked at apply time - a rank change while the aura is
            // running takes effect on the next re-application (retoggle / leave-and-reenter range). Adding a new aura
            // buff = a new stat + BakeFromCaster override; this ticker stays generic.
            (eff as AuraEffectBase)?.BakeFromCaster(entity);
            eff.infinite = true;
            eff.SetExpiryInGameDays(9999); // far-future + ExpireTick=MaxValue → persists, is sent to the client, HUD "∞"
            EffectsHud.ApplyEffectOnEntity(target, eff);
        }

        private static void Remove(Entity target, string effectId)
        {
            if (target.GetBehavior<EBEffects>()?.TryGetEffect(effectId, out var eff) == true)
                eff?.SetExpiryImmediately();
        }

        /// <summary>Strip our aura effect from everyone we were buffing (aura off / switched / caster despawn).</summary>
        private void ClearAll()
        {
            if (appliedEffectId != null)
                foreach (var id in buffed)
                {
                    var e = entity.World.GetEntityById(id);
                    if (e != null) Remove(e, appliedEffectId);
                }
            buffed.Clear();
            appliedEffectId = null;
        }

        /// <summary>Expire every aura-prefixed effect on one entity (first-tick relog cleanup on self).</summary>
        private static void ClearAuraEffects(Entity e)
        {
            var beh = e.GetBehavior<EBEffects>();
            if (beh?.activeEffects == null) return;
            var toExpire = new List<EffectBase>();
            foreach (var kv in beh.activeEffects)
                // A model-swap form (Spirit Wolf) must SURVIVE relog: expiring it here fires OnExpire -> Restore, which
                // reverts the model, and the re-apply races that Restore. Leave it be - it re-asserts itself each tick
                // (SpiritWolfAuraEffect.OnTick) and the aura ticker refreshes it via OnStack.
                if (AuraEffectIds.IsAuraEffect(kv.Key) && kv.Value != null && kv.Value is not IModelSwapEffect)
                    toExpire.Add(kv.Value);
            foreach (var ef in toExpire) ef.SetExpiryImmediately();
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            ClearAll();
            base.OnEntityDespawn(despawn);
        }
    }
}
