using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using canrpgclasses.Core.Execution;

namespace canrpgclasses.Core.HarmonyPatches
{
    /// <summary>Postfix on vanilla bow release: breaks stealth and fires the Split Shot fan. Bow firing is
    /// otherwise invisible to our combat hooks.</summary>
    public static class BowPatches
    {
        public static void Postfix_OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, Item __instance)
        {
            if (byEntity?.World == null || byEntity.World.Side != EnumAppSide.Server) return;
            if (secondsUsed < 0.65f) return; // not drawn enough - no arrow was fired
            if (byEntity.Attributes.GetInt("aimingCancel", 0) == 1) return;

            SpellExecutor.BreakCasterInvisibility(byEntity);

            // aimed_shot bonus only applies to a fresh, fully-drawn shot.
            canrpgclasses.Hunter.HunterShots.ValidateDrawOnRelease(byEntity, secondsUsed);

            int extra = canrpgclasses.Hunter.HunterShots.TakeExtraArrows(byEntity);
            if (extra <= 0) return;

            var arrowSlot = FindArrow(byEntity);
            if (arrowSlot?.Itemstack?.Collectible == null) return;
            var arrowColl = arrowSlot.Itemstack.Collectible;

            float damage = 0f;
            if (__instance.Attributes != null) damage += __instance.Attributes["damage"].AsFloat(0f);
            if (arrowColl.Attributes != null) damage += arrowColl.Attributes["damage"].AsFloat(0f);
            int damageTier = __instance.Attributes?["damageTier"].AsInt(0) ?? 0;
            arrowColl.Variant.TryGetValue("material", out var mat);
            string entityCode = arrowColl.Attributes?["arrowEntityCode"].AsString("arrow-" + (mat ?? "crude")) ?? "arrow-" + (mat ?? "crude");

            var type = byEntity.World.GetEntityType(new AssetLocation(entityCode));
            if (type == null) return;

            float strength = byEntity.Stats.GetBlended("bowDrawingStrength");
            Vec3d pos = byEntity.Pos.XYZ.Add(0.0, byEntity.LocalEyePos.Y, 0.0);

            // Fan extras 6 degrees apart around the aim line, skipping the centre (the vanilla arrow).
            const double stepDeg = 6.0 * Math.PI / 180.0;
            for (int i = 0; i < extra; i++)
            {
                int rank = i - extra / 2;
                if (rank >= 0) rank++;
                double yawOffset = rank * stepDeg;

                if (byEntity.World.ClassRegistry.CreateEntity(type) is not Entity arrow) continue;
                if (arrow is not IProjectile projectile) continue;

                projectile.FiredBy = byEntity;
                projectile.Damage = damage;
                projectile.DamageTier = damageTier;
                projectile.ProjectileStack = new ItemStack(arrowColl, 1);
                projectile.DropOnImpactChance = 0f;
                projectile.WeaponStack = slot?.Itemstack;

                Vec3d velocity = (pos.AheadCopy(1.0, byEntity.Pos.Pitch, byEntity.Pos.Yaw + yawOffset) - pos) * strength;
                arrow.Pos.SetPosWithDimension(byEntity.Pos.BehindCopy(0.21).XYZ.Add(0.0, byEntity.LocalEyePos.Y, 0.0));
                arrow.Pos.Motion.Set(velocity);
                arrow.World = byEntity.World;
                projectile.PreInitialize();
                // Non-collectible so missed extras can't be picked up (arrow dupe). Must follow PreInitialize,
                // which sets Collectible = true.
                if (arrow is EntityProjectileBase epb) epb.Collectible = false;
                byEntity.World.SpawnPriorityEntity(arrow);
            }
        }

        private static ItemSlot? FindArrow(EntityAgent byEntity)
        {
            ItemSlot? found = null;
            byEntity.WalkInventory(invslot =>
            {
                var stack = invslot?.Itemstack;
                if (stack?.Collectible?.Code != null && stack.Collectible.Code.PathStartsWith("arrow-") && stack.StackSize > 0)
                {
                    found = invslot;
                    return false;
                }
                return true;
            });
            return found;
        }
    }
}
