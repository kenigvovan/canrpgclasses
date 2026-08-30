using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    [SpellRegistration("canrpgclasses:battle_rush")]
    public class BattleRushSpell : Spell
    {
        public BattleRushSpell()
        {
            var b = Balance;
            DisplayName = "Battle Rush";
            IconName = "embrassed-energy";
            School = SpellSchool.PhysicalMelee;
            Tier = 4;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "walkspeed",
                StatusEffectDuration = b.F("speedDuration", 8f),
                StatusEffectAmplifier = b.I("speedAmp", 2),
                StatusEffectAmplifierCap = b.I("speedCap", 3)
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "strengthmelee",
                StatusEffectDuration = b.F("strDuration", 8f),
                StatusEffectAmplifier = b.I("strAmp", 3),
                StatusEffectAmplifierCap = b.I("strCap", 9),
                Particles = new ParticleSpec { ColorA = 200, ColorR = 255, ColorG = 180, ColorB = 40, Glow = true, Gravity = -0.15f, VelocityY = 1.0f, MinQuantity = 14f, AddQuantity = 10f }
            });

            ConfigureCost(defResource: 0f, defCooldown: 30f); // energy
        }
    }
}
