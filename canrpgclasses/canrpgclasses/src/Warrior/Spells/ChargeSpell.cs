using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    /// <summary>Closes the gap with a knockback-style impulse - a fast run-up, not a teleport.</summary>
    [SpellRegistration("canrpgclasses:charge")]
    public class ChargeSpell : Spell
    {
        public ChargeSpell()
        {
            var b = Balance;
            DisplayName = "Charge";
            IconName = "sprint";
            School = SpellSchool.PhysicalMelee;
            Tier = 1;
            Range = b.F("range", 20f); // engage distance; the dash impulse scales with the gap (clamped ~24 blocks)

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact { Action = ImpactAction.Dash });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "walkspeed",
                StatusEffectDuration = b.F("speedDuration", 3f),
                StatusEffectAmplifier = 1,
                StatusEffectAmplifierCap = b.I("speedCap", 3)
            });
            Impacts.Add(new SpellImpact { Action = ImpactAction.GainResource, ResourceGainAmount = b.F("rageGain", 15f) });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Stun,
                AffectAimedEnemy = true,
                RequiresTalentId = "warrior:bull_rush",
                StatusEffectDuration = b.F("stunSeconds", 2f)
            });

            ConfigureCost(defResource: 0f, defCooldown: 12f);
        }
    }
}
