using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    [SpellRegistration("canrpgclasses:rend")]
    public class RendSpell : Spell
    {
        public RendSpell()
        {
            var b = Balance;
            DisplayName = "Bleeding Cut";
            IconName = "dripping-blade";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;
            Range = 4;

            Target.Type = TargetType.Aim;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = b.F("coeff", 0.7f),
                Knockback = 0f,
                Particles = new ParticleSpec { ColorA = 230, ColorR = 180, ColorG = 20, ColorB = 20, MinQuantity = 12f, AddQuantity = 8f, Gravity = -0.2f }
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "jagged_blades",
                StatusEffectDuration = b.F("bleedDuration", 8f),
                StatusEffectAmplifier = b.I("bleedAmp", 2),
                StatusEffectAmplifierCap = b.I("bleedCap", 3)
            });

            ConfigureCost(defResource: 15f, defCooldown: 8f); // rage
        }
    }
}
