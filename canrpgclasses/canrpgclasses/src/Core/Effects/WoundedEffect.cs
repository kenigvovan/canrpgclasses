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

    /// <summary>Cuts incoming healing. Hooks the onDamaged path so vanilla heals (food, potions) are caught too.</summary>
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
