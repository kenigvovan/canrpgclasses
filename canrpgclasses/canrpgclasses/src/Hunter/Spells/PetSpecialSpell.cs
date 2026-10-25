using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    /// <summary>The out pet's own ability, picked by its species profile ("special"); HunterPetSystem.UseSpecial.</summary>
    [SpellRegistration("canrpgclasses:pet_special")]
    public class PetSpecialSpell : Spell
    {
        public PetSpecialSpell()
        {
            DisplayName = "Pet: Special";
            IconName = "wyvern";
            School = SpellSchool.Nature;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.PetCommand, PetCommand = "special", AffectCaster = true,
                Particles = new ParticleSpec { ColorA = 210, ColorR = 255, ColorG = 200, ColorB = 80, VelocityY = 0.4f }
            });

            ConfigureCost(defResource: 20f, defCooldown: 20f); // focus
        }
    }
}
