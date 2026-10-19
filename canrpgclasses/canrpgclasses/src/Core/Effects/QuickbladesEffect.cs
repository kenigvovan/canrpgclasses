using effectshud.src;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Core.Effects
{
    /// <summary>Quickblades stacks (Tier = stack count). Own stat key, not <c>strengthmelee</c>, so per-hit
    /// ConsumeStack doesn't drain Battle Rush and Avenging Light.</summary>
    public static class QuickbladesEffectIds
    {
        // The literal is persisted player state, so it keeps its original name.
        public const string Quickblades = "canrpgclasses:slice_stacks";
    }

    [EffectRegistration(QuickbladesEffectIds.Quickblades)]
    public class QuickbladesEffect : Effect
    {
        private const string StatKey = "canrpgsliceanddice";
        // Melee-damage bonus per stack (rogue.json quickblades.perStack).
        public float perStack;

        public QuickbladesEffect() { perStack = BalanceConfig.Spell("quickblades").F("perStack", 0.06f); }

        public override void OnStart()  => entity.Stats.Set(StatKeys.MeleeWeaponsDamage, StatKey, perStack * Tier);
        public override void OnExpire() => entity.Stats.Set(StatKeys.MeleeWeaponsDamage, StatKey, 0f);

        public override void OnStack(Effect otherEffect)
        {
            if (Tier > otherEffect.Tier) return;
            if (Tier == otherEffect.Tier)
            {
                ExpireTick = otherEffect.ExpireTick;
                TickCounter = otherEffect.TickCounter;
                return;
            }
            Tier = otherEffect.Tier;
            entity.Stats.Set(StatKeys.MeleeWeaponsDamage, StatKey, perStack * Tier);
            ExpireTick = otherEffect.ExpireTick;
            TickCounter = otherEffect.TickCounter;
        }

        public override bool OnDeath()
        {
            entity.Stats.Set(StatKeys.MeleeWeaponsDamage, StatKey, 0f);
            return base.OnDeath();
        }
    }
}
