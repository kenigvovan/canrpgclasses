using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    [SpellRegistration("canrpgclasses:fresh_quiver")]
    public class FreshQuiverSpell : Spell
    {
        public FreshQuiverSpell()
        {
            DisplayName = "Fresh Quiver";
            IconName = "sundial";
            School = SpellSchool.Nature;
            Tier = 4;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.ResetCooldowns,
                ResetCooldownKeys = new[]
                {
                    "canrpgclasses:chill_trap",
                    "canrpgclasses:blast_trap",
                    "canrpgclasses:jarring_shot",
                    "canrpgclasses:second_breath",
                    "canrpgclasses:hunter_disengage"
                },
                Particles = ParticleSpec.ForSchool(SpellSchool.Arcane)
            });

            ConfigureCost(defResource: 0f, defCooldown: 180f); // focus
        }
    }
}
