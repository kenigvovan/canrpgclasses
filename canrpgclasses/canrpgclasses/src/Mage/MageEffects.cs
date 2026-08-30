using effectshud.src;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage
{
    /// <summary>The guard ids carry <see cref="AuraEffectIds.Prefix"/> so EBAuras' relog cleanup picks them up.</summary>
    public static class MageEffectIds
    {
        public const string MysticGuard = AuraEffectIds.Prefix + "mystic_guard";
        public const string FrostGuard = AuraEffectIds.Prefix + "frost_guard";   // survive: flat damage reduction
        public const string MoltenGuard = AuraEffectIds.Prefix + "molten_guard"; // offense: +spell power (all schools)

        // Frost chill: the mage's own snare marker - both slows the target and flags it as "frozen" for Ice Shard /
        // Icebreaker. A dedicated id (not the generic "walkslow") so only mage frost spells enable the shatter bonus.
        public const string Chilled = "canrpg_chilled";

        public const string Ignite = "canrpg_ignite";           // DoT (Fire), on Cinder Blast
        public const string VolatileEmber = "canrpg_volatile_ember";   // DoT (Fire) + explosion on expire
        public const string Conflagration = "canrpg_conflagration";    // timed +fire spell power
        public const string IcyVeins = "canrpg_icy_veins";       // timed +frost spell power
        public const string MysticSurge = "canrpg_mystic_surge"; // timed +arcane spell power
        public const string HotStreak = "canrpg_hot_streak";     // cosmetic: your next Fire Bolt is instant (Volatile Ember proc)
        public const string FieryHaste = "canrpg_fiery_haste";   // short move-speed buff proc (Fiery Momentum talent)
        public const string ClearMind = "canrpg_clear_mind"; // cosmetic: your next cast is instant (Clear Mind)
        public const string MysticCharges = "canrpg_arcane_charges"; // stacking Arcane ramp buff (Mystic Blast builds, spenders consume)
    }

    [EffectRegistration(MageEffectIds.MysticCharges)]
    public class MysticChargesEffect : Effect
    {
        private const string Src = "canrpg_arcane_charges";
        private readonly float dmgPerCharge, costPerCharge;
        public MysticChargesEffect()
        {
            var b = BalanceConfig.Spell("mystic_blast");
            dmgPerCharge = b.F("chargeDamagePerStack", 0.15f);
            costPerCharge = b.F("chargeCostPerStack", 0.35f);
        }

        private void Apply()
        {
            int stacks = System.Math.Max(1, tier);
            entity.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Arcane), Src, dmgPerCharge * stacks);
            entity.Stats.Set(Spells.MysticBlastSpell.MysticChargeCostStat, Src, costPerCharge * stacks);
        }

        private void Clear()
        {
            entity.Stats.Remove(StatKeys.SpellpowerFor(SpellSchool.Arcane), Src);
            entity.Stats.Remove(Spells.MysticBlastSpell.MysticChargeCostStat, Src);
        }

        public override void OnStart() => Apply();

        // A fresh Mystic Blast application arrives as a new instance whose Tier the executor already computed
        // (min(cap, current+1)). Let the base adopt the higher tier + refresh the window, then re-apply the stats
        // at that tier under our single source key (idempotent) - OnStart doesn't run on a re-application.
        public override void OnStack(Effect otherEffect)
        {
            base.OnStack(otherEffect);
            Apply();
        }

        public override void OnExpire() => Clear();

        // On death effectshud calls OnDeath (not OnExpire); clear the stats here too so a mage who dies at max
        // charges doesn't keep the arcane spell-power / cost stat forever. Mirrors ChilledEffect.
        public override bool OnDeath() { Clear(); return base.OnDeath(); }
    }

    /// <summary>HUD marker only - the instant-cast window itself lives in <see cref="canrpgclasses.Core.EB.EBSpellCaster"/>.</summary>
    [EffectRegistration(MageEffectIds.ClearMind)]
    public class ClearMindEffect : Effect { }

    [EffectRegistration(MageEffectIds.FieryHaste)]
    public class FieryHasteEffect : Effect
    {
        private const string Src = "canrpg_fiery_haste";
        private readonly float amount;
        public FieryHasteEffect() { amount = BalanceConfig.Talent("mage:fire_momentum").F("hasteAmount", 0.15f); }
        public override void OnStart()  => entity.Stats.Set(StatKeys.WalkSpeed, Src, amount);
        public override void OnExpire() => entity.Stats.Remove(StatKeys.WalkSpeed, Src);
        public override bool OnDeath()  { entity.Stats.Remove(StatKeys.WalkSpeed, Src); return base.OnDeath(); }
    }

    /// <summary>HUD marker only - the instant-cast window itself lives in <see cref="canrpgclasses.Core.EB.EBSpellCaster"/>.</summary>
    [EffectRegistration(MageEffectIds.HotStreak)]
    public class HotStreakEffect : Effect { }

    [EffectRegistration(MageEffectIds.MysticGuard)]
    public class MysticGuardEffect : Effect
    {
        private readonly float regen;
        public MysticGuardEffect() { regen = BalanceConfig.Spell("mystic_guard").F("resourceRegen", 0.30f); }
        public override void OnStart()  => entity.Stats.Set(StatKeys.ResourceRegen, MageEffectIds.MysticGuard, regen);
        public override void OnExpire() => entity.Stats.Remove(StatKeys.ResourceRegen, MageEffectIds.MysticGuard);
    }

    /// <summary>Flat DR, capped together with all other DR at 50% in StunPatches.</summary>
    [EffectRegistration(MageEffectIds.FrostGuard)]
    public class FrostGuardEffect : Effect
    {
        private readonly float dr;
        public FrostGuardEffect() { dr = BalanceConfig.Spell("frost_guard").F("damageReduction", 0.10f); }
        public override void OnStart()  => entity.Stats.Set(StatKeys.DamageReduction, MageEffectIds.FrostGuard, dr);
        public override void OnExpire() => entity.Stats.Remove(StatKeys.DamageReduction, MageEffectIds.FrostGuard);
    }

    [EffectRegistration(MageEffectIds.MoltenGuard)]
    public class MoltenGuardEffect : Effect
    {
        private readonly float sp;
        public MoltenGuardEffect() { sp = BalanceConfig.Spell("molten_guard").F("spellPower", 0.10f); }
        public override void OnStart()
        {
            entity.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Fire), MageEffectIds.MoltenGuard, sp);
            entity.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Frost), MageEffectIds.MoltenGuard, sp);
            entity.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Arcane), MageEffectIds.MoltenGuard, sp);
        }
        public override void OnExpire()
        {
            entity.Stats.Remove(StatKeys.SpellpowerFor(SpellSchool.Fire), MageEffectIds.MoltenGuard);
            entity.Stats.Remove(StatKeys.SpellpowerFor(SpellSchool.Frost), MageEffectIds.MoltenGuard);
            entity.Stats.Remove(StatKeys.SpellpowerFor(SpellSchool.Arcane), MageEffectIds.MoltenGuard);
        }
    }

    [EffectRegistration(MageEffectIds.Chilled, positive: false)]
    public class ChilledEffect : Effect
    {
        // 1.0-based walkspeed penalty per amplifier tier (Ice Bolt applies tier 2 -> -0.30). Mirrors WalkSlowEffect.
        private const float SlowPerTier = -0.15f;
        private const string StatSource = "canrpgchilled";

        public override void OnStart() => entity.Stats.Set(StatKeys.WalkSpeed, StatSource, SlowPerTier * tier);

        public override void OnStack(Effect otherEffect)
        {
            if (tier > otherEffect.Tier) return;
            if (tier == otherEffect.Tier) { ExpireTick = otherEffect.ExpireTick; TickCounter = otherEffect.TickCounter; return; }
            tier = otherEffect.Tier;
            entity.Stats.Set(StatKeys.WalkSpeed, StatSource, SlowPerTier * tier);
            ExpireTick = otherEffect.ExpireTick;
            TickCounter = otherEffect.TickCounter;
        }

        public override void OnExpire() => entity.Stats.Set(StatKeys.WalkSpeed, StatSource, 0);

        // On death effectshud calls OnDeath (not OnExpire), so the slow must be cleared here too - otherwise a
        // player who dies while chilled keeps the walkspeed penalty forever. Mirrors the built-in WalkSlowEffect.
        public override bool OnDeath()
        {
            entity.Stats.Set(StatKeys.WalkSpeed, StatSource, 0);
            return base.OnDeath();
        }
    }

    [EffectRegistration(MageEffectIds.Ignite, positive: false)]
    public class IgniteEffect : SchoolDamageDotEffect
    {
        private readonly float coeff;
        public IgniteEffect() { coeff = BalanceConfig.Spell("cinder_blast").F("coeffPerTick", 0.3f); }
        protected override SpellSchool School => SpellSchool.Fire;
        protected override float CoeffPerTick => coeff;
    }

    /// <summary>Detonates on expiry, whether the timer ran out or it was dispelled.</summary>
    [EffectRegistration(MageEffectIds.VolatileEmber, positive: false)]
    public class VolatileEmberEffect : SchoolDamageDotEffect
    {
        private readonly float coeff, explosionCoeff, explosionRadius, instantFireballChance, instantFireballWindow;
        public VolatileEmberEffect()
        {
            var b = BalanceConfig.Spell("volatile_ember");
            coeff = b.F("coeffPerTick", 0.35f);
            explosionCoeff = b.F("explosionCoeff", 1.5f);
            explosionRadius = b.F("explosionRadius", 3f);
            instantFireballChance = b.F("instantFireballChance", 0.15f);
            instantFireballWindow = b.F("instantFireballWindow", 10f);
        }
        protected override SpellSchool School => SpellSchool.Fire;
        protected override float CoeffPerTick => coeff;

        // Each damaging tick has a chance to make the caster's next Fire Bolt instant (a short window).
        protected override void OnDamaged(Entity? caster, float dealtThisTick)
        {
            if (caster?.World == null || instantFireballChance <= 0f) return;
            if (caster.World.Rand.NextDouble() >= instantFireballChance) return;
            caster.GetBehavior<EBSpellCaster>()?.GrantInstantCast("canrpgclasses:fire_bolt", instantFireballWindow, MageEffectIds.HotStreak);
            SpellExecutor.ApplyEffect(caster, MageEffectIds.HotStreak, 1, instantFireballWindow);
            if (caster is EntityPlayer ep && ep.Player is IServerPlayer sp)
                sp.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("canrpgclasses:msg-instant-fire_bolt"), EnumChatType.Notification);
        }

        public override void OnExpire()
        {
            var caster = Caster;
            float dmg = casterSpellPower * explosionCoeff;
            if (dmg <= 0f || entity?.World == null) return;
            foreach (var e in entity.World.GetEntitiesAround(entity.Pos.XYZ, explosionRadius, explosionRadius,
                         en => en.Alive && en is EntityAgent && en != caster))
            {
                if (caster != null && SpellExecutor.IsAlly(caster, e)) continue;
                e.ReceiveDamage(new CanrpgDamageSource
                {
                    Source = EnumDamageSource.Entity,
                    SourceEntity = caster,
                    CauseEntity = caster,
                    School = SpellSchool.Fire,
                    Type = DamageSchools.For(SpellSchool.Fire).EngineType
                }, dmg);
            }
        }
    }

    public abstract class MageSpellPowerBuffEffect : Effect
    {
        private readonly float sp;
        protected MageSpellPowerBuffEffect(string spellConfigId) { sp = BalanceConfig.Spell(spellConfigId).F("spellPower", 0.30f); }
        protected abstract SpellSchool School { get; }
        public override void OnStart()  => entity.Stats.Set(StatKeys.SpellpowerFor(School), effectTypeId, sp);
        public override void OnExpire() => entity.Stats.Remove(StatKeys.SpellpowerFor(School), effectTypeId);
    }

    [EffectRegistration(MageEffectIds.Conflagration)]
    public class ConflagrationEffect : MageSpellPowerBuffEffect
    {
        public ConflagrationEffect() : base("conflagration") { }
        protected override SpellSchool School => SpellSchool.Fire;
    }

    [EffectRegistration(MageEffectIds.IcyVeins)]
    public class IcyVeinsEffect : MageSpellPowerBuffEffect
    {
        public IcyVeinsEffect() : base("icy_veins") { }
        protected override SpellSchool School => SpellSchool.Frost;
    }

    [EffectRegistration(MageEffectIds.MysticSurge)]
    public class MysticSurgeEffect : MageSpellPowerBuffEffect
    {
        public MysticSurgeEffect() : base("mystic_surge") { }
        protected override SpellSchool School => SpellSchool.Arcane;
    }
}
