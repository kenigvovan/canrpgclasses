using canrpgclasses.Core.Config;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    [SpellRegistration("canrpgclasses:mind_shock")]
    public class MindShockSpell : Spell
    {
        /// <summary>Damage multiplier for Mind Shock, raised by Mind Erosion. Shared with that talent.</summary>
        public const string MindShockDamageStat = "mindBlastDamage";

        public MindShockSpell()
        {
            var b = Balance;
            DisplayName = "Mind Shock";
            IconName = "haunting";
            School = SpellSchool.Shadow;
            Tier = 2;
            Range = 24;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 1.5f);

            DamageMultiplierStat = MindShockDamageStat;

            float coeff = b.F("coeff", 3.0f);
            int slowDuration = (int)b.F("slowDuration", 3f);
            DescArgs = new object[] { coeff, slowDuration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = b.F("knockback", 0.3f),
                SparkFx = true, // a sharp burst flies off the target, away from the caster
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
            // Banks a Shadow Orb on the priest. TryCast needs an enemy under the crosshair, so a completed cast
            // reliably earns its orb.
            var orbs = BalanceConfig.Spell("shadow_orbs");
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                AffectCaster = true,
                StatusEffectId = PriestEffectIds.ShadowOrbs,
                StatusEffectDuration = orbs.F("duration", 20f),
                StatusEffectAmplifier = 1,
                StatusEffectAmplifierCap = (int)orbs.F("cap", 3f),
                StatusEffectApplyMode = StatusApplyMode.Add
            });

            ConfigureCost(defResource: 35f, defCooldown: 8f); // mana
        }
    }
}
