using System;
using effectshud.src;
using Vintagestory.API.Common;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Warrior
{
    public static class ArmorBreakEffectId
    {
        // Persisted player state, so the literal keeps its original name.
        public const string Id = "canrpg_sunder";
    }

    [EffectRegistration(ArmorBreakEffectId.Id, positive: false)]
    public class ArmorBreakEffect : Effect
    {
        private readonly float perStack;
        public ArmorBreakEffect() { perStack = BalanceConfig.Spell("armor_break").F("perStack", 0.05f); }

        public override void OnShouldEntityReceiveDamage(ref float damage, DamageSource dmgSource)
        {
            if (damage > 0f && dmgSource?.Type != EnumDamageType.Heal)
                damage *= 1f + perStack * Math.Max(1, Tier);
        }
    }
}
