using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    [SpellRegistration("canrpgclasses:regroup")]
    public class RegroupSpell : Spell
    {
        public RegroupSpell()
        {
            DisplayName = "Regroup";
            IconName = "sundial";
            School = SpellSchool.PhysicalMelee;
            Tier = 4;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.ResetCooldowns,
                ResetCooldownKeys = new[]
                {
                    "canrpgclasses:slip_away",
                    "canrpgclasses:sprint",
                    "canrpgclasses:disengage",
                    "canrpgclasses:second_wind",
                    "canrpgclasses:shadow_stride"
                },
                Particles = ParticleSpec.ForSchool(SpellSchool.Arcane) // a brief temporal shimmer
            });

            ConfigureCost(defResource: 0f, defCooldown: 180f);
        }
    }
}
