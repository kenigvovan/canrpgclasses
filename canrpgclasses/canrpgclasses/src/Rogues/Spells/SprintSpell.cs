using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    [SpellRegistration("canrpgclasses:sprint")]
    public class SprintSpell : Spell
    {
        public SprintSpell()
        {
            var b = Balance;
            DisplayName = "Sprint";
            IconName = "sprint";
            School = SpellSchool.PhysicalMelee;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "walkspeed",
                StatusEffectDuration = b.F("speedDuration", 6f),
                StatusEffectAmplifier = b.I("speedAmp", 3),
                StatusEffectAmplifierCap = b.I("speedCap", 3)
            });

            ConfigureCost(defResource: 20f, defCooldown: 20f); // energy
        }
    }
}
