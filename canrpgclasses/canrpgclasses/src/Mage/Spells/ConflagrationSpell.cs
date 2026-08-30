using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:conflagration")]
    public class ConflagrationSpell : Spell
    {
        public ConflagrationSpell()
        {
            var b = Balance;
            DisplayName = "Conflagration";
            IconName = "heptagram";
            School = SpellSchool.Fire;
            Tier = 5;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            int duration = (int)b.F("duration", 15f);
            DescArgs = new object[] { (int)System.Math.Round(b.F("spellPower", 0.30f) * 100f), duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                AffectCaster = true,
                StatusEffectId = MageEffectIds.Conflagration,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.ForSchool(SpellSchool.Fire)
            });

            ConfigureCost(defResource: 0f, defCooldown: 120f);
        }
    }
}
