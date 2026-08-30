using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using effectshud.src;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Core.Effects
{
    /// <summary>
    /// The paladin aura effects. Each carries its own stat key so it never clobbers a shared effectshud buff -
    /// Sprint and Zealot's Aura coexist on <c>walkspeed</c>. EBAuras applies them infinite and removes them on
    /// leaving range; the shared <see cref="Prefix"/> lets them be wiped in bulk on load.
    /// </summary>
    public static class AuraEffectIds
    {
        public const string Prefix = "canrpgaura_";
        public const string Might = Prefix + "might";   // meleeWeaponsDamage
        public const string Haste = Prefix + "haste";   // walkspeed
        public const string Regen = Prefix + "regen";   // health regen over time

        public static bool IsAuraEffect(string id) => id != null && id.StartsWith(Prefix, System.StringComparison.Ordinal);
    }

    /// <summary>Base for the party-aura effects, carrying the "Aura of Refuge" rider - flat DR while under any of
    /// the caster's auras. The effect runs on the ally and can't read the caster's talents itself, so EBAuras
    /// bakes the value in at apply time. Subclasses overriding OnStart/OnExpire must call base.</summary>
    public abstract class AuraEffectBase : Effect
    {
        public float sanctuaryDr;
        private string SanctKey => effectTypeId + "_safe_ground";

        /// <summary>Bakes the caster's aura-improving stats in before the effect is applied to a recipient. Each
        /// subclass pulls what it cares about; the fields are public so the baked values survive relog.</summary>
        public virtual void BakeFromCaster(Entity caster) => sanctuaryDr = caster.ReductionStat(StatKeys.AuraSanctuaryDr);

        public override void OnStart() { if (sanctuaryDr > 0f) entity.Stats.Set(StatKeys.DamageReduction, SanctKey, sanctuaryDr); }
        public override void OnExpire() => entity.Stats.Remove(StatKeys.DamageReduction, SanctKey);
        public override bool OnDeath() { entity.Stats.Remove(StatKeys.DamageReduction, SanctKey); return base.OnDeath(); }
    }

    [EffectRegistration(AuraEffectIds.Might)]
    public class AuraMightEffect : AuraEffectBase
    {
        public float value;
        /// <summary>Retribution Vengeance rider (baked by EBAuras from the caster's stat): bearers reflect this
        /// fraction of incoming melee hits back at the attacker as Holy damage.</summary>
        public float reflectFraction;
        public AuraMightEffect() { value = BalanceConfig.Spell("vengeful_aura").F("value", 0.15f); }
        public override void BakeFromCaster(Entity caster) { base.BakeFromCaster(caster); reflectFraction = caster.ReductionStat(StatKeys.AuraReflectFraction); }
        public override void OnStart()  { base.OnStart(); entity.Stats.Set(StatKeys.MeleeWeaponsDamage, AuraEffectIds.Might, value); }
        public override void OnExpire() { base.OnExpire(); entity.Stats.Remove(StatKeys.MeleeWeaponsDamage, AuraEffectIds.Might); }

        // Reflect only real melee swings, never our own spell or reflect damage - two reflecting bearers must not
        // ping-pong - and never heals.
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
