using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest.Spells
{
    /// <summary>A long cast that offers a party member a portal to the priest (see <c>SummonRitual</c>).</summary>
    [SpellRegistration("canrpgclasses:summon_ritual")]
    public class SummonRitualSpell : Spell
    {
        public SummonRitualSpell()
        {
            var b = Balance;
            DisplayName = "Summon Ritual";
            IconName = "beams-aura";
            School = SpellSchool.Holy;
            PicksPartyMember = true;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 8f);

            float duration = b.F("duration", 20f);
            DescArgs = new object[] { (int)CastDuration, (int)duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.SummonAlly,
                StatusEffectDuration = duration
            });

            ConfigureCost(defResource: 40f, defCooldown: 300f);
        }
    }
}
