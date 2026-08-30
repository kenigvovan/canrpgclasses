using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    [SpellRegistration("canrpgclasses:signal_flare")]
    public class SignalFlareSpell : Spell
    {
        public SignalFlareSpell()
        {
            var b = Balance;
            DisplayName = "Signal Flare";
            IconName = "sunbeams";
            School = SpellSchool.Fire;
            Tier = 2;
            Range = b.F("radius", 12f); // reveal radius

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Reveal,
                Particles = new ParticleSpec
                {
                    // Bright, tall and long-lived, so it reads as a launched flare rather than a puff.
                    ColorA = 255, ColorR = 255, ColorG = 200, ColorB = 40, Glow = true,
                    MinQuantity = 45f, AddQuantity = 30f,
                    MinSize = 0.3f, MaxSize = 0.7f,
                    LifeLength = 2.5f, Gravity = -0.1f,
                    VelocityHoriz = 0.35f, VelocityY = 3.2f
                }
            });

            ConfigureCost(defResource: 20f, defCooldown: 20f); // focus
        }
    }
}
