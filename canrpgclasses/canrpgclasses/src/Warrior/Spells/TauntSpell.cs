using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    /// <summary>Forces nearby mobs to attack the caster; players are unaffected.</summary>
    [SpellRegistration("canrpgclasses:taunt")]
    public class TauntSpell : Spell
    {
        public TauntSpell()
        {
            var b = Balance;
            DisplayName = "Taunt";
            IconName = "bell-shield";
            School = SpellSchool.PhysicalMelee;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Aggro,
                AggroRange = b.F("aggroRange", 10f),
                Particles = new ParticleSpec { ColorA = 210, ColorR = 255, ColorG = 120, ColorB = 60, Glow = true, VelocityY = 0.6f, MinQuantity = 14f, AddQuantity = 10f }
            });

            ConfigureCost(defResource: 0f, defCooldown: 10f); // free - a tank must be able to taunt at 0 rage to open
        }
    }
}
