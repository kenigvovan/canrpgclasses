using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    /// <summary>Affects mobs only - players are never taunted.</summary>
    [SpellRegistration("canrpgclasses:reckoning")]
    public class ReckoningSpell : Spell
    {
        public ReckoningSpell()
        {
            var b = Balance;
            DisplayName = "Reckoning";
            IconName = "shouting";
            School = SpellSchool.Holy;
            Tier = 2;

            Target.Type = TargetType.Caster; // the Aggro impact runs its own radius scan
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Aggro,
                AggroRange = b.F("aggroRange", 10f),
                Particles = new ParticleSpec { ColorA = 210, ColorR = 255, ColorG = 210, ColorB = 90, Glow = true, VelocityY = 0.6f, MinQuantity = 14f, AddQuantity = 10f }
            });

            ConfigureCost(defResource: 10f, defCooldown: 12f); // mana
        }
    }
}
