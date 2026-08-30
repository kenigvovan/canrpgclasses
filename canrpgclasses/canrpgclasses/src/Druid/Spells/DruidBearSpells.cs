using canrpgclasses.Core.Spells;

namespace canrpgclasses.Druid.Spells
{
    /// <summary>Base for a Bear-form ability. Bypasses the form-lock so a shifted druid can actually cast it; spenders
    /// declare their Rage cost via ConfigureSecondaryCost.</summary>
    public abstract class BearAbilitySpell : Spell
    {
        protected BearAbilitySpell()
        {
            School = SpellSchool.Nature;
            Range = 4;
            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;
            BypassesFormLock = true;
            RequiresForm = DruidEffectIds.UrsineFormSpellId;
            // Shared cooldown group acting as a GCD for the bear kit: Bone Crunch is free and the rage spenders would
            // otherwise dump frame-by-frame. Roar and Feral Recovery opt out by resetting the group to their own key.
            Cost.Cooldown.Group = "canrpgclasses:druid_bear_gcd";
        }
    }

    [SpellRegistration("canrpgclasses:bone_crunch")]
    public class BoneCrunchSpell : BearAbilitySpell
    {
        public BoneCrunchSpell()
        {
            var b = Balance;
            DisplayName = "Bone Crunch"; IconName = "wolverine-claws"; Tier = 1;
            float coeff = b.F("coeff", 1.1f);
            DescArgs = new object[] { coeff };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                SparkFx = true,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });
            ConfigureCost(defResource: 0f, defCooldown: 1.0f); // no mana; rage is generated on the hit
        }
    }

    [SpellRegistration("canrpgclasses:heavy_paw")]
    public class HeavyPawSpell : BearAbilitySpell
    {
        public HeavyPawSpell()
        {
            var b = Balance;
            DisplayName = "Heavy Paw"; IconName = "knockout"; Tier = 1;
            float coeff = b.F("coeff", 2.2f);
            float rage = b.F("rage", 30f);
            DescArgs = new object[] { coeff, (int)rage };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = b.F("knockback", 0.3f),
                SparkFx = true,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });
            ConfigureSecondaryCost(DruidRage.PoolId, rage);
            ConfigureCost(defResource: 0f, defCooldown: 1.0f);
        }
    }

    [SpellRegistration("canrpgclasses:claw_sweep")]
    public class ClawSweepSpell : BearAbilitySpell
    {
        public ClawSweepSpell()
        {
            var b = Balance;
            DisplayName = "Claw Sweep"; IconName = "triple-scratches"; Tier = 1;
            Range = b.F("radius", 5f);
            Target.Type = TargetType.Area;
            float coeff = b.F("coeff", 1.0f);
            float rage = b.F("rage", 25f);
            DescArgs = new object[] { coeff, (int)rage };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });
            ConfigureSecondaryCost(DruidRage.PoolId, rage);
            ConfigureCost(defResource: 0f, defCooldown: 1.0f);
        }
    }

    [SpellRegistration("canrpgclasses:gash")]
    public class GashSpell : BearAbilitySpell
    {
        public GashSpell()
        {
            var b = Balance;
            DisplayName = "Gash"; IconName = "ragged-wound"; Tier = 2;
            float coeff = b.F("coeff", 0.8f);
            int duration = (int)b.F("duration", 10f);
            float rage = b.F("rage", 15f);
            DescArgs = new object[] { coeff, b.F("coeffPerTick", 0.2f), duration, (int)rage };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = DruidEffectIds.Gash,
                StatusEffectDuration = duration
            });
            ConfigureSecondaryCost(DruidRage.PoolId, rage);
            ConfigureCost(defResource: 0f, defCooldown: 1.0f);
        }
    }

    [SpellRegistration("canrpgclasses:roar")]
    public class RoarSpell : BearAbilitySpell
    {
        public RoarSpell()
        {
            var b = Balance;
            DisplayName = "Roar"; IconName = "shouting"; Tier = 1;
            Impacts.Add(new SpellImpact { Action = ImpactAction.Aggro });
            Cost.Cooldown.Group = ""; // own 8s cooldown, not the shared bear GCD
            ConfigureCost(defResource: 0f, defCooldown: b.F("cooldown", 8f));
        }
    }

    [SpellRegistration("canrpgclasses:feral_recovery")]
    public class FeralRecoverySpell : BearAbilitySpell
    {
        public FeralRecoverySpell()
        {
            var b = Balance;
            DisplayName = "Feral Recovery"; IconName = "bull"; Tier = 2;
            Target.Type = TargetType.Caster;
            Target.Affinity = TargetAffinity.Ally;
            float rage = b.F("rage", 30f);
            float coeff = b.F("healCoeff", 6f);
            DescArgs = new object[] { coeff, (int)rage };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = coeff,
                BloomFx = true,
                Particles = ParticleSpec.Heal()
            });
            ConfigureSecondaryCost(DruidRage.PoolId, rage);
            Cost.Cooldown.Group = ""; // own 6s cooldown, not the shared bear GCD
            ConfigureCost(defResource: 0f, defCooldown: b.F("cooldown", 6f));
        }
    }
}
