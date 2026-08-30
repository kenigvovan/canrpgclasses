using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    [SpellRegistration("canrpgclasses:sheltering_spirit")]
    public class ShelteringSpiritSpell : Spell
    {
        public ShelteringSpiritSpell()
        {
            var b = Balance;
            DisplayName = "Sheltering Spirit";
            IconName = "angel-wings";
            School = SpellSchool.Holy;
            Tier = 4;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct;

            int duration = (int)b.F("duration", 10f);
            DescArgs = new object[] { (int)System.Math.Round(b.F("healBoost", 0.40f) * 100f), duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = PriestEffectIds.ShelteringSpirit,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 30f, defCooldown: 90f); // mana
        }
    }
}
