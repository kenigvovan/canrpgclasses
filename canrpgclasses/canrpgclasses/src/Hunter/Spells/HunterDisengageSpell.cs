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
            Range = b.F("slowRadius", 4f); // AoE slow disc radius (zone reads the spell Range)

            // 1) Chill nearby foes at your current position (one-shot zone: no damage, just walkslow), THEN
            // 2) leap backward. Order matters so the slow lands where the enemies are, before you move.
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.DamageZone,
                DamageSpellPowerCoefficient = 0f,          // pure control, no damage
                ZoneDurationSeconds = 0.5f,                // → a single immediate pulse, no lingering
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
