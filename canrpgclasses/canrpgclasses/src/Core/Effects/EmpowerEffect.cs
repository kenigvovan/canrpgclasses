using effectshud.src;

namespace canrpgclasses.Core.Effects
{
    /// <summary>
    /// Ids for the cosmetic "empowered next strike" HUD buffs - one per arming spell, because effectshud derives
    /// the type id from the [EffectRegistration] attribute and a shared class could only show one icon. The
    /// shared <see cref="Prefix"/> is what lets them be stripped in bulk when the empowered hit lands.
    /// </summary>
    public static class EmpowerEffectIds
    {
        public const string Prefix = "canrpg_empower_";
        public const string ZealotsStrike = Prefix + "zealots_strike";
        public const string ViciousStrike = Prefix + "vicious_strike";

        public static string For(string spellLocalId) => Prefix + spellLocalId;
        public static bool IsEmpowerEffect(string id) => id != null && id.StartsWith(Prefix, System.StringComparison.Ordinal);
    }

    // Cosmetic only - the bonus itself lives in the caster's WatchedAttributes. These carry no stats; they just
    // surface the window as a HUD buff with a countdown.
    [EffectRegistration(EmpowerEffectIds.ZealotsStrike)]
    public class ZealotsStrikeEmpowerEffect : Effect { }

    [EffectRegistration(EmpowerEffectIds.ViciousStrike)]
    public class ViciousStrikeEmpowerEffect : Effect { }

    // Warrior empowered strikes.
    [EffectRegistration(EmpowerEffectIds.Prefix + "brutal_strike")] public class BrutalStrikeEmpowerEffect : Effect { }
    [EffectRegistration(EmpowerEffectIds.Prefix + "death_blow")]    public class DeathBlowEmpowerEffect : Effect { }

    // Hunter "next bow shot is empowered" buffs. The bonus lives in WatchedAttributes, armed by HunterShots and
    // consumed on the arrow's hit.
    [EffectRegistration(EmpowerEffectIds.Prefix + "measured_shot")]  public class MeasuredShotEmpowerEffect : Effect { }
    [EffectRegistration(EmpowerEffectIds.Prefix + "finishing_shot")] public class FinishingShotEmpowerEffect : Effect { }
    [EffectRegistration(EmpowerEffectIds.Prefix + "careful_shot")]   public class CarefulShotEmpowerEffect : Effect { }
    [EffectRegistration(EmpowerEffectIds.Prefix + "split_shot")]     public class SplitShotEmpowerEffect : Effect { }
    [EffectRegistration(EmpowerEffectIds.Prefix + "venom_sting")]    public class VenomStingEmpowerEffect : Effect { }
    [EffectRegistration(EmpowerEffectIds.Prefix + "jarring_shot")]   public class JarringShotEmpowerEffect : Effect { }
}
