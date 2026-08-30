using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    [SpellRegistration("canrpgclasses:piercing_light")]
    public class PiercingLightSpell : Spell
    {
        public PiercingLightSpell()
        {
            var b = Balance;
            DisplayName = "Piercing Light";
            IconName = "sunbeams";
            School = SpellSchool.Holy;
            Tier = 1;
            Range = 24;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 1.5f);

            float coeff = b.F("coeff", 2.0f);
            DescArgs = new object[] { coeff };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0f,
                TrailFx = true, // no projectile, so a mote streaks caster to target instead
                // With Penitence, part of this hit also heals shielded party allies; inert until that talent sets
                // the fraction, so the base spell is unaffected without it.
                DamageSplashHealStat = Talents.AtonementTalent.AtonementStat,
                Particles = ParticleSpec.ForSchool(SpellSchool.Holy)
            });

            ConfigureCost(defResource: 20f, defCooldown: 0f); // mana
        }
    }
}
