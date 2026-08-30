using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    /// <summary>Real projectiles, each spent on the first thing it hits - a spray, not a guaranteed AoE.</summary>
    [SpellRegistration("canrpgclasses:whirling_knives")]
    public class WhirlingKnivesSpell : Spell
    {
        public WhirlingKnivesSpell()
        {
            var b = Balance;
            DisplayName = "Whirling Knives";
            IconName = "sword-array";
            School = SpellSchool.PhysicalRanged; // thrown blades
            Tier = 3;
            Range = b.F("range", 8f); // how far a knife flies before it fizzles

            Target.Type = TargetType.None;
            Deliver.Type = DeliveryType.Projectile;
            Deliver.ProjectileVelocity = b.F("velocity", 1.2f);
            Deliver.ProjectileCount = b.I("count", 12);
            Deliver.ProjectileSpreadDegrees = b.F("spread", 360f); // a full ring

            // Per knife, so lower than a single-target finisher would deal.
            Impacts.Add(new SpellImpact { Action = ImpactAction.Damage, DamageSpellPowerCoefficient = b.F("coeff", 1.2f), Knockback = b.F("knockback", 0.3f),
                Particles = new ParticleSpec { ColorA = 220, ColorR = 210, ColorG = 70, ColorB = 50, MinQuantity = 8f, AddQuantity = 6f } });

            ConfigureCost(defResource: 35f, defCooldown: 12f); // energy
        }
    }
}
