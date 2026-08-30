using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    [SpellRegistration("canrpgclasses:ground_tremor")]
    public class GroundTremorSpell : Spell
    {
        public GroundTremorSpell()
        {
            var b = Balance;
            DisplayName = "Ground Tremor";
            IconName = "wind-hole";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;
            Range = b.F("range", 5f);

            Target.Type = TargetType.Area;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = b.F("coeff", 1.0f),
                Knockback = 0f,
                Particles = new ParticleSpec { ColorA = 200, ColorR = 200, ColorG = 210, ColorB = 235, Glow = true, MinQuantity = 12f, AddQuantity = 8f }
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "walkslow",
                StatusEffectDuration = b.F("slowDuration", 5f),
                StatusEffectAmplifier = b.I("slowAmp", 2),
                StatusEffectAmplifierCap = b.I("slowCap", 3)
            });

            ConfigureCost(defResource: 20f, defCooldown: 12f); // rage
        }
    }
}
