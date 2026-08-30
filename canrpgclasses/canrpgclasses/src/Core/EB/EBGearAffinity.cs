using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Core.EB
{
    /// <summary>
    /// Applies the current class's intrinsic <see cref="GearAffinity"/> buffs/debuffs, re-evaluated only when
    /// equipment changes. Player inventories may not exist yet at spawn, so subscription is retried until they load.
    /// </summary>
    public class EBGearAffinity : EntityBehavior
    {
        public const string Name = "canrpggearaffinity";
        private const string CharacterInv = "character";
        private const string HotbarInv = "hotbar";

        private bool initialized;
        private long retryCallbackId;

        private ICoreServerAPI? Sapi => entity.Api as ICoreServerAPI;
        private IServerPlayer? ServerPlayer => (entity as EntityPlayer)?.Player as IServerPlayer;

        public EBGearAffinity(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        public override void OnEntitySpawn() { base.OnEntitySpawn(); TryInitDelayed(); }
        public override void OnEntityLoaded() { base.OnEntityLoaded(); TryInitDelayed(); }

        private void TryInitDelayed()
        {
            if (Sapi == null || initialized) return;
            if (!TryInit()) retryCallbackId = Sapi.Event.RegisterCallback(_ => TryInit(), 2000);
        }

        private bool TryInit()
        {
            if (initialized) return true;
            var plr = ServerPlayer;
            var charInv = plr?.InventoryManager?.GetOwnInventory(CharacterInv);
            var hotbar = plr?.InventoryManager?.GetOwnInventory(HotbarInv);
            if (charInv == null || hotbar == null)
            {
                retryCallbackId = Sapi!.Event.RegisterCallback(_ => TryInit(), 2000);
                return false;
            }

            charInv.SlotModified += OnSlotModified;
            hotbar.SlotModified += OnSlotModified;
            Sapi!.Event.AfterActiveSlotChanged += OnActiveSlotChanged;

            initialized = true;
            retryCallbackId = 0;
            Reevaluate();
            return true;
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            var plr = ServerPlayer;
            if (plr != null)
            {
                var charInv = plr.InventoryManager?.GetOwnInventory(CharacterInv);
                if (charInv != null) charInv.SlotModified -= OnSlotModified;
                var hotbar = plr.InventoryManager?.GetOwnInventory(HotbarInv);
                if (hotbar != null) hotbar.SlotModified -= OnSlotModified;
            }
            if (Sapi != null)
            {
                Sapi.Event.AfterActiveSlotChanged -= OnActiveSlotChanged;
                if (retryCallbackId != 0) { Sapi.Event.UnregisterCallback(retryCallbackId); retryCallbackId = 0; }
            }
            initialized = false;
            base.OnEntityDespawn(despawn);
        }

        private void OnSlotModified(int slotId) => Reevaluate();
        private void OnActiveSlotChanged(IServerPlayer plr, ActiveSlotChangeEventArgs args)
        {
            if (plr?.Entity == entity) Reevaluate();
        }

        /// <summary>Public so a class/talent change can force an immediate refresh.</summary>
        public void Refresh() => Reevaluate();

        private void Reevaluate()
        {
            if (entity is not EntityAgent agent) return;
            var mod = canrpgclassesModSystem.For(entity.Api);
            var cls = mod?.Classes.Get(TalentState.CurrentClass(entity));
            if (cls == null) return; // no class → no affinities (the global ones are also gated behind having a class)

            var plr = ServerPlayer;
            var weapon = plr?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Collectible;
            var armorInv = plr?.InventoryManager?.GetOwnInventory(CharacterInv);

            ApplyAffinities(agent, cls.GearAffinities, weapon, armorInv);
            ApplyAffinities(agent, Classes.GearAffinity.Global, weapon, armorInv); // class-agnostic (metal armor → magic resist)

            // plate_training sets maxhealthExtraPoints, which EntityBehaviorHealth caches - recompute so the
            // new max HP takes effect and syncs to the client (other stats are read live).
            entity.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();
        }

        private static void ApplyAffinities(EntityAgent agent, System.Collections.Generic.List<Classes.GearAffinity> affs,
            CollectibleObject? weapon, IInventory? armorInv)
        {
            foreach (var aff in affs)
            {
                bool match = aff.Matches(weapon, armorInv);
                string code = "canrpgaffinity_" + aff.Key;
                foreach (var (stat, val) in aff.Modifiers)
                {
                    if (match) agent.Stats.Set(stat, code, val);
                    else agent.Stats.Remove(stat, code);
                }
            }
        }
    }
}
