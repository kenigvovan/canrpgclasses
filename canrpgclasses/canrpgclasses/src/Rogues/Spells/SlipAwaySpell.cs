using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    [SpellRegistration("canrpgclasses:slip_away")]
    public class SlipAwaySpell : Spell
    {
        public SlipAwaySpell()
        {
            var b = Balance;
            DisplayName = "Slip Away";
            IconName = "cloud-ring";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            // The same "left the body" puff Shadow Stride leaves behind.
            var vanishImpact = SpellImpact.Invisibility(b.F("invisDuration", 4f));
            vanishImpact.Particles = new ParticleSpec
            {
                ColorA = 180, ColorR = 90, ColorG = 90, ColorB = 100,
                MinQuantity = 14f, AddQuantity = 10f,
                MinSize = 0.25f, MaxSize = 0.55f,
                LifeLength = 0.6f, VelocityY = 0.4f, Gravity = -0.05f
            };
            Impacts.Add(vanishImpact);
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = "walkspeed",
                StatusEffectDuration = b.F("speedDuration", 4f),
                StatusEffectAmplifier = 1,
                StatusEffectAmplifierCap = b.I("speedCap", 3)
            });

            ConfigureCost(defResource: 30f, defCooldown: 25f); // energy
        }
    }
}
