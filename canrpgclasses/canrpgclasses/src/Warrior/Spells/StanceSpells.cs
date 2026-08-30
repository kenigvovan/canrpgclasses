using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior.Spells
{
    /// <summary>The three warrior stances - one active at a time, casting a different one swaps them. Switching
    /// does not reset rage (v1). See <see cref="StanceEffects"/>.</summary>
    public abstract class StanceSpell : Spell
    {
        protected StanceSpell(string effectId, string icon, string display)
        {
            var b = Balance;
            DisplayName = display;
            IconName = icon;
            School = SpellSchool.PhysicalMelee;
            Tier = 1;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.ToggleAura,
                StatusEffectId = effectId,
                AuraSelfOnly = true,   // a personal combat mode, never a party aura
                AuraUpkeepPerSecond = 0f
            });
            // Amount comes from the stanceSwitchRage global via WarriorRage, not from this impact.
            Impacts.Add(new SpellImpact { Action = ImpactAction.GainResource });

            // Shared cooldown group so switching stances can't be spammed frame-by-frame.
            Cost.Cooldown.Group = "canrpgclasses:warrior_stance";
            ConfigureCost(defResource: 0f, defCooldown: 1.5f);
        }
    }

    [SpellRegistration("canrpgclasses:offensive_stance")]
    public class OffensiveStanceSpell : StanceSpell
    {
        public OffensiveStanceSpell() : base(StanceEffectIds.Battle, "battle-gear", "Offensive Stance")
        {
            DescArgs = new object[] { (int)System.Math.Round(Balance.F("rageGen", 0.25f) * 100f) };
        }
    }

    [SpellRegistration("canrpgclasses:guarded_stance")]
    public class GuardedStanceSpell : StanceSpell
    {
        public GuardedStanceSpell() : base(StanceEffectIds.Defense, "cross-shield", "Guarded Stance")
        {
            DescArgs = new object[]
            {
                (int)System.Math.Round(Balance.F("damagePenalty", 0.10f) * 100f),
                (int)System.Math.Round(Balance.F("damageReduction", 0.10f) * 100f)
            };
        }
    }

    [SpellRegistration("canrpgclasses:reckless_stance")]
    public class RecklessStanceSpell : StanceSpell
    {
        public RecklessStanceSpell() : base(StanceEffectIds.Berserk, "enrage", "Reckless Stance")
        {
            DescArgs = new object[]
            {
                (int)System.Math.Round(Balance.F("damageBonus", 0.15f) * 100f),
                (int)System.Math.Round(Balance.F("takenBonus", 0.10f) * 100f)
            };
        }
    }
}
