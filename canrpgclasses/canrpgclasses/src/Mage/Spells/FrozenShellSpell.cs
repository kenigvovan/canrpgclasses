using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:frozen_shell")]
    public class FrozenShellSpell : Spell
    {
        public FrozenShellSpell()
        {
            var b = Balance;
            DisplayName = "Frozen Shell";
            IconName = "peaks";
            School = SpellSchool.Frost;
            Tier = 5;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            float duration = b.F("duration", 3f);
            DescArgs = new object[] { (int)duration };

            // Invulnerability in practice: an absorb pool big enough to soak any realistic burst in a short window.
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Shield,
                AffectCaster = true,
                ShieldSpellPowerCoefficient = b.F("shieldCoeff", 1000f),
                ShieldDurationSeconds = duration,
                ShieldDomeStyle = 2, // solid ice, distinct from Ice Ward's thin crystal
                Particles = new ParticleSpec { ColorA = 210, ColorR = 150, ColorG = 200, ColorB = 255, Glow = true, VelocityY = 0.9f, MinQuantity = 20f, AddQuantity = 12f }
            });
            // Frozen solid. AffectCaster is what lets a Root land on the caster.
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Root,
                AffectCaster = true,
                StatusEffectDuration = duration
            });

            ConfigureCost(defResource: 0f, defCooldown: 150f);
        }
    }
}
