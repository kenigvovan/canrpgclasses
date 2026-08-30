using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    [SpellRegistration("canrpgclasses:healing_hands")]
    public class HealingHandsSpell : Spell
    {
        public HealingHandsSpell()
        {
            var b = Balance;
            DisplayName = "Healing Hands";
            IconName = "tarot-19-the-sun";
            School = SpellSchool.Holy;
            Tier = 4;
            Range = 16;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 2.5f);

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = b.F("healCoeff", 20f),
                LandFxRing = true, LandFxRadius = 2.2f, // the biggest heal earns the biggest ring
                Particles = new ParticleSpec { ColorA = 230, ColorR = 140, ColorG = 255, ColorB = 150, Glow = true, Gravity = -0.25f, VelocityY = 1.2f, MinQuantity = 24f, AddQuantity = 16f }
            });

            ConfigureCost(defResource: 60f, defCooldown: 60f); // mana
        }
    }
}
