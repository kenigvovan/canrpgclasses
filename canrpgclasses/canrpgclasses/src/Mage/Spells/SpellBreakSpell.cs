using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:spell_break")]
    public class SpellBreakSpell : Spell
    {
        public SpellBreakSpell()
        {
            var b = Balance;
            DisplayName = "Spell Break";
            IconName = "mute";
            School = SpellSchool.Arcane;
            Tier = 4;
            Range = 24;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            int duration = (int)b.F("duration", 4f);
            DescArgs = new object[] { duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Silence,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.ForSchool(SpellSchool.Arcane)
            });

            ConfigureCost(defResource: 20f, defCooldown: 24f); // mana
        }
    }
}
