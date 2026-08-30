using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    [SpellRegistration("canrpgclasses:concealment")]
    public class ConcealmentSpell : Spell
    {
        public ConcealmentSpell()
        {
            var b = Balance;
            DisplayName = "Concealment";
            IconName = "hood";
            School = SpellSchool.Nature;
            Tier = 2;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                AffectCaster = true,
                StatusEffectId = ConcealmentEffectId.Id,
                StatusEffectDuration = b.F("duration", 20f),
                StatusEffectAmplifier = 1
            });

            ConfigureCost(defResource: 0f, defCooldown: 25f); // focus
        }
    }
}
