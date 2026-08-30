using System;
using effectshud.src;
using Vintagestory.API.Common;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Warrior
{
    public static class MortalWoundEffectId
    {
        public const string Id = "canrpg_mortal_wound";
    }

    /// <summary>Cuts the bearer's incoming healing. Sits on the path every heal passes through, so it hits vanilla
    /// food and potions too. Kept separate from the hunter's <see cref="canrpgclasses.Core.Effects.WoundedEffect"/>
    /// so the two anti-heal debuffs balance independently.</summary>
    [EffectRegistration(MortalWoundEffectId.Id, positive: false)]
    public class MortalWoundEffect : Effect
    {
        public float healCutPercent = BalanceConfig.Spell("maiming_strike").F("healCut", 0.5f);

        public override void OnShouldEntityReceiveDamage(ref float damage, DamageSource dmgSource)
        {
            if (damage > 0f && dmgSource?.Type == EnumDamageType.Heal)
                damage *= 1f - Math.Clamp(healCutPercent, 0f, 1f);
        }
    }
}
