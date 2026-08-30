using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:flicker")]
    public class FlickerSpell : Spell
    {
        public FlickerSpell()
        {
            var b = Balance;
            DisplayName = "Flicker";
            IconName = "air-zigzag";
            School = SpellSchool.Arcane;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Teleport,
                TeleportMode = TeleportMode.Forward,
                TeleportDistance = b.F("distance", 6f),
                Particles = ParticleSpec.ForSchool(SpellSchool.Arcane)
            });

            ConfigureCost(defResource: 15f, defCooldown: 12f); // mana
        }
    }
}
