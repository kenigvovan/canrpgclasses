using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    [SpellRegistration("canrpgclasses:wild_rage")]
    public class WildRageSpell : Spell
    {
        public WildRageSpell()
        {
            var b = Balance;
            DisplayName = "Wild Rage";
            IconName = "mouth-watering";
            School = SpellSchool.PhysicalMelee;
            Tier = 3;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact { Action = ImpactAction.Cleanse, CleanseMax = 0 }); // 0 = everything
            Impacts.Add(new SpellImpact { Action = ImpactAction.GainResource, ResourceGainAmount = b.F("rageGain", 25f) });

            ConfigureCost(defResource: 0f, defCooldown: 45f);
        }
    }
}
