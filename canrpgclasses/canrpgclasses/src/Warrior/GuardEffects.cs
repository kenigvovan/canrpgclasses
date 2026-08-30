using effectshud.src;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Warrior
{
    public static class FullGuardEffectId
    {
        public const string Id = "canrpg_full_guard";
    }

    /// <summary>The total DR cap still applies - Full Guard is meant to reach it.</summary>
    [EffectRegistration(FullGuardEffectId.Id)]
    public class FullGuardEffect : Effect
    {
        private readonly float dr;
        public FullGuardEffect() { dr = BalanceConfig.Spell("full_guard").F("damageReduction", 0.40f); }
        public override void OnStart()  => entity.Stats.Set(StatKeys.DamageReduction, FullGuardEffectId.Id, dr);
        public override void OnExpire() => entity.Stats.Remove(StatKeys.DamageReduction, FullGuardEffectId.Id);
    }

    public static class ShieldGuardEffectId
    {
        public const string Id = "canrpg_shield_guard";
    }

    [EffectRegistration(ShieldGuardEffectId.Id)]
    public class ShieldGuardEffect : Effect
    {
        private readonly float dr;
        public ShieldGuardEffect() { dr = BalanceConfig.Spell("shield_guard").F("damageReduction", 0.30f); }
        public override void OnStart()  => entity.Stats.Set(StatKeys.DamageReduction, ShieldGuardEffectId.Id, dr);
        public override void OnExpire() => entity.Stats.Remove(StatKeys.DamageReduction, ShieldGuardEffectId.Id);
    }
}
