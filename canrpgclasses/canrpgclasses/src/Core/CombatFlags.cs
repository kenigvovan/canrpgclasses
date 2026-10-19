namespace canrpgclasses.Core
{
    /// <summary>WatchedAttributes keys for timed combat flags, expired by <see cref="canrpgclasses.Core.EB.EBCombatState"/>.
    /// Each <c>*Until</c> is a deadline in server ElapsedMilliseconds.</summary>
    public static class CombatFlags
    {
        public const string Evasion = "canrpgEvasion";
        public const string EvasionUntil = "canrpgEvasionUntilMs";
        public const string Stunned = "canrpgStunned";
        public const string StunUntil = "canrpgStunUntilMs";

        // Bitmask (bit = 1<<(int)ControlType) of the active controls that end the instant the bearer takes
        // damage (Blind, Transmute). Set by ControlState.Apply, read by ControlState.OnDamage.
        public const string BreakOnDamageMask = "canrpgBreakOnDmgMask";

        public const string Absorb = "canrpgAbsorb";
        public const string AbsorbUntil = "canrpgAbsorbUntilMs";
        // Cosmetic (ShieldDomeRenderer): packed A,R,G,B tint of the absorb dome and its shape style
        // (0 = smooth energy dome, 1 = angular ice crystal for frost shields).
        public const string AbsorbColor = "canrpgAbsorbColor";
        public const string AbsorbStyle = "canrpgAbsorbStyle";
        // Cosmetic: shield lifetime in SECONDS at (re)apply. The client can't use AbsorbUntil - it's the server's
        // ElapsedMilliseconds counter and reads as already expired client-side.
        public const string AbsorbDuration = "canrpgAbsorbDurationSec";

        public const string Silenced = "canrpgSilenced";
        public const string SilenceUntil = "canrpgSilenceUntilMs";

        // Bitmask of active control types, synced by ControlState.RefreshCcGate, so per-tick CC checks cost one
        // attribute read. Absent/0 when no CC is active.
        public const string CcMask = "canrpgCcMask";

        // A feared player wanders within a radius of where they were feared (mobs flee via their own AI
        // instead): SrcX/SrcZ are the wander anchor, Heading the current yaw.
        public const string Feared = "canrpgFeared";
        public const string FearUntil = "canrpgFearUntilMs";
        public const string FearSrcX = "canrpgFearSrcX";
        public const string FearSrcZ = "canrpgFearSrcZ";
        public const string FearHeading = "canrpgFearHeading";

        public const string Rooted = "canrpgRooted";
        public const string RootUntil = "canrpgRootUntilMs";

        // Transmute; the old "polymorph" literals stay, they are persisted state.
        public const string Polymorphed = "canrpgPolymorphed";
        public const string PolymorphUntil = "canrpgPolymorphUntilMs";

        // The target's "psychedelic" value from before a blind, so it can be restored rather than zeroed (a real
        // mushroom trip may have been in progress). Its presence also marks blind-vision as active.
        public const string BlindVisionRestore = "canrpgBlindVisionRestore";

        // Set on both sides of any entity-vs-entity damage, refreshed per hit. Gates Stealth (out of combat
        // only); Slip Away is deliberately not gated by it, being the mid-fight escape.
        public const string InCombat = "canrpgInCombat";
        public const string InCombatUntil = "canrpgInCombatUntilMs";

        // Travel-form lock: no swings or casts. Self-chosen, so it lives outside ControlState (no DR); form toggles
        // set Spell.BypassesFormLock. Only Source==Player swings are blocked, so earlier totems keep working.
        public const string FormActionLock = "canrpgFormActionLock";
    }
}
