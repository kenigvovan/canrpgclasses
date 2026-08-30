using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    /// <summary>Scales off the struck enemy's missing health when the swing lands, not at cast. Only one empower
    /// can be armed at a time, so this and Brutal Strike replace each other.</summary>
    [SpellRegistration("canrpgclasses:death_blow")]
    public class DeathBlowSpell : Spell
    {
        public DeathBlowSpell()
        {
            var b = Balance;
            DisplayName = "Death Blow";
            IconName = "chopped-skull";
            School = SpellSchool.PhysicalMelee;
            Tier = 3;

            Target.Type = TargetType.Caster; // self-buff: arms the next melee swing
            Deliver.Type = DeliveryType.Direct;
            DamageMultiplierStat = WarriorStatKeys.DeathBlowDamage;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.EmpowerNextMelee,
                DamageSpellPowerCoefficient = b.F("coeff", 1.0f),
                DamageMissingHealthCoefficient = b.F("missingCoeff", 2.0f),
                EmpowerWindowSeconds = b.F("empowerWindow", 6f),
                Knockback = 0f,
                Particles = ParticleSpec.ForSchool(SpellSchool.PhysicalMelee)
            });

            ConfigureCost(defResource: 25f, defCooldown: 10f); // rage
        }
    }
}
