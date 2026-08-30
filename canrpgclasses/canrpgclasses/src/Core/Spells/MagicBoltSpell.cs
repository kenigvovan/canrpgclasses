namespace canrpgclasses.Core.Spells
{
    /// <summary>
    /// Test bolt for projectile delivery, cast with <c>/canrpgcast magic_bolt</c>. Belongs to no class, so it is
    /// reachable only through that command.
    /// </summary>
    [SpellRegistration("canrpgclasses:magic_bolt")]
    public class MagicBoltSpell : Spell
    {
        public MagicBoltSpell()
        {
            IconName = "bolt-spell-cast";
            School = SpellSchool.PhysicalRanged;
            Tier = 1;
            Range = 24;

            Target.Type = TargetType.None;
            Deliver.Type = DeliveryType.Projectile;
            Deliver.ProjectileVelocity = 0.8f;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = 3f,
                Knockback = 1f,
                Particles = new ParticleSpec { ColorA = 220, ColorR = 120, ColorG = 160, ColorB = 255, Glow = true, MinQuantity = 16f, AddQuantity = 12f }
            });

            Cost.Cooldown.Duration = 2f;
        }
    }
}
