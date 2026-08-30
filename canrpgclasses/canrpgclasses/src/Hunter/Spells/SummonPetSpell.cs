using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    /// <summary>Channeled, not instant, so a mid-fight revive is interruptible; on the pet's death the summon
    /// cooldown is bumped to petDeathLockoutSeconds. The Spawn handler lives in HunterPetSystem.</summary>
    [SpellRegistration("canrpgclasses:summon_pet")]
    public class SummonPetSpell : Spell
    {
        public SummonPetSpell()
        {
            var b = Balance;
            DisplayName = "Summon Wolf";
            IconName = "lion";
            School = SpellSchool.Nature;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 2.5f); // no instant re-pop

            Impacts.Add(new SpellImpact { Action = ImpactAction.Spawn });

            ConfigureCost(defResource: 0f, defCooldown: 5f);
        }
    }
}
