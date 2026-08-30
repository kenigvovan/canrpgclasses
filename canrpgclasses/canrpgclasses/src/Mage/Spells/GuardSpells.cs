using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    /// <summary>Only one guard can be up: casting one clears whichever was active, like warrior stances.</summary>
    public abstract class MageGuardBase : Spell
    {
        protected MageGuardBase(string effectId, string icon, string display)
        {
            DisplayName = display;
            IconName = icon;
            School = SpellSchool.Arcane;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.ToggleAura,
                StatusEffectId = effectId,
                AuraSelfOnly = true,      // a personal posture, never a party aura
                AuraUpkeepPerSecond = 0f
            });

            Cost.Cooldown.Group = "canrpgclasses:mystic_guard"; // shared, to stop flickering
            ConfigureCost(defResource: 0f, defCooldown: 1.5f);
        }
    }

    [SpellRegistration("canrpgclasses:mystic_guard")]
    public class MysticGuardSpell : MageGuardBase
    {
        public MysticGuardSpell() : base(MageEffectIds.MysticGuard, "book-aura", "Mystic Guard")
            => DescArgs = new object[] { (int)System.Math.Round(Balance.F("resourceRegen", 0.30f) * 100f) };
    }

    [SpellRegistration("canrpgclasses:frost_guard")]
    public class FrostGuardSpell : MageGuardBase
    {
        public FrostGuardSpell() : base(MageEffectIds.FrostGuard, "icebergs", "Frost Guard")
            => DescArgs = new object[] { (int)System.Math.Round(Balance.F("damageReduction", 0.10f) * 100f) };
    }

    [SpellRegistration("canrpgclasses:molten_guard")]
    public class MoltenGuardSpell : MageGuardBase
    {
        public MoltenGuardSpell() : base(MageEffectIds.MoltenGuard, "lava", "Molten Guard")
            => DescArgs = new object[] { (int)System.Math.Round(Balance.F("spellPower", 0.10f) * 100f) };
    }
}
