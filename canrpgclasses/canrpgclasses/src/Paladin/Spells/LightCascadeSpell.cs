using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    [SpellRegistration("canrpgclasses:light_cascade")]
    public class LightCascadeSpell : Spell
    {
        public LightCascadeSpell()
        {
            var b = Balance;
            DisplayName = "Light Cascade";
            IconName = "sunbeams";
            School = SpellSchool.Holy;
            Tier = 4;
            Range = b.F("range", 6f);

            Target.Type = TargetType.Area;
            Target.Affinity = TargetAffinity.Ally;
            Target.AreaIncludeCaster = true;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = b.F("healCoeff", 6f), // lower than single-target Blessed Light
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 40f, defCooldown: 30f); // mana
        }
    }
}
