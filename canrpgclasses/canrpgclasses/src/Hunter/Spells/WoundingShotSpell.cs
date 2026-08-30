using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    /// <summary>The heal cut lands as WoundedEffect (effectshud), not as a damage modifier.</summary>
    [SpellRegistration("canrpgclasses:wounding_shot")]
    public class WoundingShotSpell : Spell
    {
        public WoundingShotSpell()
        {
            var b = Balance;
            DisplayName = "Wounding Shot";
            IconName = "ragged-wound";
            School = SpellSchool.PhysicalRanged;
            Tier = 3;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            RequiresBow = true;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.EmpowerNextShot,
                DamageSpellPowerCoefficient = b.F("coeff", 0.5f),
                ShotHealCutPercent = b.F("healCut", 0.5f),
                ShotHealCutSeconds = b.F("healCutSecs", 8f),
                EmpowerWindowSeconds = b.F("window", 6f),
                Particles = new ParticleSpec { ColorA = 220, ColorR = 180, ColorG = 20, ColorB = 20, Gravity = -0.1f }
            });

            ConfigureCost(defResource: 25f, defCooldown: 12f); // focus
        }
    }
}
