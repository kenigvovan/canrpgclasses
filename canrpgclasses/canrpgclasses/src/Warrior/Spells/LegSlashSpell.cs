using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    [SpellRegistration("canrpgclasses:leg_slash")]
    public class LegSlashSpell : Spell
    {
        public LegSlashSpell()
        {
            var b = Balance;
            DisplayName = "Leg Slash";
            IconName = "achilles-heel";
            School = SpellSchool.PhysicalMelee;
            Tier = 1;
            Range = 4;

            Target.Type = TargetType.Aim;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = b.F("coeff", 0.8f),
                Knockback = 0f
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "walkslow",
                StatusEffectDuration = b.F("slowDuration", 4f),
                StatusEffectAmplifier = b.I("slowAmp", 2),
                StatusEffectAmplifierCap = b.I("slowCap", 3)
            });

            ConfigureCost(defResource: 10f, defCooldown: 6f); // rage
        }
    }
}
