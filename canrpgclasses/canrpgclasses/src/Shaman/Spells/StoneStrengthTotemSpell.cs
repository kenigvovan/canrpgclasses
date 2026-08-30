using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    [SpellRegistration("canrpgclasses:stone_strength_totem")]
    public class StoneStrengthTotemSpell : Spell
    {
        public StoneStrengthTotemSpell()
        {
            var b = Balance;
            DisplayName = "Stone Strength Totem";
            IconName = "mountains";
            School = SpellSchool.Nature;
            Tier = 3;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;

            float duration = b.F("duration", 30f);
            float radius = b.F("radius", 8f);
            DescArgs = new object[]
            {
                (int)System.Math.Round(canrpgclasses.Core.Config.BalanceConfig.Global("totemEarthMeleeBonus", 0.10f) * 100f),
                radius, (int)duration
            };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.PlaceTotem,
                TotemKind = ShamanTotemSystem.KindEarth,
                StatusEffectDuration = duration,
                ZoneRadius = radius,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });

            ConfigureCost(defResource: 35f, defCooldown: 20f); // mana
        }
    }
}
