using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    /// <summary>Hurts entities only - it never breaks blocks.</summary>
    [SpellRegistration("canrpgclasses:blast_trap")]
    public class BlastTrapSpell : Spell
    {
        public BlastTrapSpell()
        {
            var b = Balance;
            DisplayName = "Blast Trap";
            IconName = "falling-star";
            School = SpellSchool.Fire;
            Tier = 3;
            Range = b.F("radius", 4f); // blast radius

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            // Embers mark the trap while it's armed.
            Target.AreaParticles = new ParticleSpec
            {
                ColorA = 200, ColorR = 255, ColorG = 150, ColorB = 50, Glow = true,
                MinQuantity = 12f, AddQuantity = 10f,
                MinSize = 0.2f, MaxSize = 0.5f,
                LifeLength = 0.7f, Gravity = -0.05f, VelocityY = 0.6f
            };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.DamageZone,
                ZoneIsTrap = true,
                ZoneTriggerRadius = b.F("triggerRadius", 1.5f),
                ZoneDurationSeconds = b.F("armDuration", 30f),
                DamageSpellPowerCoefficient = b.F("coeff", 3f),
                Knockback = b.F("knockback", 0.4f),
                ZoneTriggerSound = "survival:sounds/effect/mediumexplosion",
                Particles = ParticleSpec.ForSchool(SpellSchool.Fire)
            });

            ConfigureCost(defResource: 30f, defCooldown: 14f); // focus
        }
    }
}
