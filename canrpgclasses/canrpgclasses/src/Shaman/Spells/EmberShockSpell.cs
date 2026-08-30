using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    [SpellRegistration("canrpgclasses:ember_shock")]
    public class EmberShockSpell : Spell
    {
        public EmberShockSpell()
        {
            var b = Balance;
            DisplayName = "Ember Shock";
            IconName = "burning-dot";
            School = SpellSchool.Fire;
            Tier = 2;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;

            float coeff = b.F("coeff", 0.8f);
            int duration = (int)b.F("duration", 12f);
            DescArgs = new object[] { coeff, b.F("coeffPerTick", 0.25f), duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0f,
                Particles = ParticleSpec.ForSchool(SpellSchool.Fire)
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = ShamanEffectIds.EmberShock,
                StatusEffectDuration = duration
            });

            ConfigureCost(defResource: 30f, defCooldown: 6f); // mana
        }
    }
}
