using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:icy_veins")]
    public class IcyVeinsSpell : Spell
    {
        public IcyVeinsSpell()
        {
            var b = Balance;
            DisplayName = "Frozen Mind";
            IconName = "brain-freeze";
            School = SpellSchool.Frost;
            Tier = 5;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            int duration = (int)b.F("duration", 15f);
            DescArgs = new object[] { (int)System.Math.Round(b.F("spellPower", 0.30f) * 100f), duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                AffectCaster = true,
                StatusEffectId = MageEffectIds.IcyVeins,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.ForSchool(SpellSchool.Frost)
            });

            ConfigureCost(defResource: 0f, defCooldown: 120f); // free, long cooldown
        }
    }
}
