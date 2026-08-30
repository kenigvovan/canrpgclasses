using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:fire_bolt")]
    public class FireBoltSpell : Spell
    {
        public FireBoltSpell()
        {
            var b = Balance;
            DisplayName = "Fire Bolt";
            IconName = "fission";
            School = SpellSchool.Fire;
            Tier = 2;
            Range = 24;

            Target.Type = TargetType.None; // a real bolt fired along the aim
            Deliver.Type = DeliveryType.Projectile;
            Deliver.ProjectileVelocity = b.F("projVelocity", 1.2f);
            Deliver.ProjectileEntity = "spellorb";

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 2.2f);

            float coeff = b.F("coeff", 2.5f);
            DescArgs = new object[] { coeff };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0f,
                Particles = ParticleSpec.ForSchool(SpellSchool.Fire)
            });

            ConfigureCost(defResource: 30f, defCooldown: 0f); // mana
        }
    }
}
