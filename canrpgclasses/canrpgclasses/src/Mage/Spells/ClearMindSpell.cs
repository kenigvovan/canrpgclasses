using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:clear_mind")]
    public class ClearMindSpell : Spell
    {
        public ClearMindSpell()
        {
            var b = Balance;
            DisplayName = "Clear Mind";
            IconName = "sundial";
            School = SpellSchool.Fire;
            Tier = 3;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            int window = (int)b.F("window", 15f);
            DescArgs = new object[] { window };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.GrantInstantCast,
                AffectCaster = true,
                StatusEffectDuration = window,
                StatusEffectId = MageEffectIds.ClearMind
            });

            ConfigureCost(defResource: 0f, defCooldown: 120f);
        }
    }
}
