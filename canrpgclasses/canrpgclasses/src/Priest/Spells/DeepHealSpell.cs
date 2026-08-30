using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    [SpellRegistration("canrpgclasses:deep_heal")]
    public class DeepHealSpell : Spell
    {
        public DeepHealSpell()
        {
            var b = Balance;
            DisplayName = "Deep Heal";
            IconName = "jeweled-chalice";
            School = SpellSchool.Holy;
            Tier = 2;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 2.5f);

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = b.F("healCoeff", 10f),
                LandFxRing = true, // the slow deliberate heal earns the landing ring
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 45f, defCooldown: 0f); // mana
        }
    }
}
