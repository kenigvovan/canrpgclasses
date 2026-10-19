using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using effectshud.src;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Core.Effects
{
    /// <summary>Paladin aura effects. Own stat keys so they don't clobber shared effectshud buffs (Sprint and
    /// Zealot's Aura both use <c>walkspeed</c>); the shared <see cref="Prefix"/> allows bulk wipe on load.</summary>
    public static class AuraEffectIds
    {
        public const string Prefix = "canrpgaura_";
        public const string Might = Prefix + "might";   // meleeWeaponsDamage
        public const string Haste = Prefix + "haste";   // walkspeed
        public const string Regen = Prefix + "regen";   // health regen over time

        public static bool IsAuraEffect(string id) => id != null && id.StartsWith(Prefix, System.StringComparison.Ordinal);
    }

    /// <summary>Base aura effect with the Aura of Refuge DR rider, baked from the caster by EBAuras since the ally
    /// can't read the caster's talents. Overrides of OnStart/OnExpire must call base.</summary>
    public abstract class AuraEffectBase : Effect
    {
        public float sanctuaryDr;
        private string SanctKey => effectTypeId + "_safe_ground";

        /// <summary>Bakes caster stats in before apply; fields are public so they survive relog.</summary>
        public virtual void BakeFromCaster(Entity caster) => sanctuaryDr = caster.ReductionStat(StatKeys.AuraSanctuaryDr);

        public override void OnStart() { if (sanctuaryDr > 0f) entity.Stats.Set(StatKeys.DamageReduction, SanctKey, sanctuaryDr); }
        public override void OnExpire() => entity.Stats.Remove(StatKeys.DamageReduction, SanctKey);
        public override bool OnDeath() { entity.Stats.Remove(StatKeys.DamageReduction, SanctKey); return base.OnDeath(); }
    }

    [EffectRegistration(AuraEffectIds.Might)]
    public class AuraMightEffect : AuraEffectBase
    {
        public float value;
        /// <summary>Vengeance rider: fraction of incoming melee reflected to the attacker as Holy.</summary>
        public float reflectFraction;
        public AuraMightEffect() { value = BalanceConfig.Spell("vengeful_aura").F("value", 0.15f); }
        public override void BakeFromCaster(Entity caster) { base.BakeFromCaster(caster); reflectFraction = caster.ReductionStat(StatKeys.AuraReflectFraction); }
        public override void OnStart()  { base.OnStart(); entity.Stats.Set(StatKeys.MeleeWeaponsDamage, AuraEffectIds.Might, value); }
        public override void OnExpire() { base.OnExpire(); entity.Stats.Remove(StatKeys.MeleeWeaponsDamage, AuraEffectIds.Might); }

        // Skip our own spell/reflect damage so two reflecting bearers don't ping-pong.
        public override void OnShouldEntityReceiveDamage(ref float damage, DamageSource dmgSource)
        {
            if (reflectFraction <= 0f || damage <= 0f || dmgSource == null || dmgSource.Type == EnumDamageType.Heal) return;
            if (dmgSource is CanrpgDamageSource) return;
            if (dmgSource.Source != EnumDamageSource.Player && dmgSource.Source != EnumDamageSource.Entity) return;
            var attacker = dmgSource.CauseEntity ?? dmgSource.SourceEntity;
            if (attacker == null || attacker == entity || !attacker.Alive) return;
            attacker.ReceiveDamage(new CanrpgDamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = entity,
                CauseEntity = entity,
                School = SpellSchool.Holy,
                Type = DamageSchools.For(SpellSchool.Holy).EngineType
            }, damage * reflectFraction);
        }
    }

    [EffectRegistration(AuraEffectIds.Haste)]
    public class AuraHasteEffect : AuraEffectBase
    {
        public float value;
        public AuraHasteEffect() { value = BalanceConfig.Spell("zealots_aura").F("value", 0.12f); }
        public override void OnStart()  { base.OnStart(); entity.Stats.Set(StatKeys.WalkSpeed, AuraEffectIds.Haste, value); }
        public override void OnExpire() { base.OnExpire(); entity.Stats.Remove(StatKeys.WalkSpeed, AuraEffectIds.Haste); }
    }

    [EffectRegistration(AuraEffectIds.Regen)]
    public class AuraRegenEffect : AuraEffectBase
    {
        public float hpPerTick;
        public AuraRegenEffect() { hpPerTick = BalanceConfig.Spell("aura_of_faith").F("hpPerTick", 0.5f); }
        public override void BakeFromCaster(Entity caster) { base.BakeFromCaster(caster); hpPerTick *= 1f + caster.ReductionStat(StatKeys.AuraRegenStrength); }
        public override void OnTick()
            => entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, hpPerTick);
    }
}
