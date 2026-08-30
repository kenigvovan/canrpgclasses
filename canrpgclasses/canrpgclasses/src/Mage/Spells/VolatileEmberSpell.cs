using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:volatile_ember")]
    public class VolatileEmberSpell : Spell
    {
        public VolatileEmberSpell()
        {
            var b = Balance;
            DisplayName = "Volatile Ember";
            IconName = "rolling-bomb";
            School = SpellSchool.Fire;
            Tier = 3;
            Range = 24;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 1f);

            int duration = (int)b.F("duration", 6f);
            DescArgs = new object[] { duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = MageEffectIds.VolatileEmber,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.ForSchool(SpellSchool.Fire)
            });

            ConfigureCost(defResource: 35f, defCooldown: 12f); // mana
        }
    }
}
