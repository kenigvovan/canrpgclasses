using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    /// <summary>Arcs from the struck enemy to the nearest others, weakening with every hop and never jumping into
    /// the caster's own party.</summary>
    [SpellRegistration("canrpgclasses:forked_lightning")]
    public class ForkedLightningSpell : Spell
    {
        public ForkedLightningSpell()
        {
            var b = Balance;
            DisplayName = "Forked Lightning";
            IconName = "air-zigzag";
            School = SpellSchool.Nature;
            Tier = 3;
            Range = 24;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 2.5f);

            DamageMultiplierStat = ShamanStatKeys.ForkedLightningDamage; // Storm Reach
            ConsumesCasterEffectId = ShamanEffectIds.Maelstrom;

            float coeff = b.F("coeff", 2.0f);
            int jumps = (int)b.F("chainJumps", 3f);
            float falloff = b.F("chainFalloff", 0.7f);
            float vsMarked = b.F("stormstrikeMultiplier", 1.2f);
            DescArgs = new object[] { coeff, jumps, (int)System.Math.Round((1f - falloff) * 100f), vsMarked };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0f,
                // Only the direct hit takes the marked bonus; the jumps ride the plain base.
                DamageVsControlledMultiplier = vsMarked,
                DamageVsControlEffectId = ShamanEffectIds.ThunderCleave,
                ConsumeControlOnHit = false,
                BoltFx = true,
                ChainJumps = jumps,
                ChainRadius = b.F("chainRadius", 6f),
                ChainFalloff = falloff,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });

            ConfigureCost(defResource: 45f, defCooldown: 8f); // mana
        }
    }
}
