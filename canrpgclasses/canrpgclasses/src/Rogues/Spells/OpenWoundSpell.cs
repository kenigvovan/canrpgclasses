using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    [SpellRegistration("canrpgclasses:open_wound")]
    public class OpenWoundSpell : Spell
    {
        public OpenWoundSpell()
        {
            var b = Balance;
            DisplayName = "Open Wound";
            IconName = "burning-dot";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;
            Range = 4;

            Target.Type = TargetType.Aim;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact { Action = ImpactAction.Damage, DamageSpellPowerCoefficient = b.F("coeff", 1.5f),
                Particles = new ParticleSpec { ColorA = 230, ColorR = 180, ColorG = 20, ColorB = 20, MinQuantity = 14f, AddQuantity = 10f, Gravity = -0.2f } });
            // 4s base plus 2s per point, so up to 14s at five.
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "jagged_blades",
                StatusEffectDuration = b.F("bleedDuration", 4f),
                DurationPerComboPoint = b.F("bleedPerCombo", 2f),
                StatusEffectAmplifier = b.I("bleedAmp", 2),
                StatusEffectAmplifierCap = b.I("bleedCap", 3)
            });

            ComboFinisher = true;
            ConfigureCost(defResource: 30f, defCooldown: 8f); // energy
        }
    }
}
