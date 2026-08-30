using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    /// <summary>Per-second damage is snapshotted at cast off shadow spell power.</summary>
    [SpellRegistration("canrpgclasses:shadow_brand")]
    public class ShadowBrandSpell : Spell
    {
        public ShadowBrandSpell()
        {
            var b = Balance;
            DisplayName = "Shadow Brand";
            IconName = "voodoo-doll";
            School = SpellSchool.Shadow;
            Tier = 1;
            Range = 24;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            int duration = (int)b.F("duration", 12f);
            DescArgs = new object[] { duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = PriestEffectIds.ShadowBrand,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.ForSchool(SpellSchool.Shadow)
            });

            ConfigureCost(defResource: 25f, defCooldown: 4f); // mana
        }
    }
}
