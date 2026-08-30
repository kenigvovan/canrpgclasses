using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    [SpellRegistration("canrpgclasses:disengage")]
    public class DisengageSpell : Spell
    {
        public DisengageSpell()
        {
            var b = Balance;
            DisplayName = "Disengage";
            IconName = "acrobatic";
            School = SpellSchool.PhysicalMelee;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Dash,
                DashReverse = true
            });
            Impacts.Add(SpellImpact.Invisibility(b.F("invisDuration", 1.5f)));

            ConfigureCost(defResource: 25f, defCooldown: 14f); // energy
        }
    }
}
