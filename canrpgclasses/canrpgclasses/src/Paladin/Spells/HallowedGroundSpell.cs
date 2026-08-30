using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    [SpellRegistration("canrpgclasses:hallowed_ground")]
    public class HallowedGroundSpell : Spell
    {
        public HallowedGroundSpell()
        {
            var b = Balance;
            DisplayName = "Hallowed Ground";
            IconName = "sunbeams";
            School = SpellSchool.Holy;
            Tier = 3;
            Range = 5;

            // Caster-targeted: the DamageZone impact runs its own area scan each tick at the cast point.
            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            // A carpet of slow-rising sparks over the whole zone, re-emitted each particle tick and kept light
            // per burst because it refreshes often. Shown even with no enemies present.
            Target.AreaParticles = new ParticleSpec
            {
                ColorA = 200, ColorR = 255, ColorG = 230, ColorB = 130, Glow = true,
                MinQuantity = 28f, AddQuantity = 16f,
                MinSize = 0.25f, MaxSize = 0.5f,
                LifeLength = 1.0f, Gravity = -0.02f,
                VelocityHoriz = 0.15f, VelocityY = 0.7f // mostly straight up, little horizontal drift
            };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.DamageZone,
                DamageSpellPowerCoefficient = b.F("coeff", 0.5f), // per tick
                Knockback = b.F("knockback", 0f),                 // no repeated knockback inside the zone
                ZoneDurationSeconds = b.F("zoneDuration", 6f),
                ZoneTickSeconds = b.F("zoneTick", 1f),
                ZoneParticleSeconds = b.F("zoneParticleTick", 0.4f),
                ZoneLandRune = true, // a sigil is branded on the ground when the zone is laid down
                Particles = ParticleSpec.ForSchool(SpellSchool.Holy) // per-hit sear on each enemy struck
            });

            ConfigureCost(defResource: 25f, defCooldown: 12f); // mana
        }
    }
}
