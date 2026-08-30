using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    [SpellRegistration("canrpgclasses:inner_flame")]
    public class InnerFlameSpell : Spell
    {
        public InnerFlameSpell()
        {
            var b = Balance;
            DisplayName = "Inner Flame";
            IconName = "heraldic-sun";
            School = SpellSchool.Holy;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            int duration = (int)b.F("duration", 60f);
            DescArgs = new object[] { (int)System.Math.Round(b.F("damageReduction", 0.08f) * 100f), duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = PriestEffectIds.InnerFlame,
                StatusEffectDuration = duration
            });

            ConfigureCost(defResource: 15f, defCooldown: 10f); // mana
        }
    }
}
