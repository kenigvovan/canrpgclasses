using effectshud.src;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Hunter
{
    /// <summary>Not invisibility - the hunter blends into the terrain, so creatures only notice them from much
    /// closer. Lowers the vanilla <c>animalSeekingRange</c> under its own stat key, so it never clobbers the
    /// rogue's Quiet Steps on the same stat.</summary>
    public static class ConcealmentEffectId { public const string Id = "canrpg_concealment"; }

    [EffectRegistration(ConcealmentEffectId.Id)]
    public class ConcealmentEffect : Effect
    {
        private readonly float reduction;
        public ConcealmentEffect() { reduction = BalanceConfig.Spell("concealment").F("seekReduction", 0.8f); }

        // A negative bonus on the 1.0-based stat: -0.8 leaves mobs detecting at about a fifth of normal range.
        public override void OnStart()  => entity.Stats.Set(StatKeys.AnimalSeekingRange, ConcealmentEffectId.Id, -reduction);
        public override void OnExpire() => entity.Stats.Remove(StatKeys.AnimalSeekingRange, ConcealmentEffectId.Id);
    }
}
