using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    /// <summary>Only enemies trip it, and it expires if nobody does.</summary>
    [SpellRegistration("canrpgclasses:chill_trap")]
    public class ChillTrapSpell : Spell
    {
        public ChillTrapSpell()
        {
            var b = Balance;
            DisplayName = "Chill Trap";
            IconName = "fireflake";
            School = SpellSchool.Frost;
            Tier = 2;
            Range = b.F("radius", 4f); // blast radius

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            // Marks the trigger footprint while the trap is armed.
            Target.AreaParticles = new ParticleSpec
            {
                ColorA = 190, ColorR = 150, ColorG = 220, ColorB = 255, Glow = true,
                MinQuantity = 10f, AddQuantity = 8f,
                MinSize = 0.2f, MaxSize = 0.45f,
                LifeLength = 1.0f, Gravity = -0.02f, VelocityY = 0.25f
            };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.DamageZone,
                ZoneIsTrap = true,
                ZoneTriggerRadius = b.F("triggerRadius", 1.5f),
                ZoneDurationSeconds = b.F("armDuration", 30f),
                DamageSpellPowerCoefficient = 0f, // control only
                ZoneStatusEffectId = "walkslow",
                ZoneStatusTier = b.I("slowAmp", 2),
                ZoneStatusSeconds = b.F("slowDuration", 4f)
            });

            ConfigureCost(defResource: 25f, defCooldown: 15f); // focus
        }
    }
}
