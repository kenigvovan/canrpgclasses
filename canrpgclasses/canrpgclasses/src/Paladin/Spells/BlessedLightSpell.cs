using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    [SpellRegistration("canrpgclasses:blessed_light")]
    public class BlessedLightSpell : Spell
    {
        public BlessedLightSpell()
        {
            var b = Balance;
            DisplayName = "Blessed Light";
            IconName = "prayer";
            School = SpellSchool.Holy;
            Tier = 1;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally; // ally under the crosshair, else self
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 2f);

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = b.F("healCoeff", 8f),
                LandFxRing = true, // the slow deliberate heal earns the landing ring
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 30f, defCooldown: 0f); // mana
        }
    }
}
