using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    [SpellRegistration("canrpgclasses:brutal_strike")]
    public class BrutalStrikeSpell : Spell
    {
        public BrutalStrikeSpell()
        {
            var b = Balance;
            DisplayName = "Brutal Strike";
            IconName = "blade-fall";
            School = SpellSchool.PhysicalMelee;
            Tier = 1;

            Target.Type = TargetType.Caster; // self-buff: empowers the next melee hit
            Deliver.Type = DeliveryType.Direct;
            DamageMultiplierStat = WarriorStatKeys.BrutalDamage;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.EmpowerNextMelee,
                DamageSpellPowerCoefficient = b.F("empowerCoeff", 2.5f),
                Knockback = b.F("knockback", 0.2f),
                EmpowerWindowSeconds = b.F("empowerWindow", 6f),
                Particles = ParticleSpec.ForSchool(SpellSchool.PhysicalMelee)
            });

            ConfigureCost(defResource: 10f, defCooldown: 3f); // rage
        }
    }
}
