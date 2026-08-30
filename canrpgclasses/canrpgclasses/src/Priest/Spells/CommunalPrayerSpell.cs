using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    [SpellRegistration("canrpgclasses:communal_prayer")]
    public class CommunalPrayerSpell : Spell
    {
        public CommunalPrayerSpell()
        {
            var b = Balance;
            DisplayName = "Communal Prayer";
            IconName = "prayer";
            School = SpellSchool.Holy;
            Tier = 3;
            Range = b.F("range", 10f);

            Target.Type = TargetType.Area;
            Target.Affinity = TargetAffinity.Ally;
            Target.AreaIncludeCaster = true;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 2.5f);

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = b.F("healCoeff", 6f),
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 50f, defCooldown: 20f); // mana
        }
    }
}
