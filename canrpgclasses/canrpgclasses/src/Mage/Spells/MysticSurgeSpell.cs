using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:mystic_surge")]
    public class MysticSurgeSpell : Spell
    {
        public MysticSurgeSpell()
        {
            var b = Balance;
            DisplayName = "Mystic Surge";
            IconName = "embrassed-energy";
            School = SpellSchool.Arcane;
            Tier = 5;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            int duration = (int)b.F("duration", 15f);
            DescArgs = new object[] { (int)System.Math.Round(b.F("spellPower", 0.30f) * 100f), duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                AffectCaster = true,
                StatusEffectId = MageEffectIds.MysticSurge,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.ForSchool(SpellSchool.Arcane)
            });

            ConfigureCost(defResource: 0f, defCooldown: 120f);
        }
    }
}
