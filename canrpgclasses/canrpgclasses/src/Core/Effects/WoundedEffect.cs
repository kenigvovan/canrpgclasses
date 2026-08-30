using System;
using effectshud.src;
using Vintagestory.API.Common;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Core.Effects
{
    public static class WoundedEffectId
    {
        public const string Id = "canrpg_wounded";
    }

    /// <summary>Mortal wound: cuts the bearer's incoming healing. Catches every heal - ours and vanilla food or
    /// potions alike - by hooking the onDamaged path they all pass through before ApplyHealing.</summary>
    [EffectRegistration(WoundedEffectId.Id, positive: false)]
    public class WoundedEffect : Effect
    {
        public float healCutPercent = BalanceConfig.Spell("wounding_shot").F("healCut", 0.5f);

        public override void OnShouldEntityReceiveDamage(ref float damage, DamageSource dmgSource)
        {
            if (damage > 0f && dmgSource?.Type == EnumDamageType.Heal)
                damage *= 1f - Math.Clamp(healCutPercent, 0f, 1f);
        }
    }
}
