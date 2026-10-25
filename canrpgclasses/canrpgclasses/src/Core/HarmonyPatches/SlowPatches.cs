using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace canrpgclasses.Core.HarmonyPatches
{
    /// <summary>Vanilla reads the <c>walkspeed</c> stat only for players; this makes slows (walkslow etc.) work on mobs too.</summary>
    public static class SlowPatches
    {
        // EntityAgent.GetWalkSpeedMultiplier; EntityPlayer's override already applies the stat after calling base.
        public static void Postfix_GetWalkSpeedMultiplier(EntityAgent __instance, ref double __result)
        {
            if (__instance is EntityPlayer) return;
            __result *= GameMath.Clamp(__instance.Stats.GetBlended(StatKeys.WalkSpeed), 0f, 999f);
        }
    }
}
