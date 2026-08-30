using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    [SpellRegistration("canrpgclasses:elemental_surge")]
    public class ElementalSurgeSpell : Spell
    {
        public ElementalSurgeSpell()
        {
            var b = Balance;
            DisplayName = "Elemental Surge";
            IconName = "heraldic-sun";
            School = SpellSchool.Nature;
            Tier = 5;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;

            int duration = (int)b.F("duration", 15f);
            DescArgs = new object[] { (int)System.Math.Round(b.F("spellPower", 0.30f) * 100f), duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                AffectCaster = true,
                StatusEffectId = ShamanEffectIds.ElementalSurge,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });

            ConfigureCost(defResource: 0f, defCooldown: 120f);
        }
    }
}
