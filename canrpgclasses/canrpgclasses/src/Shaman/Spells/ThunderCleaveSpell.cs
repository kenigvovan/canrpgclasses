using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    /// <summary>The damage rides the empower bundle, but the marker has to be stamped when the swing lands, since
    /// an empower can't apply a status effect to the target it hits.</summary>
    [SpellRegistration("canrpgclasses:thunder_cleave")]
    public class ThunderCleaveSpell : Spell
    {
        public ThunderCleaveSpell()
        {
            var b = Balance;
            DisplayName = "Thunder Cleave";
            IconName = "swords-power";
            School = SpellSchool.Nature;
            Tier = 3;

            Target.Type = TargetType.Caster; // self-buff: empowers the next melee hit
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;

            DamageMultiplierStat = ShamanStatKeys.ThunderCleaveDamage;

            float coeff = b.F("empowerCoeff", 1.5f);
            float window = b.F("empowerWindow", 6f);
            int marker = (int)b.F("markerDuration", 12f);
            DescArgs = new object[] { coeff, marker };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.EmpowerNextMelee,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0.2f,
                EmpowerWindowSeconds = window,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });
            // The marker carrier: same window, and it drops as soon as it stamps a target.
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                AffectCaster = true,
                StatusEffectId = ShamanEffectIds.ThunderCleaveArmed,
                StatusEffectDuration = window
            });

            ConfigureCost(defResource: 30f, defCooldown: 10f); // mana
        }
    }
}
