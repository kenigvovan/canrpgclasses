using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using canrpgclasses.Core;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman
{
    /// <summary><see cref="ImpactAction.PlaceTotem"/> handler. One totem per shaman; a new plant removes the old one.</summary>
    public static class ShamanTotemSystem
    {
        public const string TotemEntityCode = "shamantotem";

        public const string KindSearing = "searing";     // fire turret (Elemental)
        public const string KindStream = "stream";       // group heal pulse (Restoration)
        public const string KindEarth = "earth";         // melee-damage buff pulse (Enhancement)
        public const string KindEarthbind = "earthbind"; // slows nearby enemies (base kit)
        public const string KindManaSpring = "manaspring"; // restores mana to nearby allies (Restoration)

        /// <summary>Owner WA: the planted totem's entity id, so the next plant can remove the old one.</summary>
        public const string OwnerTotemIdKey = "canrpgTotemEntityId";

        private static bool registered;

        public static void Init()
        {
            if (registered) return;
            registered = true;
            SpellExecutor.RegisterImpact(ImpactAction.PlaceTotem, ApplyPlaceTotem);
        }

        private static void ApplyPlaceTotem(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var caster = ctx.Caster;
            if (caster is not EntityPlayer op || op.PlayerUID == null || string.IsNullOrEmpty(imp.TotemKind)) return;
            var world = caster.World;

            long oldId = caster.WatchedAttributes.GetLong(OwnerTotemIdKey, 0);
            if (oldId != 0) world.GetEntityById(oldId)?.Die(EnumDespawnReason.Removed);

            var type = world.GetEntityType(new AssetLocation(canrpgclassesModSystem.ModId, TotemEntityCode));
            if (type == null || world.ClassRegistry.CreateEntity(type) is not EntityAgent totem) return;

            var wa = totem.WatchedAttributes;
            wa.SetString(EBShamanTotem.OwnerUidKey, op.PlayerUID);
            wa.SetString(EBShamanTotem.KindKey, imp.TotemKind!);
            wa.SetLong(EBShamanTotem.ExpiryKey, world.ElapsedMilliseconds + (long)(imp.StatusEffectDuration * 1000f));
            wa.SetFloat(EBShamanTotem.RadiusKey, imp.ZoneRadius > 0f ? imp.ZoneRadius : 8f);
            // Snapshot like a DoT so the values survive a chunk reload without re-reading the caster.
            wa.SetFloat(EBShamanTotem.PowerKey, ctx.SpellPower);
            wa.SetFloat(EBShamanTotem.HealPowerKey, caster.Stats.GetBlended(StatKeys.HealingPower));

            totem.Pos.SetPos(caster.Pos.XYZ);
            totem.World = world;
            world.SpawnEntity(totem);

            wa.MarkPathDirty(EBShamanTotem.OwnerUidKey);
            caster.WatchedAttributes.SetLong(OwnerTotemIdKey, totem.EntityId);
            caster.WatchedAttributes.MarkPathDirty(OwnerTotemIdKey);

            imp.Particles?.Emit(world, totem);
        }

        public static bool IsTotem(Entity? e) => e?.Code?.Path == TotemEntityCode;
    }
}
