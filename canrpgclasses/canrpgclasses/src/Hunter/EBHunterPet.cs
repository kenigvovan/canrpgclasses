using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.HarmonyPatches;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Hunter
{
    /// <summary>Despawns the wolf once its owner is gone (the pet doesn't persist across relog), keeps its max health
    /// in step with the owner, and runs Beast Kinship's out-of-combat regeneration.</summary>
    public class EBHunterPet : EntityBehavior
    {
        public const string Name = "canrpghunterpet";

        // Vanilla key read by the stayclosetoguardedentity follow task; SpellExecutor.IsAlly uses it too.
        public const string OwnerUidKey = canrpgclasses.Core.AttrKeys.GuardedByPlayerUid;

        private const float CheckInterval = 2f;
        private float sinceCheck;

        public EBHunterPet(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        // Server-only trick state; a trick never outlives the pet, so it isn't saved.
        private string? trick;
        private Vec3d? trickAt;
        private const double TrickOwnerRange = 6;

        public string? CurrentTrick => trick;

        public void StartTrick(string code)
        {
            StopTrick();
            if (!entity.AnimManager.StartAnimation(code)) return;
            trick = code;
            trickAt = entity.Pos.XYZ;
        }

        public void StopTrick()
        {
            if (trick == null) return;
            entity.AnimManager.StopAnimation(trick);
            trick = null;
            trickAt = null;
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);
            if (entity.Api.Side != EnumAppSide.Server) return;

            // Every tick: a pet that starts moving or gets a target drops its trick at once.
            if (trick != null && (entity.Pos.XYZ.SquareDistanceTo(trickAt!) > 0.09 || HunterPetSystem.ForcedTargetOf(entity) != null))
                StopTrick();

            sinceCheck += deltaTime;
            if (sinceCheck < CheckInterval) return;
            sinceCheck = 0f;

            string uid = entity.WatchedAttributes.GetString(OwnerUidKey, null);
            var owner = uid == null ? null : entity.World.PlayerByUid(uid)?.Entity;
            if (owner == null || !owner.Alive)
            {
                entity.Die(EnumDespawnReason.Removed);
                return;
            }
            if (!entity.Alive) return;

            // The owner left the hunter class: send the pet away, no summon lockout.
            if (Core.Talents.TalentState.CurrentClass(owner) != "hunter")
            {
                HunterPetSystem.DismissActive(owner);
                return;
            }

            if (trick != null && owner.Pos.SquareDistanceTo(entity.Pos.XYZ) > TrickOwnerRange * TrickOwnerRange) StopTrick();

            // Level-ups and Alpha ranks land on a wolf that is already out.
            float wanted = HunterPetSystem.PetMaxHealth(owner, entity);
            var bh = entity.GetBehavior<EntityBehaviorHealth>();
            if (bh != null && System.Math.Abs(bh.BaseMaxHealth - wanted) > 0.01f) SetMaxHealth(wanted, keepFraction: true);

            RegenOutOfCombat(owner, bh);
        }

        private void RegenOutOfCombat(EntityPlayer owner, EntityBehaviorHealth? bh)
        {
            int rank = TalentState.Rank(owner, "hunter:beast_kinship");
            if (rank <= 0 || bh == null || bh.Health >= bh.MaxHealth || StunPatches.IsInCombat(owner)) return;
            float perSecond = BalanceConfig.Talent("hunter:beast_kinship").F("perRank", 0.01f) * rank * bh.MaxHealth;
            entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal },
                perSecond * CheckInterval);
        }

        /// <summary>Must set BaseMaxHealth: EntityBehaviorHealth recomputes maxhealth from it on every hit, so a
        /// directly written maxhealth is lost on the first damage. <paramref name="keepFraction"/> keeps a hurt wolf
        /// hurt; otherwise it comes out at full health.</summary>
        public void SetMaxHealth(float maxHp, bool keepFraction)
        {
            var bh = entity.GetBehavior<EntityBehaviorHealth>();
            if (bh == null) return;
            float fraction = keepFraction && bh.MaxHealth > 0f ? bh.Health / bh.MaxHealth : 1f;
            bh.BaseMaxHealth = maxHp;
            bh.MarkDirty();
            bh.Health = bh.MaxHealth * fraction;
        }

        /// <summary>The owner right-clicks with a pet cosmetic (any item with <c>attributes.petCosmetic</c>) to put it on
        /// a free place, or sneaks and right-clicks empty-handed to take one off. Kept on the stable slot, so it comes
        /// back with every summon.</summary>
        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
            if (entity.Api.Side != EnumAppSide.Server || mode != EnumInteractMode.Interact) return;
            if (byEntity is not EntityPlayer player || player.PlayerUID != entity.WatchedAttributes.GetString(OwnerUidKey, null)) return;

            var worn = entity.WatchedAttributes.GetOrAddTreeAttribute(EBPetCosmetics.Key);
            var stack = itemslot?.Itemstack;
            string? place = EBPetCosmetics.Spec(stack?.Collectible)?["place"].AsString();

            if (place != null && !worn.HasAttribute(place))
            {
                itemslot!.TakeOut(1);
                itemslot.MarkDirty();
                SetCosmetic(player, worn, place, stack!.Collectible.Code.ToString());
                handled = EnumHandling.PreventDefault;
            }
            else if (stack == null && player.Controls.Sneak && worn.Count > 0)
            {
                // One part per click.
                foreach (var entry in worn)
                {
                    var item = entity.World.GetItem(new AssetLocation((entry.Value as StringAttribute)?.value ?? ""));
                    if (item != null)
                    {
                        var back = new ItemStack(item);
                        if (player.Player?.InventoryManager?.TryGiveItemstack(back, true) != true)
                            entity.World.SpawnItemEntity(back, entity.Pos.XYZ);
                    }
                    SetCosmetic(player, worn, entry.Key, null);
                    break;
                }
                handled = EnumHandling.PreventDefault;
            }
        }

        private void SetCosmetic(EntityPlayer owner, ITreeAttribute worn, string place, string? itemCode)
        {
            if (itemCode == null) worn.RemoveAttribute(place);
            else worn.SetString(place, itemCode);
            entity.WatchedAttributes.MarkPathDirty(EBPetCosmetics.Key);
            PetStable.SetActiveCosmetic(owner, place, itemCode);
        }

        public void SetHealthFraction(float fraction)
        {
            var bh = entity.GetBehavior<EntityBehaviorHealth>();
            if (bh == null) return;
            bh.Health = bh.MaxHealth * System.Math.Clamp(fraction, 0.05f, 1f);
        }
    }
}
