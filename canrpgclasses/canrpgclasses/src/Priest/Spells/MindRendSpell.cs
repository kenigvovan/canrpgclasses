using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    /// <summary>Moving or being CC'd breaks the channel; taking damage does not.</summary>
    [SpellRegistration("canrpgclasses:mind_rend")]
    public class MindRendSpell : Spell
    {
        public MindRendSpell()
        {
            var b = Balance;
            DisplayName = "Mind Rend";
            IconName = "brain";
            School = SpellSchool.Shadow;
            Tier = 2;
            Range = 24;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            // The aimed target is locked at cast start, and every tick re-lands the impacts on it - damage plus a
            // snare refresh, so the slow holds for the whole channel.
            CastMode = CastMode.Channel;
            CastDuration = b.F("channel", 3.0f);
            ChannelTicks = (int)b.F("ticks", 4f);
            BeamFx = true;

            float coeff = b.F("coeff", 0.8f);
            int slowDuration = (int)b.F("slowDuration", 2f);
            DescArgs = new object[] { coeff, ChannelTicks, CastDuration, slowDuration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0f,
                Particles = ParticleSpec.ForSchool(SpellSchool.Shadow)
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "walkslow",
                StatusEffectDuration = slowDuration,
                StatusEffectAmplifier = (int)b.F("slowAmp", 2f),
                StatusEffectAmplifierCap = (int)b.F("slowCap", 3f)
            });

            ConfigureCost(defResource: 25f, defCooldown: 0f); // mana
        }
    }
}
