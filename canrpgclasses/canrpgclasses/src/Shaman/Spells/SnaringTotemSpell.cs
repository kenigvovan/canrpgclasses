using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    [SpellRegistration("canrpgclasses:snaring_totem")]
    public class SnaringTotemSpell : Spell
    {
        public SnaringTotemSpell()
        {
            var b = Balance;
            DisplayName = "Snaring Totem";
            IconName = "barbed-wire";
            School = SpellSchool.Nature;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;

            float duration = b.F("duration", 20f);
            float radius = b.F("radius", 8f);
            DescArgs = new object[] { (int)radius, (int)duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.PlaceTotem,
                TotemKind = ShamanTotemSystem.KindEarthbind,
                StatusEffectDuration = duration, // lifetime
                ZoneRadius = radius,             // reach
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });

            ConfigureCost(defResource: 15f, defCooldown: 15f); // mana
        }
    }
}
