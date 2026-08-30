using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    [SpellRegistration("canrpgclasses:shock_powder")]
    public class ShockPowderSpell : Spell
    {
        public ShockPowderSpell()
        {
            var b = Balance;
            DisplayName = "Shock Powder";
            IconName = "powder";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;
            Range = 5;

            Target.Type = TargetType.Area;
            Target.AreaVerticalRangeMultiplier = 0.5f;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Stun,
                StatusEffectDuration = b.F("stun", 2f) // AoE stun kept short
            });

            ConfigureCost(defResource: 35f, defCooldown: 18f); // energy
        }
    }
}
