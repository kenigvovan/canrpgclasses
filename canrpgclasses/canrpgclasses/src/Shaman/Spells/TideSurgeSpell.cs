using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    [SpellRegistration("canrpgclasses:tide_surge")]
    public class TideSurgeSpell : Spell
    {
        public TideSurgeSpell()
        {
            var b = Balance;
            DisplayName = "Tide Surge";
            IconName = "splash";
            School = SpellSchool.Nature;
            Tier = 2;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;

            float coeff = b.F("healCoeff", 4f);
            int duration = (int)b.F("duration", 6f);
            DescArgs = new object[] { coeff, b.F("coeffPerTick", 0.25f), duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = coeff,
                BloomFx = true,
                Particles = ParticleSpec.Heal()
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = ShamanRestoIds.TideSurge,
                StatusEffectDuration = duration
            });

            ConfigureCost(defResource: 30f, defCooldown: 6f); // mana
        }
    }
}
