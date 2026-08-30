using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    /// <summary>Hitscan along the aim ray, so the Holy Power is granted on cast like the other builders.</summary>
    [SpellRegistration("canrpgclasses:sentence")]
    public class SentenceSpell : Spell
    {
        public SentenceSpell()
        {
            var b = Balance;
            DisplayName = "Sentence";
            IconName = "tarot-20-judgement";
            School = SpellSchool.Holy;
            Tier = 2;
            Range = 20;

            Target.Type = TargetType.Aim;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = b.F("coeff", 2.0f),
                Knockback = b.F("knockback", 0.5f),
                RuneFx = true, // a sigil is branded on the ground under the target
                // A bright narrow pillar, to read differently from the soft golden puff of the other holy spells.
                Particles = new ParticleSpec
                {
                    ColorA = 230, ColorR = 255, ColorG = 250, ColorB = 220, // bright white-gold
                    Glow = true,
                    MinQuantity = 20f, AddQuantity = 14f,
                    VelocityHoriz = 0.4f, // narrow, so it reads as a column
                    VelocityY = 2.2f,
                    LifeLength = 0.6f,
                    MinSize = 0.25f, MaxSize = 0.5f
                }
            });

            ComboBuilder = true;
            // Zealous Sentence raises this stat to sharpen Sentence alone, the way finisherDamage sharpens finishers.
            DamageMultiplierStat = Core.StatKeys.JudgementDamage;
            ConfigureCost(defResource: 12f, defCooldown: 8f); // mana
        }
    }
}
