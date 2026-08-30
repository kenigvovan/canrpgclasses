using effectshud.src;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Warrior
{
    /// <summary>
    /// The three warrior Stance effects, applied INFINITE + self-only via EBAuras' ToggleAura. The Berserker
    /// "+received damage" side can't be a negative DamageReduction, so it lives in <see cref="WarriorRage"/>'s hook.
    /// </summary>
    public static class StanceEffectIds
    {
        public const string Battle = canrpgclasses.Core.Effects.AuraEffectIds.Prefix + "battle";
        public const string Defense = canrpgclasses.Core.Effects.AuraEffectIds.Prefix + "defense";
        public const string Berserk = canrpgclasses.Core.Effects.AuraEffectIds.Prefix + "berserk";
    }

    [EffectRegistration(StanceEffectIds.Battle)]
    public class OffensiveStanceEffect : Effect
    {
        private readonly float rageGen;
        public OffensiveStanceEffect() { rageGen = BalanceConfig.Spell("offensive_stance").F("rageGen", 0.25f); }
        public override void OnStart()  => entity.Stats.Set(WarriorStatKeys.RageGeneration, StanceEffectIds.Battle, rageGen);
        public override void OnExpire() => entity.Stats.Remove(WarriorStatKeys.RageGeneration, StanceEffectIds.Battle);
    }

    [EffectRegistration(StanceEffectIds.Defense)]
    public class GuardedStanceEffect : Effect
    {
        private readonly float damagePenalty;
        private readonly float damageReduction;
        public GuardedStanceEffect()
        {
            var b = BalanceConfig.Spell("guarded_stance");
            damagePenalty = b.F("damagePenalty", 0.10f);
            damageReduction = b.F("damageReduction", 0.10f);
        }
        public override void OnStart()
        {
            // meleeWeaponsDamage feeds both the swing and our PhysicalMelee spell power (EBRpgStats.GetSpellPower),
            // so the penalty applies to weapon strikes and rage abilities alike.
            entity.Stats.Set(StatKeys.MeleeWeaponsDamage, StanceEffectIds.Defense, -damagePenalty);
            entity.Stats.Set(StatKeys.DamageReduction, StanceEffectIds.Defense, damageReduction);
        }
        public override void OnExpire()
        {
            entity.Stats.Remove(StatKeys.MeleeWeaponsDamage, StanceEffectIds.Defense);
            entity.Stats.Remove(StatKeys.DamageReduction, StanceEffectIds.Defense);
        }
    }

    [EffectRegistration(StanceEffectIds.Berserk)]
    public class RecklessStanceEffect : Effect
    {
        private readonly float damageBonus;
        public RecklessStanceEffect()
        {
            damageBonus = BalanceConfig.Spell("reckless_stance").F("damageBonus", 0.15f);
        }
        public override void OnStart()  => entity.Stats.Set(StatKeys.MeleeWeaponsDamage, StanceEffectIds.Berserk, damageBonus);
        public override void OnExpire() => entity.Stats.Remove(StatKeys.MeleeWeaponsDamage, StanceEffectIds.Berserk);
    }
}
