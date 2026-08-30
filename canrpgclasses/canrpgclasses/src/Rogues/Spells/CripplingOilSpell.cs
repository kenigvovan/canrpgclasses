using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    /// <summary>The slow it applies is simplealchemy's "walkslow", not our own.</summary>
    [SpellRegistration("canrpgclasses:crippling_oil")]
    public class CripplingOilSpell : Spell
    {
        public CripplingOilSpell()
        {
            var b = Balance;
            DisplayName = "Crippling Oil";
            IconName = "drop-weapon";
            School = SpellSchool.PhysicalMelee;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.CoatWeapon,
                StatusEffectId = "walkslow",
                StatusEffectAmplifier = b.I("tier", 2),
                CoatCharges = b.I("charges", 6)
            });

            ConfigureCost(defResource: 25f, defCooldown: 18f); // energy
        }
    }
}
