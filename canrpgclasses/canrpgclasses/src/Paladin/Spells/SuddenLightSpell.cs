using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    [SpellRegistration("canrpgclasses:sudden_light")]
    public class SuddenLightSpell : Spell
    {
        public SuddenLightSpell()
        {
            var b = Balance;
            DisplayName = "Sudden Light";
            IconName = "beams-aura";
            School = SpellSchool.Holy;
            Tier = 2;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 1f);

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = b.F("healCoeff", 5f),
                BloomFx = true, // a quick fountain of petals rises at the healed target
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 45f, defCooldown: 6f); // the price of speed
        }
    }
}
