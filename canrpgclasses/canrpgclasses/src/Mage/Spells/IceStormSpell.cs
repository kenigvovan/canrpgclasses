using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:ice_storm")]
    public class IceStormSpell : Spell
    {
        public IceStormSpell()
        {
            var b = Balance;
            DisplayName = "Ice Storm";
            IconName = "splash";
            School = SpellSchool.Frost;
            Tier = 3;
            Range = b.F("range", 18f); // max cast distance, not the storm's radius

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            // Channeled: moving or being CC'd ends the storm early. The channel matches the zone duration, so the
            // two stay in lockstep.
            CastMode = CastMode.Channel;
            CastDuration = b.F("zoneDuration", 6f);

            Target.AreaParticles = new ParticleSpec
            {
                ColorA = 200, ColorR = 150, ColorG = 200, ColorB = 255, Glow = true,
                MinQuantity = 28f, AddQuantity = 16f,
                MinSize = 0.2f, MaxSize = 0.45f,
                LifeLength = 1.0f, Gravity = -0.05f,
                VelocityHoriz = 0.1f, VelocityY = -0.3f
            };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.DamageZone,
                DamageSpellPowerCoefficient = b.F("coeff", 0.4f),
                Knockback = 0f,
                ZoneAtAimPoint = true,
                ZoneRadius = b.F("zoneRadius", 4.5f),
                ZoneDurationSeconds = b.F("zoneDuration", 6f),
                ZoneTickSeconds = b.F("zoneTick", 1f),
                ZoneParticleSeconds = b.F("zoneParticleTick", 0.4f),
                ZoneStatusEffectId = MageEffectIds.Chilled,
                ZoneStatusTier = (int)b.F("slowAmp", 2f),
                ZoneStatusSeconds = b.F("slowDuration", 2f),
                ZoneLandRing = true, // an icy shockwave marks where the storm lands
                Particles = ParticleSpec.ForSchool(SpellSchool.Frost)
            });

            int slowPct = (int)System.Math.Round(b.F("slowAmp", 2f) * 15f); // the chill is 15% walkspeed per tier
            int slowDuration = (int)b.F("slowDuration", 2f);
            DescArgs = new object[] { slowPct, slowDuration };

            ConfigureCost(defResource: 50f, defCooldown: 15f); // mana
        }
    }
}
