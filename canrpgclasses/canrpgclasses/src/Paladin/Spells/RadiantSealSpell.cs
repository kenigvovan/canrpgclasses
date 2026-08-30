using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    /// <summary>The coat applies its effect to the struck enemy, so what it carries must be a debuff, never a
    /// self-buff.</summary>
    [SpellRegistration("canrpgclasses:radiant_seal")]
    public class RadiantSealSpell : Spell
    {
        public RadiantSealSpell()
        {
            var b = Balance;
            DisplayName = "Radiant Seal";
            IconName = "heptagram";
            School = SpellSchool.Holy;
            Tier = 2;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.CoatWeapon,
                StatusEffectId = "weakmelee", // lands on the struck enemy, weakening its attacks
                StatusEffectAmplifier = b.I("tier", 2),
                CoatCharges = b.I("charges", 8),
                Particles = new ParticleSpec { ColorA = 200, ColorR = 255, ColorG = 235, ColorB = 130, Glow = true, VelocityY = 0.7f }
            });

            ConfigureCost(defResource: 15f, defCooldown: 20f); // mana
        }
    }
}
