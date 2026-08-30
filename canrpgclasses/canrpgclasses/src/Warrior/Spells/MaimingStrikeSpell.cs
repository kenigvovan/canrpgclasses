using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    /// <summary>A direct hit rather than an empowered next swing, because the empower bundle can't carry a debuff
    /// onto the struck target.</summary>
    [SpellRegistration("canrpgclasses:maiming_strike")]
    public class MaimingStrikeSpell : Spell
    {
        public MaimingStrikeSpell()
        {
            var b = Balance;
            DisplayName = "Maiming Strike";
            IconName = "ragged-wound";
            School = SpellSchool.PhysicalMelee;
            Tier = 3;
            Range = 4;

            Target.Type = TargetType.Aim;
            Deliver.Type = DeliveryType.Direct;
            DamageMultiplierStat = WarriorStatKeys.MaimingDamage; // sharpened by Precise Strikes
            DescArgs = new object[]
            {
                (int)System.Math.Round(b.F("healCut", 0.5f) * 100f),
                (int)System.Math.Round(b.F("healCutSecs", 6f))
            };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = b.F("coeff", 3.5f),
                Knockback = 0f,
                Particles = ParticleSpec.ForSchool(SpellSchool.PhysicalMelee)
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = MortalWoundEffectId.Id,
                StatusEffectDuration = b.F("healCutSecs", 6f),
                StatusEffectAmplifier = 1,
                StatusEffectAmplifierCap = 1
            });

            ConfigureCost(defResource: 30f, defCooldown: 12f); // rage
        }
    }
}
