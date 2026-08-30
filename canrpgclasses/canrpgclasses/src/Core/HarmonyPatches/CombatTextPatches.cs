using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using canrpgclasses.Core.Net;

namespace canrpgclasses.Core.HarmonyPatches
{
    /// <summary>
    /// Harmony prefix+postfix on <c>Entity.ReceiveDamage</c> reporting damage dealt back to the attacker's client
    /// as floating combat text. It reports HP actually lost, snapshotted before/after, because the <c>damage</c>
    /// argument is pre-mitigation - vanilla armor reduction runs inside the original method body.
    /// </summary>
    public static class CombatTextPatches
    {
        // Entity.ReceiveDamage(DamageSource damageSource, float damage) -> bool (true when damage was applied)
        public static void Prefix_ReceiveDamage(Entity __instance, out float __state)
        {
            __state = HealthOf(__instance);
        }

        public static void Postfix_ReceiveDamage(Entity __instance, DamageSource damageSource, float damage, bool __result, float __state)
        {
            if (!__result) return;
            if (damageSource == null || damageSource.Type == EnumDamageType.Heal) return;
            if (__instance?.Api is not ICoreServerAPI) return; // only the authoritative server reports damage

            var attacker = damageSource.CauseEntity ?? damageSource.SourceEntity;
            if (attacker is not EntityPlayer ep || ep.Player is not IServerPlayer sp) return;
            if (__instance == attacker) return;

            // Prefer actual HP lost (post-mitigation); fall back to the raw argument for entities without the
            // standard health attribute tree (no EntityBehaviorHealth).
            float after = HealthOf(__instance);
            float dealt = (__state >= 0f && after >= 0f) ? __state - after : damage;
            if (dealt <= 0f) return;

            var mod = canrpgclassesModSystem.For(__instance.Api); // per-hit path: O(1) sided lookup
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
