using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace canrpgclasses.Hunter
{
    /// <summary>
    /// Server behavior on the hunter's wolf: ticks to despawn the pet once its owner is gone (no persistence
    /// across relog - resummon is free). Owner-scaled HP is pushed here on summon.
    /// </summary>
    public class EBHunterPet : EntityBehavior
    {
        public const string Name = "canrpghunterpet";

        // Owner linkage (set by HunterPetSystem.SummonPet). Same vanilla key the stayclosetoguardedentity
        // follow/teleport task reads, and what SpellExecutor.IsAlly uses to treat the pet as its owner's ally.
        public const string OwnerUidKey = canrpgclasses.Core.AttrKeys.GuardedByPlayerUid;

        private float sinceCheck;

        public EBHunterPet(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);
            if (entity.Api.Side != EnumAppSide.Server) return;

            sinceCheck += deltaTime;
            if (sinceCheck < 2f) return;
            sinceCheck = 0f;

            string uid = entity.WatchedAttributes.GetString(OwnerUidKey, null);
            var owner = uid == null ? null : entity.World.PlayerByUid(uid)?.Entity;
            if (owner == null || !owner.Alive)
            {
                entity.Die(EnumDespawnReason.Removed);
            }
        }

        /// <summary>Sets the pet's max/current health, called by HunterPetSystem on summon after owner scaling
        /// is computed. Must set BaseMaxHealth (not the derived "maxhealth" tree value): EntityBehaviorHealth
        /// recomputes maxhealth = basemaxhealth + modifiers on every hit (UpdateMaxHealth), so a directly-written
        /// maxhealth is wiped on the first damage - the wolf spawned at 57 then dropped to the JSON base 20. Setting
        /// the base makes it stick.</summary>
        public void SetMaxHealth(float maxHp)
        {
            var bh = entity.GetBehavior<EntityBehaviorHealth>();
            if (bh == null) return;
            bh.BaseMaxHealth = maxHp;
            bh.MarkDirty();           // recompute MaxHealth from the new base + any modifiers
            bh.Health = bh.MaxHealth; // spawn at full
        }
    }
}
