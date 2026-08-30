using effectshud.src;

namespace canrpgclasses.Core.Effects
{
    /// <summary>Cosmetic HUD markers for crowd control. The control itself lives in
    /// <see cref="canrpgclasses.Core.Control.ControlState"/>; these carry no stats and exist only so a controlled
    /// player sees an icon and a countdown.</summary>
    public static class ControlEffectIds
    {
        public const string Stunned = "canrpg_cc_stunned";
        public const string Feared = "canrpg_cc_feared";
        public const string Silenced = "canrpg_cc_silenced";
        public const string Rooted = "canrpg_cc_rooted";
        public const string Polymorphed = "canrpg_cc_polymorphed";
    }

    [EffectRegistration(ControlEffectIds.Stunned, positive: false)]     public class CcStunnedEffect : Effect { }
    [EffectRegistration(ControlEffectIds.Feared, positive: false)]      public class CcFearedEffect : Effect { }
    [EffectRegistration(ControlEffectIds.Silenced, positive: false)]    public class CcSilencedEffect : Effect { }
    [EffectRegistration(ControlEffectIds.Rooted, positive: false)]      public class CcRootedEffect : Effect { }
    [EffectRegistration(ControlEffectIds.Polymorphed, positive: false)] public class CcPolymorphedEffect : Effect { }
}
