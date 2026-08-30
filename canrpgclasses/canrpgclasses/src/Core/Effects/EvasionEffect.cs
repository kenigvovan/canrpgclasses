using effectshud.src;

namespace canrpgclasses.Core.Effects
{
    /// <summary>Cosmetic HUD buff for Evasion: surfaces the active dodge window as a buff icon with a live countdown,
    /// so the player can see it's up and how long is left. Carries NO stats - the dodge roll itself lives in
    /// StunPatches (the canrpgEvasion WatchedAttributes flag); this is purely the HUD readout.</summary>
    public static class EvasionEffectId
    {
        public const string Id = "canrpg_evasion";
    }

    [EffectRegistration(EvasionEffectId.Id)]
    public class EvasionEffect : Effect { }
}
