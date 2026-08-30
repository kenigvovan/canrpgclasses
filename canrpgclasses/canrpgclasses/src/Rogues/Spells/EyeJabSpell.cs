using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    [SpellRegistration("canrpgclasses:eye_jab")]
    public class EyeJabSpell : Spell
    {
        public EyeJabSpell()
        {
            var b = Balance;
            DisplayName = "Eye Jab";
            IconName = "eyepatch";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;
            Range = 4;

            Target.Type = TargetType.Aim;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact { Action = ImpactAction.Stun, StatusEffectDuration = b.F("stun", 2f) });

            ConfigureCost(defResource: 25f, defCooldown: 12f); // energy
        }
    }
}
