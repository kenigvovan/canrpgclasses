using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:mystic_barrage")]
    public class MysticBarrageSpell : Spell
    {
        public MysticBarrageSpell()
        {
            var b = Balance;
            DisplayName = "Mystic Barrage";
            IconName = "black-hole-bolas";
            School = SpellSchool.Arcane;
            Tier = 2;
            Range = 24;

            Target.Type = TargetType.None;
            Deliver.Type = DeliveryType.Projectile;
            Deliver.ProjectileVelocity = b.F("projVelocity", 2.8f); // near-instant, to keep it snappy
            Deliver.ProjectileEntity = "spellorb";

            CastMode = CastMode.Channel;
            CastDuration = b.F("channel", 1.5f);
            ChannelTicks = (int)b.F("bolts", 3f);

            // A spender: every bolt rides the charged arcane spell power, snapshotted at launch, and the charges
            // are stripped when the channel ends. The cheap up-front cost is the payoff for ramping.
            ConsumesCasterEffectId = MageEffectIds.MysticCharges;

            float coeff = b.F("coeff", 1.2f); // per bolt
            DescArgs = new object[] { coeff, ChannelTicks };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0f,
                Particles = ParticleSpec.ForSchool(SpellSchool.Arcane)
            });

            ConfigureCost(defResource: 20f, defCooldown: 3f); // mana
        }
    }
}
