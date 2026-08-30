using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    [SpellRegistration("canrpgclasses:beast_fury")]
    public class BeastFurySpell : Spell
    {
        public BeastFurySpell()
        {
            var b = Balance;
            DisplayName = "Beast Fury";
            IconName = "enrage";
            School = SpellSchool.Nature;
            Tier = 3;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.PetTarget,
                // Here the coefficient is the pet's damage bonus and the duration is the window; the status
                // effect is only the HUD icon.
                DamageSpellPowerCoefficient = b.F("damageBonus", 0.4f),
                StatusEffectDuration = b.F("duration", 12f),
                StatusEffectId = "strengthmelee",
                StatusEffectAmplifier = 3,
                Particles = new ParticleSpec { ColorA = 210, ColorR = 255, ColorG = 120, ColorB = 40, Glow = true, Gravity = -0.1f, VelocityY = 0.8f, MinQuantity = 14f, AddQuantity = 10f }
            });

            ConfigureCost(defResource: 25f, defCooldown: 45f); // focus
        }
    }
}
