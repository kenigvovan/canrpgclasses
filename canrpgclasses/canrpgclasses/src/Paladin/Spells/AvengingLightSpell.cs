using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    [SpellRegistration("canrpgclasses:avenging_light")]
    public class AvengingLightSpell : Spell
    {
        public AvengingLightSpell()
        {
            var b = Balance;
            DisplayName = "Avenging Light";
            IconName = "sharp-crown";
            School = SpellSchool.Holy;
            Tier = 4;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "walkspeed",
                StatusEffectDuration = b.F("speedDuration", 12f),
                StatusEffectAmplifier = b.I("speedAmp", 2),
                StatusEffectAmplifierCap = b.I("speedCap", 3)
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "strengthmelee",
                StatusEffectDuration = b.F("strDuration", 12f),
                StatusEffectAmplifier = b.I("strAmp", 3),
                StatusEffectAmplifierCap = b.I("strCap", 9),
                Particles = new ParticleSpec { ColorA = 210, ColorR = 255, ColorG = 230, ColorB = 110, Glow = true, Gravity = -0.15f, VelocityY = 1.1f, MinQuantity = 16f, AddQuantity = 12f } // golden wings
            });

            ConfigureCost(defResource: 20f, defCooldown: 90f); // mana
        }
    }
}
