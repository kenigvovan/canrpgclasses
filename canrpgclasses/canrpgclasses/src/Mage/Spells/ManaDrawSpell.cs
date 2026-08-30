using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    [SpellRegistration("canrpgclasses:mana_draw")]
    public class ManaDrawSpell : Spell
    {
        public ManaDrawSpell()
        {
            var b = Balance;
            DisplayName = "Mana Draw";
            IconName = "transportation-rings";
            School = SpellSchool.Arcane;
            Tier = 4;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            float manaGain = b.F("manaGain", 50f);
            int ticks = (int)b.F("ticks", 4f);
            float channel = b.F("channel", 3f);

            CastMode = CastMode.Channel;
            CastDuration = channel;
            ChannelTicks = ticks;

            DescArgs = new object[] { (int)manaGain, (int)channel };

            // The channel fires this impact once per tick, so the total is split across them.
            Impacts.Add(new SpellImpact { Action = ImpactAction.GainResource, ResourceGainAmount = manaGain / ticks });

            ConfigureCost(defResource: 0f, defCooldown: 90f);
        }
    }
}
