using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    [SpellRegistration("canrpgclasses:sacred_barrier")]
    public class SacredBarrierSpell : Spell
    {
        public SacredBarrierSpell()
        {
            var b = Balance;
            DisplayName = "Sacred Barrier";
            IconName = "aura";
            School = SpellSchool.Holy;
            Tier = 4;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Shield,
                ShieldSpellPowerCoefficient = b.F("shieldCoeff", 15f),
                ShieldDurationSeconds = b.F("shieldDuration", 10f),
                Particles = new ParticleSpec { ColorA = 200, ColorR = 255, ColorG = 235, ColorB = 130, Glow = true, VelocityY = 0.9f, MinQuantity = 18f, AddQuantity = 12f }
            });

            ConfigureCost(defResource: 30f, defCooldown: 120f); // mana
        }
    }
}
