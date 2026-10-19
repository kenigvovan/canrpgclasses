using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    [SpellRegistration("canrpgclasses:hunter_disengage")]
    public class HunterDisengageSpell : Spell
    {
        public HunterDisengageSpell()
        {
            var b = Balance;
            DisplayName = "Disengage";
            IconName = "acrobatic";
            School = SpellSchool.PhysicalRanged;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            Range = b.F("slowRadius", 4f); // the zone reads its radius from Range

            // Slow first, then leap, so the slow lands where the enemies are.
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.DamageZone,
                DamageSpellPowerCoefficient = 0f,
                ZoneDurationSeconds = 0.5f,                // a single pulse, no lingering
                ZoneStatusEffectId = "walkslow",
                ZoneStatusTier = b.I("slowAmp", 2),
                ZoneStatusSeconds = b.F("slowDuration", 3f)
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Dash,
                DashReverse = true
            });

            ConfigureCost(defResource: 25f, defCooldown: 14f); // focus
        }
    }
}
