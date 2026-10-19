using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    [SpellRegistration("canrpgclasses:mending_wave")]
    public class MendingWaveSpell : Spell
    {
        public MendingWaveSpell()
        {
            var b = Balance;
            DisplayName = "Mending Wave";
            IconName = "wave-crest";
            School = SpellSchool.Nature;
            Tier = 1;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 2.8f);

            // Instant at full Storm Charge so a melee shaman can weave in an emergency heal.
            ConsumesCasterEffectId = ShamanEffectIds.Maelstrom;

            float coeff = b.F("healCoeff", 9.0f);
            DescArgs = new object[] { coeff };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = coeff,
                LandFxRing = true,
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 40f, defCooldown: 0f); // mana
        }
    }
}
