using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    [SpellRegistration("canrpgclasses:shield_guard")]
    public class ShieldGuardSpell : Spell
    {
        public ShieldGuardSpell()
        {
            var b = Balance;
            DisplayName = "Shield Guard";
            IconName = "shield-echoes";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;
            RequiresShield = true;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            DescArgs = new object[] { (int)System.Math.Round(b.F("damageReduction", 0.30f) * 100f) };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = ShieldGuardEffectId.Id,
                StatusEffectDuration = b.F("duration", 4f),
                StatusEffectAmplifier = 1,
                StatusEffectAmplifierCap = 1,
                AffectCaster = true,
                Particles = new ParticleSpec { ColorA = 190, ColorR = 170, ColorG = 190, ColorB = 220, Glow = true, VelocityY = 0.7f }
            });

            ConfigureCost(defResource: 15f, defCooldown: 8f); // rage
        }
    }
}
