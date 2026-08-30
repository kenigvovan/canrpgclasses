using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    [SpellRegistration("canrpgclasses:gore")]
    public class GoreSpell : Spell
    {
        public GoreSpell()
        {
            var b = Balance;
            DisplayName = "Gore";
            IconName = "neck-bite";
            School = SpellSchool.PhysicalMelee;
            Tier = 3;
            Range = 4;

            Target.Type = TargetType.Aim;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = b.F("coeff", 2.0f),
                Knockback = 0f,
                Particles = new ParticleSpec { ColorA = 230, ColorR = 190, ColorG = 20, ColorB = 20, MinQuantity = 14f, AddQuantity = 10f, Gravity = -0.2f }
            });
            // AffectCaster sends the heal back to the warrior rather than the struck enemy.
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                AffectCaster = true,
                HealSpellPowerCoefficient = b.F("healCoeff", 1.2f),
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 20f, defCooldown: 6f); // rage
        }
    }
}
