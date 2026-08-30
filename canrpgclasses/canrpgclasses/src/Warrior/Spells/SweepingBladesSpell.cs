using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    [SpellRegistration("canrpgclasses:sweeping_blades")]
    public class SweepingBladesSpell : Spell
    {
        public SweepingBladesSpell()
        {
            var b = Balance;
            DisplayName = "Sweeping Blades";
            IconName = "spinning-blades";
            School = SpellSchool.PhysicalMelee;
            Tier = 3;
            Range = b.F("range", 4.5f);
            AnimationCode = "falx"; // the vanilla sweep, defined by all three player models

            Target.Type = TargetType.Area;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = b.F("coeff", 1.8f),
                Knockback = b.F("knockback", 0.4f),
                Particles = ParticleSpec.ForSchool(SpellSchool.PhysicalMelee)
            });

            ConfigureCost(defResource: 25f, defCooldown: 10f); // rage
        }
    }
}
