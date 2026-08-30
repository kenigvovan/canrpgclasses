using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    [SpellRegistration("canrpgclasses:shield_of_faith")]
    public class ShieldOfFaithSpell : Spell
    {
        public ShieldOfFaithSpell()
        {
            var b = Balance;
            DisplayName = "Shield of Faith";
            IconName = "power-ring";
            School = SpellSchool.Holy;
            Tier = 2;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Shield,
                ShieldSpellPowerCoefficient = b.F("shieldCoeff", 8f),
                ShieldDurationSeconds = b.F("shieldDuration", 12f),
                Particles = new ParticleSpec { ColorA = 200, ColorR = 200, ColorG = 230, ColorB = 255, Glow = true, VelocityY = 0.8f }
            });

            ConfigureCost(defResource: 25f, defCooldown: 20f); // mana
        }
    }
}
