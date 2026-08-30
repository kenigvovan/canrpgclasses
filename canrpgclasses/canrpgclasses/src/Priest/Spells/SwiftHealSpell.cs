using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    [SpellRegistration("canrpgclasses:swift_heal")]
    public class SwiftHealSpell : Spell
    {
        public SwiftHealSpell()
        {
            var b = Balance;
            DisplayName = "Swift Heal";
            IconName = "kneeling";
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
                BloomFx = true,
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 40f, defCooldown: 4f); // the price of speed
        }
    }
}
