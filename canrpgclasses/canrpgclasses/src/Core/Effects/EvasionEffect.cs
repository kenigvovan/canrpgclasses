using effectshud.src;

namespace canrpgclasses.Core.Effects
{
    /// <summary>HUD-only buff icon for Evasion. No stats; the dodge roll lives in StunPatches (canrpgEvasion flag).</summary>
    public static class EvasionEffectId
    {
        public const string Id = "canrpg_evasion";
    }

    [EffectRegistration(EvasionEffectId.Id)]
    public class EvasionEffect : Effect { }
}
