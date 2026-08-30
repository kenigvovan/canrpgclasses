using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    [SpellRegistration("canrpgclasses:sacred_hymn")]
    public class SacredHymnSpell : Spell
    {
        public SacredHymnSpell()
        {
            var b = Balance;
            DisplayName = "Sacred Hymn";
            IconName = "tarot-20-judgement";
            School = SpellSchool.Holy;
            Tier = 5;
            Range = b.F("range", 10f);

            Target.Type = TargetType.Area;
            Target.Affinity = TargetAffinity.Ally;
            Target.AreaIncludeCaster = true;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 2f);

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = b.F("healCoeff", 12f),
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 60f, defCooldown: 120f); // mana
        }
    }
}
