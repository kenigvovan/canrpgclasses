using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    /// <summary>Began as a rogue spell, hence the unchanged id and lang keys.</summary>
    [SpellRegistration("canrpgclasses:fearsome_shout")]
    public class FearsomeShoutSpell : Spell
    {
        public FearsomeShoutSpell()
        {
            var b = Balance;
            DisplayName = "Fearsome Shout";
            IconName = "screaming";
            School = SpellSchool.PhysicalMelee;
            Tier = 3;
            Range = 12;

            Target.Type = TargetType.Area;
            Target.AreaVerticalRangeMultiplier = 0.5f;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "weakmelee",
                StatusEffectDuration = b.F("duration", 8f),
                StatusEffectAmplifier = b.I("amp", 1),
                StatusEffectAmplifierCap = b.I("cap", 5),
                StatusEffectApplyMode = StatusApplyMode.Add
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = b.F("coeff", 0.05f),
                Knockback = 0f
            });

            ConfigureCost(defResource: 20f, defCooldown: 20f); // rage
        }
    }
}
