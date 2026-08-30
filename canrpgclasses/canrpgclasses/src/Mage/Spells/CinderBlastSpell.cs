using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:cinder_blast")]
    public class CinderBlastSpell : Spell
    {
        public const string CinderBlastDamageStat = "pyroblastDamage";

        public CinderBlastSpell()
        {
            var b = Balance;
            DisplayName = "Cinder Blast";
            IconName = "explosion-rays";
            School = SpellSchool.Fire;
            Tier = 3;
            Range = 24;

            Target.Type = TargetType.None; // a real bolt fired along the aim
            Deliver.Type = DeliveryType.Projectile;
            Deliver.ProjectileVelocity = b.F("projVelocity", 1.1f);
            Deliver.ProjectileEntity = "spellorb";

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 3.0f);
            DamageMultiplierStat = CinderBlastDamageStat;

            float coeff = b.F("coeff", 3.5f);
            int duration = (int)b.F("duration", 8f);
            float stunChance = b.F("stunChance", 0.15f);
            int stunDuration = (int)b.F("stunDuration", 2f);
            DescArgs = new object[] { coeff, duration, (int)System.Math.Round(stunChance * 100f), stunDuration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0f,
                Particles = ParticleSpec.ForSchool(SpellSchool.Fire)
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = MageEffectIds.Ignite,
                StatusEffectDuration = duration
            });
            // The stun doesn't break on damage, or the Ignite ticking on the same target would cut it short.
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Stun,
                Chance = stunChance,
                StatusEffectDuration = stunDuration,
                StunBreaksOnDamage = false
            });

            ConfigureCost(defResource: 45f, defCooldown: 12f); // mana
        }
    }
}
