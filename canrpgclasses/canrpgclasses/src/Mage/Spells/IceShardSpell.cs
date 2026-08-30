using canrpgclasses.Core;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    /// <summary>The shatter keys off the mage's own root/chill, not a generic slow - someone else's snare won't
    /// set it up.</summary>
    [SpellRegistration("canrpgclasses:ice_shard")]
    public class IceShardSpell : Spell
    {
        public IceShardSpell()
        {
            var b = Balance;
            DisplayName = "Ice Shard";
            IconName = "frozen-arrow";
            School = SpellSchool.Frost;
            Tier = 1;
            Range = 24;

            Target.Type = TargetType.None;
            Deliver.Type = DeliveryType.Projectile;
            Deliver.ProjectileVelocity = b.F("projVelocity", 3.0f); // near-instant, to keep it snappy
            Deliver.ProjectileEntity = "spellorb";

            CastMode = CastMode.Instant;
            DamageMultiplierStat = "iceLanceDamage"; // Sharp Ice talent

            float coeff = b.F("coeff", 1.2f);
            float shatter = b.F("frozenMultiplier", 3f);
            DescArgs = new object[] { coeff, shatter };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0f,
                DamageVsControlledMultiplier = shatter,
                DamageVsControlFlag = CombatFlags.Rooted,
                DamageVsControlEffectId = MageEffectIds.Chilled,
                ConsumeControlOnHit = true, // one freeze, one shatter
                Particles = ParticleSpec.ForSchool(SpellSchool.Frost)
            });

            ConfigureCost(defResource: 15f, defCooldown: 0f); // mana
        }
    }
}
