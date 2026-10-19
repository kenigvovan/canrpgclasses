using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace canrpgclasses.Hunter
{
    /// <summary>Despawns the wolf once its owner is gone; the pet doesn't persist across relog.</summary>
    public class EBHunterPet : EntityBehavior
    {
        public const string Name = "canrpghunterpet";

        // Vanilla key read by the stayclosetoguardedentity follow task; SpellExecutor.IsAlly uses it too.
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

        /// <summary>Must set BaseMaxHealth: EntityBehaviorHealth recomputes maxhealth from it on every hit, so a
        /// directly written maxhealth is lost on the first damage.</summary>
        public void SetMaxHealth(float maxHp)
        {
            var bh = entity.GetBehavior<EntityBehaviorHealth>();
            if (bh == null) return;
            bh.BaseMaxHealth = maxHp;
            bh.MarkDirty();
            bh.Health = bh.MaxHealth;
        }
    }
}
