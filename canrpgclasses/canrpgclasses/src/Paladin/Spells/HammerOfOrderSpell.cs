using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    [SpellRegistration("canrpgclasses:hammer_of_order")]
    public class HammerOfOrderSpell : Spell
    {
        public HammerOfOrderSpell()
        {
            var b = Balance;
            DisplayName = "Hammer of Order";
            IconName = "mailed-fist";
            School = SpellSchool.Holy;
            Tier = 2;
            Range = 8;

            Target.Type = TargetType.Aim;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Stun,
                StatusEffectDuration = b.F("stun", 2.5f),
                Particles = new ParticleSpec { ColorA = 230, ColorR = 255, ColorG = 235, ColorB = 120, Glow = true, VelocityY = 0.3f }
            });

            ConfigureCost(defResource: 15f, defCooldown: 18f); // mana
        }
    }
}
