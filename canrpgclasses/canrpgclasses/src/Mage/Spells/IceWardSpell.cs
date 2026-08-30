using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    /// <summary>Shares the one absorb pool with the priest's Warding Word - the two don't stack.</summary>
    [SpellRegistration("canrpgclasses:ice_ward")]
    public class IceWardSpell : Spell
    {
        public IceWardSpell()
        {
            var b = Balance;
            DisplayName = "Ice Ward";
            IconName = "icicles-aura";
            School = SpellSchool.Frost;
            Tier = 4;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            float shieldDuration = b.F("shieldDuration", 15f);
            DescArgs = new object[] { (int)shieldDuration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Shield,
                AffectCaster = true,
                ShieldSpellPowerCoefficient = b.F("shieldCoeff", 6f),
                ShieldDurationSeconds = shieldDuration,
                Particles = new ParticleSpec { ColorA = 200, ColorR = 150, ColorG = 200, ColorB = 255, Glow = true, VelocityY = 0.8f }
            });

            ConfigureCost(defResource: 35f, defCooldown: 25f); // mana
        }
    }
}
