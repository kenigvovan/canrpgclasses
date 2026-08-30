using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    [SpellRegistration("canrpgclasses:armor_break")]
    public class ArmorBreakSpell : Spell
    {
        public ArmorBreakSpell()
        {
            var b = Balance;
            DisplayName = "Armor Break";
            IconName = "shoulder-armor";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;
            Range = 4;

            Target.Type = TargetType.Aim;
            Deliver.Type = DeliveryType.Direct;
            int perStackPct = (int)System.Math.Round(b.F("perStack", 0.05f) * 100f);
            DescArgs = new object[] { perStackPct, perStackPct * b.I("cap", 5) };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = b.F("coeff", 0.5f),
                Knockback = 0f
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = ArmorBreakEffectId.Id,
                StatusEffectDuration = b.F("duration", 15f),
                StatusEffectAmplifier = 1,
                StatusEffectAmplifierCap = b.I("cap", 5),
                StatusEffectApplyMode = StatusApplyMode.Add
            });

            ConfigureCost(defResource: 10f, defCooldown: 4f); // rage
        }
    }
}
