using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    [SpellRegistration("canrpgclasses:ember_totem")]
    public class EmberTotemSpell : Spell
    {
        public EmberTotemSpell()
        {
            var b = Balance;
            DisplayName = "Ember Totem";
            IconName = "kindle";
            School = SpellSchool.Fire;
            Tier = 3;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;

            float duration = b.F("duration", 30f);
            float radius = b.F("radius", 8f);
            DescArgs = new object[] { canrpgclasses.Core.Config.BalanceConfig.Global("totemSearingCoeff", 0.4f), radius, (int)duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.PlaceTotem,
                TotemKind = ShamanTotemSystem.KindSearing,
                StatusEffectDuration = duration, // lifetime
                ZoneRadius = radius,             // reach
                Particles = ParticleSpec.ForSchool(SpellSchool.Fire)
            });

            ConfigureCost(defResource: 35f, defCooldown: 20f); // mana
        }
    }
}
