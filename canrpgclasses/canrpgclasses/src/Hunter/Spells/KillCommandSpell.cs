using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    /// <summary>Sends the wolf at the aimed foe and charges its next bite; HunterPetSystem spends the charge on the bite.</summary>
    [SpellRegistration("canrpgclasses:kill_command")]
    public class KillCommandSpell : Spell
    {
        public KillCommandSpell()
        {
            var b = Balance;
            DisplayName = "Kill Command";
            IconName = "neck-bite";
            School = SpellSchool.Nature;
            Tier = 1;
            Range = 20f;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            // The executor grants the combo point on cast, since the target is an enemy.
            ComboBuilder = true;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.PetCommand,
                PetCommand = "kill",
                DamageSpellPowerCoefficient = b.F("coeff", 2.5f),
                EmpowerWindowSeconds = b.F("window", 6f),
                Particles = new ParticleSpec { ColorA = 210, ColorR = 255, ColorG = 80, ColorB = 50, VelocityY = 0.4f }
            });

            ConfigureCost(defResource: 30f, defCooldown: 8f); // focus
        }
    }
}
