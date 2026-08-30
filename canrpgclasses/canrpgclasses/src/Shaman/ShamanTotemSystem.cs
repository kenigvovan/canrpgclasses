using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using canrpgclasses.Core;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman
{
    /// <summary>
    /// Server-side driver for the shaman's totems: the <see cref="ImpactAction.PlaceTotem"/> impact.
    /// One totem at a time - planting a new one drops whatever you had out.
    /// </summary>
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
            wa.SetString(EBShamanTotem.OwnerUidKey, op.PlayerUID); // owner link → IsAlly, and the despawn check
            wa.SetString(EBShamanTotem.KindKey, imp.TotemKind!);
            wa.SetLong(EBShamanTotem.ExpiryKey, world.ElapsedMilliseconds + (long)(imp.StatusEffectDuration * 1000f));
            wa.SetFloat(EBShamanTotem.RadiusKey, imp.ZoneRadius > 0f ? imp.ZoneRadius : 8f);
            // Snapshot the caster's power at plant time (like a DoT): the totem pulses at the strength it was
            // planted with, and the numbers survive a chunk reload rather than re-reading a caster who may be gone.
            wa.SetFloat(EBShamanTotem.PowerKey, ctx.SpellPower);
            wa.SetFloat(EBShamanTotem.HealPowerKey, caster.Stats.GetBlended(StatKeys.HealingPower));

            // Plant it at the caster's feet, on whatever they're standing on. (Pos is authoritative on the server;
            // ServerPos is an obsolete alias for it in this VS build.)
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
