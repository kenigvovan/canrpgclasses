using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    /// <summary>Brings a dead wolf back at partial health and lifts the summon lockout. Castable only while that
    /// lockout runs (HunterPetSystem's Spawn precheck).</summary>
    [SpellRegistration("canrpgclasses:revive_pet")]
    public class RevivePetSpell : Spell
    {
        public RevivePetSpell()
        {
            var b = Balance;
            DisplayName = "Revive Pet";
            IconName = "ankh";
            School = SpellSchool.Nature;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 5f);

            DescArgs = new object[] { (int)System.Math.Round(b.F("healthFraction", 0.5f) * 100f) };
            Impacts.Add(new SpellImpact { Action = ImpactAction.Spawn, PetCommand = "revive" });

            ConfigureCost(defResource: 30f, defCooldown: 60f); // focus
        }
    }
}
