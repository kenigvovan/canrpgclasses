namespace canrpgclasses.Core.EB
{
    /// <summary>Why <see cref="EBSpellCaster.TryCast"/> refused a cast; read by AI casters and debug tools.</summary>
    public enum CastFailReason
    {
        None, WrongSide, UnknownSpell, Stunned, Feared, Silenced, Transmuted, FormLocked, WrongForm, InCombat,
        NeedBow, NeedShield, NeedAttribute, OnCooldown, GlobalCooldown, NoTarget, NeedTargetEffect, NoResource, NoCombo, Blocked
    }

    /// <summary>Events that break an in-progress Charge cast.</summary>
    [System.Flags]
    public enum CastInterruptFlags
    {
        None = 0,
        Movement = 1,
        Damage = 2
    }
}
