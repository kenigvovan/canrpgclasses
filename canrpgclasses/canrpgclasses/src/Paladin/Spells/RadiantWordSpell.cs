using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    [SpellRegistration("canrpgclasses:radiant_word")]
    public class RadiantWordSpell : Spell
    {
        public RadiantWordSpell()
        {
            var b = Balance;
            DisplayName = "Radiant Word";
            IconName = "griffin-symbol";
            School = SpellSchool.Holy;
            Tier = 2;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally; // ally under the crosshair, else self
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Heal,
                HealSpellPowerCoefficient = b.F("healCoeff", 1f),       // small base
                HealPerComboPoint = b.F("healPerHolyPower", 3f),        // the Holy Power is where the heal comes from
                BloomFx = true,
                Particles = ParticleSpec.Heal()
            });

            ComboFinisher = true;
            ConfigureCost(defResource: 5f, defCooldown: 0f); // mana
        }
    }
}
