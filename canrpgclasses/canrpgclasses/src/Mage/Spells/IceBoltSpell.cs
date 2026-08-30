using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:ice_bolt")]
    public class IceBoltSpell : Spell
    {
        public IceBoltSpell()
        {
            var b = Balance;
            DisplayName = "Ice Bolt";
            IconName = "bolt-spell-cast";
            School = SpellSchool.Frost;
            Tier = 1;
            Range = 24;

            Target.Type = TargetType.None; // a real bolt fired along the aim
            Deliver.Type = DeliveryType.Projectile;
            Deliver.ProjectileVelocity = b.F("projVelocity", 1.2f);
            Deliver.ProjectileEntity = "spellorb";

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 1.8f);

            float coeff = b.F("coeff", 1.8f);
            int slowDuration = (int)b.F("slowDuration", 2f);
            int slowPct = (int)System.Math.Round(b.F("slowAmp", 2f) * 15f); // the chill is 15% walkspeed per tier
            DescArgs = new object[] { coeff, slowPct, slowDuration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0f,
                Particles = ParticleSpec.ForSchool(SpellSchool.Frost)
            });
            // The mage's own chill marker rather than a generic walkslow, because it also arms Ice Shard's shatter.
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = Mage.MageEffectIds.Chilled,
                StatusEffectDuration = slowDuration,
                StatusEffectAmplifier = (int)b.F("slowAmp", 2f),
                StatusEffectAmplifierCap = (int)b.F("slowCap", 3f)
            });

            ConfigureCost(defResource: 20f, defCooldown: 0f); // mana
        }
    }
}
