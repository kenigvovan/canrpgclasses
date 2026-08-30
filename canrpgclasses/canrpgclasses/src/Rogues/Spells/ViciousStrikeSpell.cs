using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    /// <summary>Builder: arms the next melee swing with bonus damage. The combo point lands only when that
    /// empowered hit actually connects inside the window.</summary>
    [SpellRegistration("canrpgclasses:vicious_strike")]
    public class ViciousStrikeSpell : Spell
    {
        public ViciousStrikeSpell()
        {
            var b = Balance;
            DisplayName = "Vicious Strike";
            IconName = "scalpel-strike";
            School = SpellSchool.PhysicalMelee;
            Tier = 1;

            Target.Type = TargetType.Caster; // self-buff: empowers the next melee hit
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.EmpowerNextMelee,
                DamageSpellPowerCoefficient = b.F("empowerCoeff", 3f),
                Knockback = b.F("knockback", 0.2f),
                EmpowerWindowSeconds = b.F("empowerWindow", 6f), // miss the window and the buff, with its combo, is gone
                Particles = ParticleSpec.ForSchool(SpellSchool.PhysicalMelee)
            });

            ComboBuilder = true;
            ConfigureCost(defResource: 30f, defCooldown: 2f); // a cheap builder, it feeds the finishers
        }
    }
}
