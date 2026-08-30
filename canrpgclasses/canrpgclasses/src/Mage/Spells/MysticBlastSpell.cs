using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    /// <summary>Each Mystic Charge raises the mage's arcane spell power AND this spell's own mana cost, so
    /// ramping has to be dumped before it prices itself out.</summary>
    [SpellRegistration("canrpgclasses:mystic_blast")]
    public class MysticBlastSpell : Spell
    {
        /// <summary>Stat the Mystic Instability talent raises to boost this spell's damage.</summary>
        public const string MysticBlastDamageStat = "arcaneBlastDamage";

        /// <summary>Multiplies this spell's mana cost, raised per charge by
        /// <see cref="canrpgclasses.Mage.MysticChargesEffect"/>.</summary>
        public const string MysticChargeCostStat = "arcaneChargeCost";

        public MysticBlastSpell()
        {
            var b = Balance;
            DisplayName = "Mystic Blast";
            IconName = "magic-palm";
            School = SpellSchool.Arcane;
            Tier = 2;
            Range = 24;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 1.8f);
            DamageMultiplierStat = MysticBlastDamageStat;
            ResourceCostMultiplierStat = MysticChargeCostStat;

            float coeff = b.F("coeff", 2.2f);
            int chargeCap = b.I("chargeCap", 4);
            float chargeDuration = b.F("chargeDuration", 10f);
            float dmgPerStack = b.F("chargeDamagePerStack", 0.15f);
            float costPerStack = b.F("chargeCostPerStack", 0.35f);
            DescArgs = new object[]
            {
                coeff, chargeCap,
                (int)System.Math.Round(dmgPerStack * 100f),
                (int)System.Math.Round(costPerStack * 100f)
            };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0f,
                TrailFx = true, // no bolt to watch, so a mote streaks over instead
                Particles = ParticleSpec.ForSchool(SpellSchool.Arcane)
            });

            // The charge lands on the mage, not the target. TryCast needs an enemy under the crosshair, so a
            // completed cast always earns one - an interrupted cast doesn't.
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                AffectCaster = true,
                StatusEffectId = MageEffectIds.MysticCharges,
                StatusEffectDuration = chargeDuration,
                StatusEffectAmplifier = 1,
                StatusEffectAmplifierCap = chargeCap,
                StatusEffectApplyMode = StatusApplyMode.Add
            });

            ConfigureCost(defResource: 30f, defCooldown: 0f); // mana, times the charge multiplier
        }
    }
}
