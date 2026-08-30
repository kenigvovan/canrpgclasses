using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:flame_field")]
    public class FlameFieldSpell : Spell
    {
        public FlameFieldSpell()
        {
            var b = Balance;
            DisplayName = "Flame Field";
            IconName = "interstellar-path";
            School = SpellSchool.Fire;
            Tier = 4;
            Range = b.F("range", 18f); // max cast distance, not the fire's radius

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Target.AreaParticles = new ParticleSpec
            {
                ColorA = 200, ColorR = 255, ColorG = 140, ColorB = 40, Glow = true,
                MinQuantity = 28f, AddQuantity = 16f,
                MinSize = 0.25f, MaxSize = 0.5f,
                LifeLength = 1.0f, Gravity = -0.02f,
                VelocityHoriz = 0.15f, VelocityY = 0.7f
            };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.DamageZone,
                DamageSpellPowerCoefficient = b.F("coeff", 0.6f),
                Knockback = 0f,
                ZoneAtAimPoint = true,
                ZoneRadius = b.F("zoneRadius", 4.5f), // the fire's own radius, decoupled from cast distance
                ZoneDurationSeconds = b.F("zoneDuration", 6f),
                ZoneTickSeconds = b.F("zoneTick", 1f),
                ZoneParticleSeconds = b.F("zoneParticleTick", 0.4f),
                Particles = ParticleSpec.ForSchool(SpellSchool.Fire)
            });

            ConfigureCost(defResource: 50f, defCooldown: 15f); // mana
        }
    }
}
