using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    [SpellRegistration("canrpgclasses:leaping_charge")]
    public class LeapingChargeSpell : Spell
    {
        public LeapingChargeSpell()
        {
            var b = Balance;
            DisplayName = "Leaping Charge";
            IconName = "jump-across";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            // With no target, ApplyDash reads these two instead of aiming at anyone.
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Dash,
                DashForwardStrength = b.F("forward", 0.8f),
                DashUpward = b.F("upward", 0.35f)
            });

            ConfigureCost(defResource: 10f, defCooldown: 20f); // rage
        }
    }
}
