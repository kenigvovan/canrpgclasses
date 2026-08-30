using effectshud.src;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Priest
{
    /// <summary>Shadow Guise carries <see cref="AuraEffectIds.Prefix"/> so EBAuras' relog cleanup wipes it.</summary>
    public static class PriestEffectIds
    {
        // Snapshot DoT/HoT effects (per-tick magnitude scales off the caster's spell power at cast time).
        public const string SoothingPrayer = "canrpg_renew";                     // HoT (Holy)
        public const string ShadowBrand = "canrpg_shadow_brand"; // DoT (Shadow)
        public const string ConsumingPlague = "canrpg_consuming_plague";// DoT (Shadow) + self-heal
        public const string SacredFlame = "canrpg_sacred_flame";              // DoT (Holy)

        public const string InnerFlame = "canrpg_inner_flame";
        public const string SuppressPain = "canrpg_suppress_pain";
        public const string ShelteringSpirit = "canrpg_sheltering_spirit";
        public const string Dissipate = "canrpg_dissipate";
        public const string SteadyWill = "canrpg_steady_will"; // stacking DR buff, gained on taking damage (Steady Will talent)

        public const string ShadowGuise = AuraEffectIds.Prefix + "shadow_guise";

        // Shadow Orbs: the Shadow ramp resource (stacking buff, Tier = orb count). Built by Mind Shock + DoT ticks,
        // spent by Consuming Plague (mirror of the mage's Mystic Charges).
        public const string ShadowOrbs = "canrpg_shadow_orbs";
    }

    /// <summary>Numbers live under the <c>shadow_orbs</c> balance entry, so the effect, Mind Shock and the DoT
    /// generators all read one source.</summary>
    public static class ShadowOrbs
    {
        public static int Cap => (int)BalanceConfig.Spell("shadow_orbs").F("cap", 3f);
        public static float Duration => BalanceConfig.Spell("shadow_orbs").F("duration", 20f);

        /// <summary>Passes the target tier explicitly so <see cref="ShadowOrbsEffect.OnStack"/> climbs to it.</summary>
        public static void Grant(Entity caster, int amount = 1)
        {
            if (caster == null || amount <= 0) return;
            int current = caster.GetBehavior<EBEffects>()?.GetEffectTier(PriestEffectIds.ShadowOrbs) ?? 0;
            if (current < 0) current = 0;
            int tier = System.Math.Min(Cap, current + amount);
            SpellExecutor.ApplyEffect(caster, PriestEffectIds.ShadowOrbs, tier, Duration);
        }

        /// <summary>Called from the Shadow DoTs' OnDamaged hook, so maintaining them feeds the orb economy.</summary>
        public static void RollTickGeneration(Entity? caster)
        {
            if (caster?.World == null) return;
            float chance = BalanceConfig.Spell("shadow_orbs").F("dotOrbChance", 0.15f);
            if (chance > 0f && caster.World.Rand.NextDouble() < chance) Grant(caster, 1);
        }
    }

    [EffectRegistration(PriestEffectIds.ShadowOrbs)]
    public class ShadowOrbsEffect : Effect
    {
        private const string Src = "canrpg_shadow_orbs";
        private readonly float powerPerOrb;
        public ShadowOrbsEffect() { powerPerOrb = BalanceConfig.Spell("shadow_orbs").F("shadowPowerPerOrb", 0.12f); }

        private void Apply() => entity.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Shadow), Src, powerPerOrb * System.Math.Max(1, tier));
        private void Clear() => entity.Stats.Remove(StatKeys.SpellpowerFor(SpellSchool.Shadow), Src);

        public override void OnStart() => Apply();

        // A fresh grant arrives as a new instance whose Tier the caller already capped; let the base adopt the higher
        // tier + refresh the window, then re-apply the stat at that tier (OnStart doesn't run on a re-application).
        public override void OnStack(Effect otherEffect) { base.OnStack(otherEffect); Apply(); }

        public override void OnExpire() => Clear();
        public override bool OnDeath() { Clear(); return base.OnDeath(); }
    }

    [EffectRegistration(PriestEffectIds.SteadyWill)]
    public class SteadyWillEffect : Effect
    {
        private const string Src = "canrpgtalent_focusedwill";
        private readonly float perStack;
        public SteadyWillEffect() { perStack = BalanceConfig.Talent("priest:steady_will").F("perRank", 0.03f); }
        public override void OnStart() => entity.Stats.Set(StatKeys.DamageReduction, Src, perStack * tier);
        public override void OnStack(Effect otherEffect)
        {
            if (otherEffect.Tier > tier) tier = otherEffect.Tier; // climb toward the cap, never drop a stack on refresh
            entity.Stats.Set(StatKeys.DamageReduction, Src, perStack * tier);
            ExpireTick = otherEffect.ExpireTick;   // refresh the timer on every hit
            TickCounter = otherEffect.TickCounter;
        }
        public override void OnExpire() => entity.Stats.Remove(StatKeys.DamageReduction, Src);
        public override bool OnDeath() { entity.Stats.Remove(StatKeys.DamageReduction, Src); return base.OnDeath(); }
    }

    /// <summary>The per-tick heal is snapshotted at cast, so it keeps healing at full strength even if the priest
    /// swaps gear or dies mid-HoT.</summary>
    [EffectRegistration(PriestEffectIds.SoothingPrayer)]
    public class SoothingPrayerEffect : SnapshotEffect
    {
        private readonly float coeffPerTick;
        /// <summary>Baked heal applied each tick. Public so it survives relog (OnStart doesn't re-run on deserialize).</summary>
        public float perTick;

        public SoothingPrayerEffect() { coeffPerTick = BalanceConfig.Spell("renew").F("coeffPerTick", 0.5f); }

        protected override void Recompute()
        {
            float healMul = 1f, improved = 0f;
            var caster = Caster;
            if (caster != null)
            {
                healMul = caster.Stats.GetBlended(StatKeys.HealingPower);
                int rank = TalentState.Rank(caster, "priest:lasting_renew");
                if (rank > 0) improved = BalanceConfig.Talent("priest:lasting_renew").F("perRank", 0.10f) * rank;
            }
            perTick = casterSpellPower * coeffPerTick * healMul * (1f + improved);
        }

        public override void OnTick()
        {
            if (perTick <= 0f) return;
            entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, perTick);
        }
    }

    [EffectRegistration(PriestEffectIds.SacredFlame, positive: false)]
    public class SacredFlameDotEffect : SchoolDamageDotEffect
    {
        private readonly float coeff;
        public SacredFlameDotEffect() { coeff = BalanceConfig.Spell("sacred_flame").F("coeffPerTick", 0.3f); }
        protected override SpellSchool School => SpellSchool.Holy;
        protected override float CoeffPerTick => coeff;
    }

    [EffectRegistration(PriestEffectIds.ShelteringSpirit)]
    public class ShelteringSpiritEffect : Effect
    {
        private readonly float boost;
        public ShelteringSpiritEffect() { boost = BalanceConfig.Spell("sheltering_spirit").F("healBoost", 0.40f); }
        public override void OnStart()  => entity.Stats.Set(StatKeys.HealingEffectiveness, PriestEffectIds.ShelteringSpirit, boost);
        public override void OnExpire() => entity.Stats.Remove(StatKeys.HealingEffectiveness, PriestEffectIds.ShelteringSpirit);
    }

    /// <summary>Each tick has a chance to grant the caster a Shadow Orb (see <see cref="ShadowOrbs"/>).</summary>
    [EffectRegistration(PriestEffectIds.ShadowBrand, positive: false)]
    public class ShadowBrandEffect : SchoolDamageDotEffect
    {
        private readonly float coeff;
        public ShadowBrandEffect() { coeff = BalanceConfig.Spell("shadow_brand").F("coeffPerTick", 0.4f); }
        protected override SpellSchool School => SpellSchool.Shadow;
        protected override float CoeffPerTick => coeff;
        protected override void OnDamaged(Entity? caster, float dealtThisTick) => ShadowOrbs.RollTickGeneration(caster);
    }

    /// <summary>The leech fires in <see cref="OnDamaged"/>, so it rides the same snapshot as the damage.</summary>
    [EffectRegistration(PriestEffectIds.ConsumingPlague, positive: false)]
    public class ConsumingPlagueEffect : SchoolDamageDotEffect
    {
        private readonly float coeff;
        private readonly float selfHealFraction;
        public ConsumingPlagueEffect()
        {
            var b = BalanceConfig.Spell("consuming_plague");
            coeff = b.F("coeffPerTick", 0.6f);
            selfHealFraction = b.F("selfHealFraction", 0.5f);
        }
        protected override SpellSchool School => SpellSchool.Shadow;
        protected override float CoeffPerTick => coeff;
        protected override void OnDamaged(Entity? caster, float dealtThisTick)
        {
            if (caster == null) return;
            if (selfHealFraction > 0f)
                caster.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal },
                    dealtThisTick * selfHealFraction);
            // A live plague keeps trickling orbs back after Consuming Plague consumed them - the maintenance loop.
            ShadowOrbs.RollTickGeneration(caster);
        }
    }

    /// <summary>The 50% DR cap in StunPatches still applies.</summary>
    [EffectRegistration(PriestEffectIds.Dissipate)]
    public class DissipateEffect : Effect
    {
        private readonly float dr;
        public DissipateEffect() { dr = BalanceConfig.Spell("dissipate").F("damageReduction", 0.40f); }
        public override void OnStart()  => entity.Stats.Set(StatKeys.DamageReduction, PriestEffectIds.Dissipate, dr);
        public override void OnExpire() => entity.Stats.Remove(StatKeys.DamageReduction, PriestEffectIds.Dissipate);
    }

    /// <summary>Applied infinite and self-only by EBAuras through the spell's ToggleAura impact.</summary>
    [EffectRegistration(PriestEffectIds.ShadowGuise)]
    public class ShadowGuiseEffect : Effect
    {
        private readonly float shadowBonus;
        private readonly float healPenalty;
        public ShadowGuiseEffect()
        {
            var b = BalanceConfig.Spell("shadow_guise");
            shadowBonus = b.F("shadowBonus", 0.15f);
            healPenalty = b.F("healPenalty", 0.20f);
        }
        public override void OnStart()
        {
            entity.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Shadow), PriestEffectIds.ShadowGuise, shadowBonus);
            entity.Stats.Set(StatKeys.HealingPower, PriestEffectIds.ShadowGuise, -healPenalty);
        }
        public override void OnExpire()
        {
            entity.Stats.Remove(StatKeys.SpellpowerFor(SpellSchool.Shadow), PriestEffectIds.ShadowGuise);
            entity.Stats.Remove(StatKeys.HealingPower, PriestEffectIds.ShadowGuise);
        }
    }

    [EffectRegistration(PriestEffectIds.InnerFlame)]
    public class InnerFlameEffect : Effect
    {
        private readonly float dr;
        public InnerFlameEffect() { dr = BalanceConfig.Spell("inner_flame").F("damageReduction", 0.08f); }
        public override void OnStart()  => entity.Stats.Set(StatKeys.DamageReduction, PriestEffectIds.InnerFlame, dr);
        public override void OnExpire() => entity.Stats.Remove(StatKeys.DamageReduction, PriestEffectIds.InnerFlame);
    }

    [EffectRegistration(PriestEffectIds.SuppressPain)]
    public class SuppressPainEffect : Effect
    {
        private readonly float dr;
        public SuppressPainEffect() { dr = BalanceConfig.Spell("suppress_pain").F("damageReduction", 0.40f); }
        public override void OnStart()  => entity.Stats.Set(StatKeys.DamageReduction, PriestEffectIds.SuppressPain, dr);
        public override void OnExpire() => entity.Stats.Remove(StatKeys.DamageReduction, PriestEffectIds.SuppressPain);
    }
}
