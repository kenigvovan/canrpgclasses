using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    [SpellRegistration("canrpgclasses:venom_coat")]
    public class VenomCoatSpell : Spell
    {
        public VenomCoatSpell()
        {
            var b = Balance;
            DisplayName = "Venom Coat";
            IconName = "dripping-blade";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.CoatWeapon,
                StatusEffectId = "poison", // poison | walkslow | weakmelee
                StatusEffectAmplifier = b.I("tier", 2),
                CoatCharges = b.I("charges", 6)
            });

            ConfigureCost(defResource: 25f, defCooldown: 18f); // energy
        }
    }
}
