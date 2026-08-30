using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    /// <summary>The burn is a snapshot DoT - its per-second damage is fixed at cast.</summary>
    [SpellRegistration("canrpgclasses:sacred_flame")]
    public class SacredFlameSpell : Spell
    {
        public SacredFlameSpell()
        {
            var b = Balance;
            DisplayName = "Sacred Flame";
            IconName = "tarot-19-the-sun";
            School = SpellSchool.Holy;
            Tier = 3;
            Range = 24;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 2f);

            float coeff = b.F("coeff", 1.5f);
            int duration = (int)b.F("duration", 8f);
            DescArgs = new object[] { coeff, duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Knockback = 0f,
                PillarFx = true, // a column of fire strikes down on the target
                // With Penitence, part of the hit also heals shielded party allies.
                DamageSplashHealStat = Talents.AtonementTalent.AtonementStat,
                Particles = ParticleSpec.ForSchool(SpellSchool.Holy)
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = PriestEffectIds.SacredFlame,
                StatusEffectDuration = duration
            });

            ConfigureCost(defResource: 30f, defCooldown: 10f); // mana
        }
    }
}
