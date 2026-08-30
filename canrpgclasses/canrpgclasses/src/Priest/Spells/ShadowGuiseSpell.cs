using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    [SpellRegistration("canrpgclasses:shadow_guise")]
    public class ShadowGuiseSpell : Spell
    {
        public ShadowGuiseSpell()
        {
            var b = Balance;
            DisplayName = "Shadow Guise";
            IconName = "shadow-follower";
            School = SpellSchool.Shadow;
            Tier = 5;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            DescArgs = new object[]
            {
                (int)System.Math.Round(b.F("shadowBonus", 0.15f) * 100f),
                (int)System.Math.Round(b.F("healPenalty", 0.20f) * 100f)
            };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.ToggleAura,
                StatusEffectId = PriestEffectIds.ShadowGuise,
                AuraSelfOnly = true,   // a personal form, never a party aura
                AuraUpkeepPerSecond = 0f
            });

            ConfigureCost(defResource: 0f, defCooldown: 1.5f);
        }
    }
}
