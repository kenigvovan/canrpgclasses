using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    /// <summary>Lands far harder on a target burning under Ember Shock, and unlike the mage's Ice Shard it does not
    /// consume the burn - one Ember Shock feeds every Magma Burst for its whole duration.</summary>
    [SpellRegistration("canrpgclasses:magma_burst")]
    public class MagmaBurstSpell : Spell
    {
        public MagmaBurstSpell()
        {
            var b = Balance;
            DisplayName = "Magma Burst";
            IconName = "lava";
            School = SpellSchool.Fire;
            Tier = 4;
            Range = 24;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 2.0f);

            DamageMultiplierStat = ShamanStatKeys.MagmaBurstDamage; // Magma Flow

            float coeff = b.F("coeff", 2.2f);
            float vsBurning = b.F("burningMultiplier", 1.5f);
            DescArgs = new object[] { coeff, vsBurning };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0f,
                TrailFx = true, // no travelling projectile, so a molten mote is hurled over instead
                DamageVsControlledMultiplier = vsBurning,
                DamageVsControlEffectId = ShamanEffectIds.EmberShock,
                ConsumeControlOnHit = false,
                Particles = ParticleSpec.ForSchool(SpellSchool.Fire)
            });

            ConfigureCost(defResource: 35f, defCooldown: 8f); // mana
        }
    }
}
