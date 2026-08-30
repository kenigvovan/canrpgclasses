using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    /// <summary>The dodge roll happens in StunPatches.Prefix_ReceiveDamage, and only for physical hits.</summary>
    [SpellRegistration("canrpgclasses:evasion")]
    public class EvasionSpell : Spell
    {
        public EvasionSpell()
        {
            var b = Balance;
            DisplayName = "Evasion";
            IconName = "jump-across";
            School = SpellSchool.PhysicalMelee;
            Tier = 3;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Evasion,
                EvasionChance = b.F("chance", 0.5f),
                StatusEffectDuration = b.F("duration", 6f)
            });

            ConfigureCost(defResource: 25f, defCooldown: 45f); // energy
        }
    }
}
