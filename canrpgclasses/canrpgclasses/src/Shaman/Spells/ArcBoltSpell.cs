using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    /// <summary>Hits harder against a target marked by Thunder Cleave - the link between the melee spec's swing
    /// window and its casts.</summary>
    [SpellRegistration("canrpgclasses:arc_bolt")]
    public class ArcBoltSpell : Spell
    {
        public ArcBoltSpell()
        {
            var b = Balance;
            DisplayName = "Arc Bolt";
            IconName = "hypersonic-bolt";
            School = SpellSchool.Nature;
            Tier = 1;
            Range = 24;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 2.0f);

            // Storm Charge makes this instant at full stacks, and the cast spends them.
            ConsumesCasterEffectId = ShamanEffectIds.Maelstrom;

            float coeff = b.F("coeff", 2.0f);
            float vsMarked = b.F("stormstrikeMultiplier", 1.2f);
            DescArgs = new object[] { coeff, vsMarked };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0f,
                DamageVsControlledMultiplier = vsMarked,
                DamageVsControlEffectId = ShamanEffectIds.ThunderCleave,
                ConsumeControlOnHit = false, // the mark keeps working for its whole duration
                BoltFx = true,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });

            ConfigureCost(defResource: 20f, defCooldown: 0f); // mana
        }
    }
}
