using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    /// <summary>The cooldown is deliberately shorter than the invisibility, so out of combat you can re-stealth
    /// seamlessly; attacking makes EBSpellCaster rewrite it to the full lockout.</summary>
    [SpellRegistration("canrpgclasses:stealth")]
    public class StealthSpell : Spell
    {
        public StealthSpell()
        {
            var b = Balance;
            DisplayName = "Stealth";
            IconName = "hood";
            School = SpellSchool.PhysicalMelee;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            float duration = b.F("duration", 40f);
            Impacts.Add(SpellImpact.Invisibility(duration));

            // Out-of-combat approach tool - Slip Away (no gate) is the mid-fight escape.
            RequiresOutOfCombat = true;
            OpensStealthWindowSeconds = duration;

            ConfigureCost(defResource: 0f, defCooldown: 30f); // free utility opener
        }
    }
}
