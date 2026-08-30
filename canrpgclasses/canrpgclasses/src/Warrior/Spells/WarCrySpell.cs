using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    [SpellRegistration("canrpgclasses:war_cry")]
    public class WarCrySpell : Spell
    {
        public WarCrySpell()
        {
            var b = Balance;
            DisplayName = "War Cry";
            IconName = "shouting";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;
            Range = b.F("radius", 10f);

            Target.Type = TargetType.Area;
            Target.Affinity = TargetAffinity.Ally;
            Target.AreaIncludeCaster = true;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "strengthmelee",
                StatusEffectDuration = b.F("duration", 20f),
                StatusEffectAmplifier = b.I("amp", 2),
                StatusEffectAmplifierCap = b.I("cap", 3),
                Particles = new ParticleSpec { ColorA = 200, ColorR = 220, ColorG = 60, ColorB = 40, Glow = true, Gravity = -0.15f, VelocityY = 1.0f, MinQuantity = 14f, AddQuantity = 10f }
            });

            ConfigureCost(defResource: 15f, defCooldown: 40f); // rage
        }
    }
}
