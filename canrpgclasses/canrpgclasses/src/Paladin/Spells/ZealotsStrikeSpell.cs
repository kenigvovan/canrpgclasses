using canrpgclasses.Core.Spells;

namespace canrpgclasses.Paladin.Spells
{
    /// <summary>The Holy Power lands only when the empowered hit connects inside the window, so it stays a melee
    /// reward rather than a free ranged nuke.</summary>
    [SpellRegistration("canrpgclasses:zealots_strike")]
    public class ZealotsStrikeSpell : Spell
    {
        public ZealotsStrikeSpell()
        {
            var b = Balance;
            DisplayName = "Zealot's Strike";
            IconName = "shining-sword";
            School = SpellSchool.Holy;
            Tier = 1;

            Target.Type = TargetType.Caster; // self-buff: empowers the next melee hit instead of dealing damage now
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.EmpowerNextMelee,
                DamageSpellPowerCoefficient = b.F("empowerCoeff", 2f),
                Knockback = b.F("knockback", 0.2f),
                EmpowerWindowSeconds = b.F("empowerWindow", 6f), // land a melee hit inside the window, or lose it
                Particles = ParticleSpec.ForSchool(SpellSchool.Holy)
            });

            ComboBuilder = true; // Holy Power is granted on the empowered hit, see DidAttack
            ConfigureCost(defResource: 5f, defCooldown: 4f); // cheap, but the filler still draws on the pool
        }
    }
}
