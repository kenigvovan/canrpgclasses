using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    /// <summary>Hops to the allies who need it most, not the closest ones, weakening with each hop.</summary>
    [SpellRegistration("canrpgclasses:cascading_heal")]
    public class CascadingHealSpell : Spell
    {
        public CascadingHealSpell()
        {
            var b = Balance;
            DisplayName = "Cascading Heal";
            IconName = "wave-crest";
            School = SpellSchool.Nature;
            Tier = 3;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 2.5f);

            float coeff = b.F("healCoeff", 7f);
            int jumps = (int)b.F("chainJumps", 3f);
            float falloff = b.F("chainFalloff", 0.7f);
            DescArgs = new object[] { coeff, jumps, (int)System.Math.Round((1f - falloff) * 100f) };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = coeff,
                LandFxRing = true,
                ChainJumps = jumps,
                ChainRadius = b.F("chainRadius", 8f),
                ChainFalloff = falloff,
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 45f, defCooldown: 8f); // mana
        }
    }
}
