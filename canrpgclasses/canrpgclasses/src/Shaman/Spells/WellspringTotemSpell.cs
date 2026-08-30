using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    /// <summary>Only mana users benefit - allies on rage, energy or focus get nothing.</summary>
    [SpellRegistration("canrpgclasses:wellspring_totem")]
    public class WellspringTotemSpell : Spell
    {
        public WellspringTotemSpell()
        {
            var b = Balance;
            DisplayName = "Wellspring Totem";
            IconName = "waterfall";
            School = SpellSchool.Nature;
            Tier = 2;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;

            float duration = b.F("duration", 30f);
            float radius = b.F("radius", 8f);
            DescArgs = new object[]
            {
                canrpgclasses.Core.Config.BalanceConfig.Global("totemManaSpringPerTick", 4f), radius, (int)duration
            };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.PlaceTotem,
                TotemKind = ShamanTotemSystem.KindManaSpring,
                StatusEffectDuration = duration, // lifetime
                ZoneRadius = radius,             // reach
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });

            ConfigureCost(defResource: 30f, defCooldown: 20f); // mana
        }
    }
}
