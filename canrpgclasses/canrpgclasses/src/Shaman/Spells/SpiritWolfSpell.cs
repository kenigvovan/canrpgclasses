using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    [SpellRegistration("canrpgclasses:spirit_wolf")]
    public class SpiritWolfSpell : Spell
    {
        public SpiritWolfSpell()
        {
            var b = Balance;
            DisplayName = "Spirit Wolf";
            IconName = "lion";
            School = SpellSchool.Nature;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;
            BypassesFormLock = true; // the wolf can cast nothing else, but must be able to toggle itself off
            AuraLocksActions = true; // relog-safe: the gate derives the lock from the active aura and this flag

            DescArgs = new object[] { (int)System.Math.Round(b.F("walkSpeed", 0.30f) * 100f) };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.ToggleAura,
                StatusEffectId = ShamanEffectIds.SpiritWolf,
                AuraSelfOnly = true,      // a personal form, never a party aura
                AuraUpkeepPerSecond = 0f
            });

            ConfigureCost(defResource: 0f, defCooldown: 3f);
        }
    }
}
