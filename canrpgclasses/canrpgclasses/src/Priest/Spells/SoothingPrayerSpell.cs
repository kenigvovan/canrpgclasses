using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    /// <summary>Per-tick heal is snapshotted at cast. effectshud ticks once a second, so the duration in seconds
    /// is also the tick count.</summary>
    [SpellRegistration("canrpgclasses:soothing_prayer")]
    public class SoothingPrayerSpell : Spell
    {
        public SoothingPrayerSpell()
        {
            var b = Balance;
            DisplayName = "Soothing Prayer";
            IconName = "bandaged";
            School = SpellSchool.Holy;
            Tier = 1;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct;

            float duration = b.F("duration", 12f);
            DescArgs = new object[] { (int)duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = PriestEffectIds.SoothingPrayer,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 25f, defCooldown: 3f); // mana
        }
    }
}
