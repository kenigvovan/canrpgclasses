using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    [SpellRegistration("canrpgclasses:all_out_attack")]
    public class AllOutAttackSpell : Spell
    {
        public AllOutAttackSpell()
        {
            var b = Balance;
            DisplayName = "All-Out Attack";
            IconName = "swords-power";
            School = SpellSchool.PhysicalMelee;
            Tier = 4;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "strengthmelee",
                StatusEffectDuration = b.F("duration", 12f),
                StatusEffectAmplifier = b.I("amp", 3),
                StatusEffectAmplifierCap = b.I("cap", 9),
                Particles = new ParticleSpec { ColorA = 210, ColorR = 255, ColorG = 90, ColorB = 40, Glow = true, Gravity = -0.15f, VelocityY = 1.1f, MinQuantity = 16f, AddQuantity = 12f }
            });
            Impacts.Add(new SpellImpact { Action = ImpactAction.GainResource, ResourceGainAmount = b.F("rageGain", 20f) });

            ConfigureCost(defResource: 0f, defCooldown: 120f);
        }
    }
}
