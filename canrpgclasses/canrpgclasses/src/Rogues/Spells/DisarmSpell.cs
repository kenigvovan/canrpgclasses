using canrpgclasses.Core.Spells;

namespace canrpgclasses.Rogues.Spells
{
    /// <summary>A player's weapon moves to another hotbar slot (never to the ground); a mob instead gets a
    /// weakmelee debuff. See SpellExecutor.ApplyDisarm.</summary>
    [SpellRegistration("canrpgclasses:disarm")]
    public class DisarmSpell : Spell
    {
        public DisarmSpell()
        {
            var b = Balance;
            DisplayName = "Disarm";
            IconName = "drop-weapon";
            School = SpellSchool.PhysicalMelee;
            Tier = 2;
            Range = 4;

            Target.Type = TargetType.Aim;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Disarm,
                StatusEffectDuration = b.F("duration", 6f) // mob weakmelee duration; players just lose their grip
            });

            ConfigureCost(defResource: 35f, defCooldown: 14f);  // energy
        }
    }
}
