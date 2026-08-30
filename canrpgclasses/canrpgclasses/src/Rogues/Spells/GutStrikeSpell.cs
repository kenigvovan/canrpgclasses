using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    /// <summary>Folds into the next real melee hit instead of landing on its own, so the target's i-frames can't
    /// swallow it. The combo points go on cast.</summary>
    [SpellRegistration("canrpgclasses:gut_strike")]
    public class GutStrikeSpell : Spell
    {
        public GutStrikeSpell()
        {
            var b = Balance;
            DisplayName = "Gut Strike";
            IconName = "ragged-wound";
            School = SpellSchool.PhysicalMelee;
            Tier = 3;

            Target.Type = TargetType.Caster; // self-buff: empowers the next melee hit
            Deliver.Type = DeliveryType.Direct;

            // 2 + 1.5 per point, so up to 9.5x spell power at five.
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.EmpowerNextMelee,
                DamageSpellPowerCoefficient = b.F("coeff", 2f),
                DamagePerComboPoint = b.F("perCombo", 1.5f),
                Knockback = b.F("knockback", 0.4f),
                EmpowerWindowSeconds = b.F("empowerWindow", 6f),
                Particles = new ParticleSpec { ColorA = 230, ColorR = 200, ColorG = 30, ColorB = 30, MinQuantity = 18f, AddQuantity = 14f, VelocityHoriz = 1.6f } // heavy crimson spray
            });

            ComboFinisher = true;
            ConfigureCost(defResource: 35f, defCooldown: 10f); // energy
        }
    }
}
