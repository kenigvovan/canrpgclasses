using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    /// <summary>Sets the canrpgSilenced flag read by EBSpellCaster - a mob that doesn't cast is unaffected.</summary>
    [SpellRegistration("canrpgclasses:silence")]
    public class SilenceSpell : Spell
    {
        public SilenceSpell()
        {
            var b = Balance;
            DisplayName = "Silence";
            IconName = "mute";
            School = SpellSchool.Shadow;
            Tier = 3;
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
                Particles = ParticleSpec.ForSchool(SpellSchool.Shadow)
            });

            ConfigureCost(defResource: 25f, defCooldown: 20f); // mana
        }
    }
}
