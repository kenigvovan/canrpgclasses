using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    [SpellRegistration("canrpgclasses:static_shield")]
    public class StaticShieldSpell : Spell
    {
        public StaticShieldSpell()
        {
            var b = Balance;
            DisplayName = "Static Shield";
            IconName = "shield-echoes";
            School = SpellSchool.Nature;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;

            int charges = (int)b.F("charges", 3f);
            float coeff = b.F("coeff", 0.3f);
            float duration = b.F("duration", 600f);
            DescArgs = new object[] { charges, coeff };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                AffectCaster = true,
                StatusEffectId = ShamanEffectIds.StaticShield,
                StatusEffectDuration = duration,
                StatusEffectAmplifier = charges, // the tier is the remaining charges
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });

            ConfigureCost(defResource: 20f, defCooldown: 6f); // mana
        }
    }
}
