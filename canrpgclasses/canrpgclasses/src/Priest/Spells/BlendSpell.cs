using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    /// <summary>There is no aggro wipe - a mob drops its target through its own AI while the priest is hidden.</summary>
    [SpellRegistration("canrpgclasses:blend")]
    public class BlendSpell : Spell
    {
        public BlendSpell()
        {
            var b = Balance;
            DisplayName = "Blend";
            IconName = "wing-cloak";
            School = SpellSchool.Shadow;
            Tier = 3;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            float duration = b.F("duration", 5f);
            int slowPct = (int)System.Math.Round(b.F("slowAmp", 2f) * 15f); // walkslow is about 15% per tier
            DescArgs = new object[] { (int)duration, slowPct };

            Impacts.Add(SpellImpact.Invisibility(duration));
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                AffectCaster = true,
                StatusEffectId = "walkslow",
                StatusEffectDuration = (int)duration,
                StatusEffectAmplifier = (int)b.F("slowAmp", 2f),
                StatusEffectAmplifierCap = (int)b.F("slowCap", 3f)
            });

            ConfigureCost(defResource: 20f, defCooldown: 45f); // mana
        }
    }
}
