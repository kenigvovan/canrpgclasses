using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    /// <summary>Breaks the instant the target takes damage, so it is control rather than a damage setup.</summary>
    [SpellRegistration("canrpgclasses:blind")]
    public class BlindSpell : Spell
    {
        public BlindSpell()
        {
            var b = Balance;
            DisplayName = "Blind";
            IconName = "bleeding-eye";
            School = SpellSchool.PhysicalRanged; // thrown powder
            Tier = 2;
            Range = b.F("range", 8f);

            Target.Type = TargetType.Aim;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Stun,
                StatusEffectDuration = b.F("duration", 8f),
                StunBreaksOnDamage = true,
                // Screen-distortion feedback while blinded: reuses the game's own "psychedelic" perception effect
                // (the same one hallucinogenic mushrooms trigger) - no new client code needed.
                StunBlindsVision = true,
                StunVisionIntensity = b.F("visionIntensity", 1f)
            });

            ConfigureCost(defResource: 25f, defCooldown: 25f);   // energy
        }
    }
}
