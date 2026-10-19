using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using canrpgclasses.Core.Net;

namespace canrpgclasses.Core.HarmonyPatches
{
    /// <summary>Sends floating combat text to the attacker. Reports HP actually lost (before/after snapshot): the
    /// <c>damage</c> argument is pre-mitigation, armor runs inside ReceiveDamage.</summary>
    public static class CombatTextPatches
    {
        public static void Prefix_ReceiveDamage(Entity __instance, out float __state)
        {
            __state = HealthOf(__instance);
        }

        public static void Postfix_ReceiveDamage(Entity __instance, DamageSource damageSource, float damage, bool __result, float __state)
        {
            if (!__result) return;
            if (damageSource == null || damageSource.Type == EnumDamageType.Heal) return;
            if (__instance?.Api is not ICoreServerAPI) return;

            var attacker = damageSource.CauseEntity ?? damageSource.SourceEntity;
            if (attacker is not EntityPlayer ep || ep.Player is not IServerPlayer sp) return;
            if (__instance == attacker) return;

            // Fall back to the raw argument for entities without a health attribute tree.
            float after = HealthOf(__instance);
            float dealt = (__state >= 0f && after >= 0f) ? __state - after : damage;
            if (dealt <= 0f) return;

            var mod = canrpgclassesModSystem.For(__instance.Api);
            var pos = __instance.Pos;
            mod?.ServerChannel?.SendPacket(new DamageNumberPacket
            {
                X = pos.X,
                Y = pos.Y + __instance.SelectionBox.Y2 * 0.6,
                Z = pos.Z,
                Amount = dealt
            }, sp);
        }

        private static float HealthOf(Entity e) => e?.WatchedAttributes?.GetTreeAttribute("health")?.GetFloat("currenthealth") ?? -1f;
    }
}
