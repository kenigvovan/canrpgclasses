using canrpgclasses.Core.Spells;

namespace canrpgclasses.Druid.Spells
{
    [SpellRegistration("canrpgclasses:bark_hide")]
    public class BarkHideSpell : Spell
    {
        public BarkHideSpell()
        {
            var b = Balance;
            DisplayName = "Bark Hide"; IconName = "barbed-wire";
            School = SpellSchool.Nature; Tier = 1;
            Target.Type = TargetType.Caster; Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct; CastMode = CastMode.Instant;
            BypassesFormLock = true; // a defensive cooldown must be usable even while shifted

            int duration = (int)b.F("duration", 12f);
            DescArgs = new object[] { (int)System.Math.Round(b.F("damageReduction", 0.20f) * 100f), duration };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = DruidEffectIds.BarkHide,
                StatusEffectDuration = duration,
                BloomFx = true,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });
            ConfigureCost(defResource: 0f, defCooldown: b.F("cooldown", 60f));
        }
    }

    /// <summary>Base for a Restoration heal: smart-cast (an allied player under the crosshair, else the caster). Mana.</summary>
    public abstract class DruidHealSpell : Spell
    {
        protected DruidHealSpell()
        {
            School = SpellSchool.Nature;
            Range = 20;
            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct;
        }
    }

    [SpellRegistration("canrpgclasses:verdant_renewal")]
    public class VerdantRenewalSpell : DruidHealSpell
    {
        public VerdantRenewalSpell()
        {
            var b = Balance;
            DisplayName = "Verdant Renewal"; IconName = "pine-tree"; Tier = 1;
            CastMode = CastMode.Instant;
            int duration = (int)b.F("duration", 12f);
            DescArgs = new object[] { b.F("coeffPerTick", 1.2f), duration };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = DruidEffectIds.VerdantRenewal,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.Heal() // application burst
            });
            ConfigureCost(defResource: 25f, defCooldown: 0f);
        }
    }

    [SpellRegistration("canrpgclasses:restorative_bloom")]
    public class RestorativeBloomSpell : DruidHealSpell
    {
        public RestorativeBloomSpell()
        {
            var b = Balance;
            DisplayName = "Restorative Bloom"; IconName = "leaf-swirl"; Tier = 2;
            CastMode = CastMode.Charge; CastDuration = b.F("cast", 1.5f);
            int duration = (int)b.F("duration", 9f);
            float coeff = b.F("healCoeff", 4f);
            DescArgs = new object[] { coeff, b.F("coeffPerTick", 1.0f), duration };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = coeff,
                BloomFx = true,
                Particles = ParticleSpec.Heal()
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = DruidEffectIds.RestorativeBloom,
                StatusEffectDuration = duration
            });
            ConfigureCost(defResource: 35f, defCooldown: 0f);
        }
    }

    [SpellRegistration("canrpgclasses:mending_touch")]
    public class MendingTouchSpell : DruidHealSpell
    {
        public MendingTouchSpell()
        {
            var b = Balance;
            DisplayName = "Mending Touch"; IconName = "linden-leaf"; Tier = 2;
            CastMode = CastMode.Charge; CastDuration = b.F("cast", 2.6f);
            float coeff = b.F("healCoeff", 9f);
            DescArgs = new object[] { coeff };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = coeff,
                LandFxRing = true,
                Particles = ParticleSpec.Heal()
            });
            ConfigureCost(defResource: 40f, defCooldown: 0f);
        }
    }

    /// <summary>Cashes in one of the druid's own heal-over-times on the target. With no HoT there the cast is refused
    /// outright and costs nothing.</summary>
    [SpellRegistration("canrpgclasses:instant_mend")]
    public class InstantMendSpell : DruidHealSpell
    {
        public InstantMendSpell()
        {
            var b = Balance;
            DisplayName = "Instant Mend"; IconName = "acorn"; Tier = 3;
            CastMode = CastMode.Instant;
            float coeff = b.F("healCoeff", 6f);
            DescArgs = new object[] { coeff };
            RequiresTargetEffectIds = DruidEffectIds.HealOverTimeIds; // gate: something must be ticking to cash in
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = coeff,
                ConsumeTargetEffectIds = DruidEffectIds.HealOverTimeIds, // and it eats exactly one of them
                BloomFx = true,
                Particles = ParticleSpec.Heal()
            });
            ConfigureCost(defResource: 30f, defCooldown: 8f);
        }
    }

    [SpellRegistration("canrpgclasses:spreading_bloom")]
    public class SpreadingBloomSpell : Spell
    {
        public SpreadingBloomSpell()
        {
            var b = Balance;
            DisplayName = "Spreading Bloom"; IconName = "willow-tree";
            School = SpellSchool.Nature; Tier = 3; Range = b.F("radius", 8f);
            Target.Type = TargetType.Area; Target.Affinity = TargetAffinity.Ally;
            Target.AreaIncludeCaster = true;
            Deliver.Type = DeliveryType.Direct; CastMode = CastMode.Instant;
            int duration = (int)b.F("duration", 7f);
            DescArgs = new object[] { b.F("coeffPerTick", 0.8f), duration };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = DruidEffectIds.SpreadingBloom,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.Heal() // application burst on each ally hit
            });
            ConfigureCost(defResource: 45f, defCooldown: 8f);
        }
    }

    [SpellRegistration("canrpgclasses:living_blossom")]
    public class LivingBlossomSpell : DruidHealSpell
    {
        public LivingBlossomSpell()
        {
            var b = Balance;
            DisplayName = "Living Blossom"; IconName = "holy-oak"; Tier = 2;
            CastMode = CastMode.Instant;
            int duration = (int)b.F("duration", 7f);
            DescArgs = new object[] { b.F("coeffPerTick", 0.9f), duration };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = DruidEffectIds.LivingBlossom,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.Heal()
            });
            ConfigureCost(defResource: 20f, defCooldown: 0f);
        }
    }

    [SpellRegistration("canrpgclasses:quickening")]
    public class QuickeningSpell : Spell
    {
        public QuickeningSpell()
        {
            var b = Balance;
            DisplayName = "Quickening"; IconName = "sundial";
            School = SpellSchool.Nature; Tier = 3;
            Target.Type = TargetType.Caster; Deliver.Type = DeliveryType.Direct; CastMode = CastMode.Instant;
            int window = (int)b.F("window", 12f);
            DescArgs = new object[] { window };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.GrantInstantCast,
                AffectCaster = true,
                StatusEffectDuration = window,
                StatusEffectId = DruidEffectIds.Quickening
            });
            ConfigureCost(defResource: 0f, defCooldown: b.F("cooldown", 120f));
        }
    }
}
