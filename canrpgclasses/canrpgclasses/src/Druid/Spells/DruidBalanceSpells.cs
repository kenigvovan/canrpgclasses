using canrpgclasses.Core.Spells;

namespace canrpgclasses.Druid.Spells
{
    [SpellRegistration("canrpgclasses:lunar_flare")]
    public class LunarFlareSpell : Spell
    {
        public LunarFlareSpell()
        {
            var b = Balance;
            DisplayName = "Lunar Flare"; IconName = "falling-star";
            School = SpellSchool.Nature; Tier = 1; Range = 24;
            Target.Type = TargetType.Aim; Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct; CastMode = CastMode.Instant;

            float coeff = b.F("coeff", 1.0f);
            int duration = (int)b.F("duration", 10f);
            DescArgs = new object[] { coeff, b.F("coeffPerTick", 0.25f), duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                PillarFx = true,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = DruidEffectIds.LunarFlare,
                StatusEffectDuration = duration
            });
            ConfigureCost(defResource: 20f, defCooldown: 0f);
        }
    }

    [SpellRegistration("canrpgclasses:stinging_swarm")]
    public class StingingSwarmSpell : Spell
    {
        public StingingSwarmSpell()
        {
            var b = Balance;
            DisplayName = "Stinging Swarm"; IconName = "tree-beehive";
            School = SpellSchool.Nature; Tier = 2; Range = 24;
            Target.Type = TargetType.Aim; Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct; CastMode = CastMode.Instant;

            int duration = (int)b.F("duration", 12f);
            DescArgs = new object[] { b.F("coeffPerTick", 0.3f), duration };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = DruidEffectIds.StingingSwarm,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });
            ConfigureCost(defResource: 18f, defCooldown: 0f);
        }
    }

    [SpellRegistration("canrpgclasses:star_lance")]
    public class StarLanceSpell : Spell
    {
        public StarLanceSpell()
        {
            var b = Balance;
            DisplayName = "Star Lance"; IconName = "star-satellites";
            School = SpellSchool.Arcane; Tier = 2; Range = 26;
            Target.Type = TargetType.Aim; Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Charge; CastDuration = b.F("cast", 2.6f);

            float coeff = b.F("coeff", 3.0f);
            DescArgs = new object[] { coeff };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                TrailFx = true,
                Particles = ParticleSpec.ForSchool(SpellSchool.Arcane)
            });
            ConfigureCost(defResource: 35f, defCooldown: 0f);
        }
    }

    [SpellRegistration("canrpgclasses:astral_burst")]
    public class AstralBurstSpell : Spell
    {
        public AstralBurstSpell()
        {
            var b = Balance;
            DisplayName = "Astral Burst"; IconName = "falling-star";
            School = SpellSchool.Arcane; Tier = 3; Range = 26;
            Target.Type = TargetType.Aim; Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct; CastMode = CastMode.Instant;

            float coeff = b.F("coeff", 2.2f);
            DescArgs = new object[] { coeff };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                BoltFx = true,
                Particles = ParticleSpec.ForSchool(SpellSchool.Arcane)
            });
            ConfigureCost(defResource: 30f, defCooldown: 8f);
        }
    }

    [SpellRegistration("canrpgclasses:raging_winds")]
    public class RagingWindsSpell : Spell
    {
        public RagingWindsSpell()
        {
            var b = Balance;
            DisplayName = "Raging Winds"; IconName = "flower-twirl";
            School = SpellSchool.Nature; Tier = 3; Range = b.F("range", 18f);
            Target.Type = TargetType.Caster; Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Channel; CastDuration = b.F("zoneDuration", 6f);
            Target.AreaParticles = ParticleSpec.ForSchool(SpellSchool.Nature);

            float coeff = b.F("coeff", 0.5f);
            DescArgs = new object[] { coeff };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.DamageZone,
                DamageSpellPowerCoefficient = coeff, // per tick
                ZoneAtAimPoint = true,
                ZoneRadius = b.F("zoneRadius", 5f),
                ZoneDurationSeconds = b.F("zoneDuration", 6f),
                ZoneTickSeconds = b.F("zoneTick", 1f),
                ZoneLandRing = true,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });
            ConfigureCost(defResource: 40f, defCooldown: 10f);
        }
    }

    [SpellRegistration("canrpgclasses:wind_blast")]
    public class WindBlastSpell : Spell
    {
        public WindBlastSpell()
        {
            var b = Balance;
            DisplayName = "Wind Blast"; IconName = "fluffy-cloud";
            School = SpellSchool.Nature; Tier = 3; Range = b.F("radius", 7f);
            Target.Type = TargetType.Area; Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct; CastMode = CastMode.Instant;

            float coeff = b.F("coeff", 1.2f);
            int slowDur = (int)b.F("slowDuration", 4f);
            DescArgs = new object[] { coeff, (int)b.F("radius", 7f), slowDur };
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = b.F("knockback", 3.5f),
                NovaFx = true,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "walkslow",
                StatusEffectDuration = slowDur,
                StatusEffectAmplifier = b.I("slowAmp", 2),
                StatusEffectAmplifierCap = b.I("slowCap", 3)
            });
            ConfigureCost(defResource: 25f, defCooldown: 25f);
        }
    }
}
