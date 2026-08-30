using effectshud.src;
using Vintagestory.API.Common;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Warrior
{
    public static class EnrageEffectId
    {
        public const string Id = "canrpg_enrage";
    }

    [EffectRegistration(EnrageEffectId.Id)]
    public class EnrageEffect : Effect
    {
        private const string Src = "canrpg_enrage";
        private readonly float dmg, haste;
        public EnrageEffect()
        {
            var b = BalanceConfig.Talent("warrior:frenzy");
            dmg = b.F("enrageDamage", 0.15f);
            haste = b.F("enrageHaste", 0.15f);
        }
        public override void OnStart()
        {
            entity.Stats.Set(StatKeys.MeleeWeaponsDamage, Src, dmg);
            entity.Stats.Set(StatKeys.WalkSpeed, Src, haste);
        }
        public override void OnExpire()
        {
            entity.Stats.Remove(StatKeys.MeleeWeaponsDamage, Src);
            entity.Stats.Remove(StatKeys.WalkSpeed, Src);
        }
        // On death effectshud calls OnDeath (not OnExpire); clear the buffed stats here too so they don't linger.
        public override bool OnDeath() { OnExpire(); return base.OnDeath(); }
    }
}
