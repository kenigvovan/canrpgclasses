using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    /// <summary>Heals the wolf companion. Does nothing without a pet out.</summary>
    [SpellRegistration("canrpgclasses:tend_beast")]
    public class TendBeastSpell : Spell
    {
        public TendBeastSpell()
        {
            var b = Balance;
            DisplayName = "Tend Beast";
            IconName = "bandage-roll";
            School = SpellSchool.Nature;
            Tier = 2;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.PetTarget,
                HealSpellPowerCoefficient = b.F("healCoeff", 8f),
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 25f, defCooldown: 10f); // focus
        }
    }
}
