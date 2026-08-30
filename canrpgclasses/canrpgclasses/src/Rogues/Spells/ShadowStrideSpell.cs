using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    /// <summary>The aim only positions the teleport - the invisibility lands on the caster.</summary>
    [SpellRegistration("canrpgclasses:shadow_stride")]
    public class ShadowStrideSpell : Spell
    {
        public ShadowStrideSpell()
        {
            var b = Balance;
            DisplayName = "Shadow Stride";
            IconName = "shadow-follower";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;
            Range = 12;

            Target.Type = TargetType.Aim;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Teleport,
                TeleportMode = TeleportMode.BehindTarget,
                TeleportDistance = 1.5f,
                TeleportFaceTarget = true,
                // A puff of grey smoke left where the caster was standing.
                Particles = new ParticleSpec
                {
                    ColorA = 180, ColorR = 90, ColorG = 90, ColorB = 100,
                    MinQuantity = 14f, AddQuantity = 10f,
                    MinSize = 0.25f, MaxSize = 0.55f,
                    LifeLength = 0.6f, VelocityY = 0.4f, Gravity = -0.05f
                }
            });
            Impacts.Add(SpellImpact.Invisibility(b.F("invisDuration", 2f), affectCaster: true));

            ConfigureCost(defResource: 25f, defCooldown: 12f); // energy
        }
    }
}
