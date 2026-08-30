using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    /// <summary>Folds into the next real melee hit instead of dealing damage itself, so the target's i-frames
    /// can't swallow it. The Holy Power goes on cast.</summary>
    [SpellRegistration("canrpgclasses:righteous_verdict")]
    public class RighteousVerdictSpell : Spell
    {
        public RighteousVerdictSpell()
        {
            var b = Balance;
            DisplayName = "Righteous Verdict";
            IconName = "knight-banner";
            School = SpellSchool.Holy;
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
                EmpowerWindowSeconds = b.F("empowerWindow", 6f), // land a melee hit inside the window, or lose it
                Particles = new ParticleSpec { ColorA = 230, ColorR = 255, ColorG = 235, ColorB = 120, Glow = true, MinQuantity = 18f, AddQuantity = 14f, VelocityY = 1.0f }
            });

            ComboFinisher = true;
            ConfigureCost(defResource: 5f, defCooldown: 6f); // the real cost is Holy Power
        }
    }
}
