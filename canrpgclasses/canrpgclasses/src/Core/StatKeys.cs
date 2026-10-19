using canrpgclasses.Core.Spells;

namespace canrpgclasses.Core
{
    /// <summary>Shared EntityStats keys. Unless noted, 1.0-based multipliers (unset blends to 1.0).</summary>
    public static class StatKeys
    {
        /// <summary>Combo-finisher damage; the default <see cref="canrpgclasses.Core.Spells.Spell.DamageMultiplierStat"/> for finishers.</summary>
        public const string FinisherDamage = "finisherDamage";

        /// <summary>Sentence's damage only (SentenceSpell's <c>DamageMultiplierStat</c>).</summary>
        public const string JudgementDamage = "judgementDamage";

        /// <summary>All healing done by this caster.</summary>
        public const string HealingPower = "healingPower";

        /// <summary>Global cooldown reduction, read as a 0-based fraction and capped together with the per-spell
        /// stat by <c>cooldownReductionCap</c>.</summary>
        public const string CooldownReduction = "cooldownReduction";

        /// <summary>Per-spell cooldown reduction stat (e.g. Practiced Regroup), 1.0-based.</summary>
        public static string CooldownReductionFor(string spellLocalId) => "cooldownReduction_" + spellLocalId;

        /// <summary>Per-spell cast-time reduction, capped by <c>castReductionCap</c>.</summary>
        public static string CastReductionFor(string spellLocalId) => "castReduction_" + spellLocalId;

        /// <summary>Per-school spell power, folded into <see cref="canrpgclasses.Core.EB.EBRpgStats.GetSpellPower"/>.</summary>
        public static string SpellpowerFor(SpellSchool school) => "spellpower_" + school.ToString().ToLowerInvariant();

        public static readonly string SpellpowerHoly = SpellpowerFor(SpellSchool.Holy);

        /// <summary>Incoming-damage reduction, capped by <c>damageReductionCap</c>.</summary>
        public const string DamageReduction = "canrpgDamageReduction";

        /// <summary>Resist to all magic schools, added to the per-school <c>canrpgResist_&lt;school&gt;</c> and capped
        /// together by magicResistCap. Read 0-based; physical hits skip it.</summary>
        public const string MagicResist = "canrpgResist_magic";

        /// <summary>Attacker's magic penetration, added to per-school <c>canrpgPen_&lt;school&gt;</c> and subtracted from
        /// the victim's resist before the cap. Never pushes resist below 0.</summary>
        public const string MagicPen = "canrpgPen_magic";

        /// <summary>Aura upkeep reduction, capped by <c>auraUpkeepReductionCap</c> so an aura is never free.</summary>
        public const string AuraUpkeepReduction = "auraUpkeepReduction";

        /// <summary>Boost to the caster's Aura of Faith regen, read 0-based in EBAuras.</summary>
        public const string AuraRegenStrength = "auraRegenStrength";

        /// <summary>Aura of Refuge DR fraction on the aura caster, read 0-based and baked into every applied aura effect.</summary>
        public const string AuraSanctuaryDr = "auraSanctuaryDr";

        /// <summary>Melee reflect fraction (as Holy) on the aura caster, baked into the applied Might effect. Read 0-based.</summary>
        public const string AuraReflectFraction = "auraReflect";

        /// <summary>Primary resource regen, folded into the tick by <see cref="canrpgclasses.Core.Resources.EBResources"/>.</summary>
        public const string ResourceRegen = "resourceRegen";

        // ---- Vanilla engine stats. Spelled as the game spells them - "healingeffectivness" is missing an 'e'. ----
        public const string WalkSpeed = "walkspeed";
        public const string HungerRate = "hungerrate";
        public const string HealingEffectiveness = "healingeffectivness";
        public const string MeleeWeaponsDamage = "meleeWeaponsDamage";
        /// <summary>Bow arrows scale off this, not meleeWeaponsDamage (EntityProjectileBase, ItemSling).</summary>
        public const string RangedWeaponsDamage = "rangedWeaponsDamage";
        public const string MaxHealthExtraPoints = "maxhealthExtraPoints";
        public const string AnimalSeekingRange = "animalSeekingRange";

        /// <summary>Bow draw speed; vanilla draw timing is hardcoded in ItemBow, so only aimed_shot's full-draw check reads it.</summary>
        public const string BowDrawSpeed = "bowDrawSpeed";

        /// <summary>Trap burst damage and slow strength, read by <see cref="canrpgclasses.Core.Execution.ZoneManager"/>.</summary>
        public const string TrapPower = "trapPower";

        /// <summary>Flag stat (>0.5 = on): damaging traps also ignite. A stat rather than a talent id keeps the core class-agnostic.</summary>
        public const string TrapIgnite = "trapIgnite";
    }
}
