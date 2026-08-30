using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    [SpellRegistration("canrpgclasses:nerve_strike")]
    public class NerveStrikeSpell : Spell
    {
        public NerveStrikeSpell()
        {
            var b = Balance;
            DisplayName = "Nerve Strike";
            IconName = "mailed-fist";
            School = SpellSchool.PhysicalMelee;
            Tier = 3;
            Range = 5;

            Target.Type = TargetType.Aim;
            Deliver.Type = DeliveryType.Direct;

            // 1s base plus 0.6s per point, so up to 4s at five.
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Stun,
                StatusEffectDuration = b.F("stun", 1f),
                DurationPerComboPoint = b.F("stunPerCombo", 0.6f)
            });

            ComboFinisher = true;
            ConfigureCost(defResource: 30f, defCooldown: 20f); // energy
        }
    }
}
