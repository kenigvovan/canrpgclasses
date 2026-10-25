using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    /// <summary>Channel on a weakened wild creature; PetTaming runs the ticks and adds it to the stable.</summary>
    [SpellRegistration("canrpgclasses:tame_beast")]
    public class TameBeastSpell : Spell
    {
        public TameBeastSpell()
        {
            var b = Balance;
            DisplayName = "Tame Beast";
            IconName = "griffin-symbol";
            School = SpellSchool.Nature;
            Tier = 1;
            Range = b.F("range", 6f);
            TriggersGlobalCooldown = false;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Channel;
            CastDuration = b.F("duration", 8f);
            ChannelTicks = (int)b.F("ticks", 8f);

            DescArgs = new object[]
            {
                (int)System.Math.Round(Core.Config.BalanceConfig.Global("tameMaxHealthFraction", 0.35f) * 100f),
                (int)CastDuration
            };
            Impacts.Add(new SpellImpact { Action = ImpactAction.TameTick }); // visuals live in PetTaming

            ConfigureCost(defResource: 40f, defCooldown: 20f); // focus
        }
    }
}
