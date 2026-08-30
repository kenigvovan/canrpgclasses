using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    [SpellRegistration("canrpgclasses:full_guard")]
    public class FullGuardSpell : Spell
    {
        public FullGuardSpell()
        {
            var b = Balance;
            DisplayName = "Full Guard";
            IconName = "shield-opposition";
            School = SpellSchool.PhysicalMelee;
            Tier = 3;
            RequiresShield = true;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            DescArgs = new object[] { (int)System.Math.Round(b.F("damageReduction", 0.40f) * 100f) };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = FullGuardEffectId.Id,
                StatusEffectDuration = b.F("duration", 8f),
                StatusEffectAmplifier = 1,
                StatusEffectAmplifierCap = 1,
                AffectCaster = true,
                Particles = new ParticleSpec { ColorA = 200, ColorR = 180, ColorG = 210, ColorB = 235, Glow = true, VelocityY = 0.8f }
            });

            ConfigureCost(defResource: 20f, defCooldown: 60f); // rage
        }
    }
}
