using canrpgclasses.Core.Spells;

namespace canrpgclasses.Core
{
    /// <summary>
    /// EntityStats keys shared across the codebase: set by talents/class masteries, read by the spell
    /// pipeline (<see cref="canrpgclasses.Core.Execution.SpellExecutor"/>, <see cref="canrpgclasses.Core.EB.EBSpellCaster"/>)
    /// and the client tooltip. Unless noted otherwise they are 1.0-based multipliers; unset blends to 1.0.
    /// </summary>
    public static class StatKeys
    {
        /// <summary>1.0-based multiplier on combo-finisher damage (Deadly Precision, Sacred Purpose). The default
        /// <see cref="canrpgclasses.Core.Spells.Spell.DamageMultiplierStat"/> for finishers.</summary>
        public const string FinisherDamage = "finisherDamage";

        /// <summary>Multiplier on Sentence's damage alone, declared by SentenceSpell as its
        /// <c>DamageMultiplierStat</c>.</summary>
        public const string JudgementDamage = "judgementDamage";

        /// <summary>1.0-based multiplier on all healing DONE by this caster (holy tree mastery).</summary>
        public const string HealingPower = "healingPower";

        /// <summary>Global cooldown reduction, read as a 0-based fraction and capped together with the per-spell
        /// stat by <c>cooldownReductionCap</c>.</summary>
        public const string CooldownReduction = "cooldownReduction";

        /// <summary>Per-spell cooldown reduction stat (e.g. Practiced Regroup), 1.0-based.</summary>
        public static string CooldownReductionFor(string spellLocalId) => "cooldownReduction_" + spellLocalId;

        /// <summary>Per-spell cast-time reduction, capped by <c>castReductionCap</c>.</summary>
        public static string CastReductionFor(string spellLocalId) => "castReduction_" + spellLocalId;

        /// <summary>Per-school spell-power multiplier (talents/masteries), folded into
        /// <see cref="canrpgclasses.Core.EB.EBRpgStats.GetSpellPower"/> like a vanilla 1.0-based stat.</summary>
        public static string SpellpowerFor(SpellSchool school) => "spellpower_" + school.ToString().ToLowerInvariant();

        /// <summary>Precomputed <see cref="SpellpowerFor"/>(Holy) - referenced throughout the paladin content.</summary>
        public static readonly string SpellpowerHoly = SpellpowerFor(SpellSchool.Holy);

        /// <summary>Incoming-damage reduction, capped by <c>damageReductionCap</c>.</summary>
        public const string DamageReduction = "canrpgDamageReduction";

        /// <summary>1.0-based flat resistance to all magic schools (worn metal armor, resist talents/gems). Read
        /// 0-based in StunPatches and ADDED to the hit's per-school <c>canrpgResist_&lt;school&gt;</c>, then capped
        /// together (magicResistCap). Only magical (non-physical) schools take this step. Default (unset) = 0.</summary>
        public const string MagicResist = "canrpgResist_magic";

        /// <summary>1.0-based flat magic PENETRATION on the ATTACKER (resist talents/gems). Read 0-based in StunPatches
        /// and ADDED to the hit's per-school <c>canrpgPen_&lt;school&gt;</c>, then SUBTRACTED from the victim's total
        /// magic resist before the cap. Only magical schools. Damage is never increased past 0 resist. Unset = 0.</summary>
        public const string MagicPen = "canrpgPen_magic";

        /// <summary>Aura upkeep reduction, capped by <c>auraUpkeepReductionCap</c> so an aura is never free.</summary>
        public const string AuraUpkeepReduction = "auraUpkeepReduction";

        /// <summary>1.0-based boost to the caster's Aura of Faith regen (Zealous Faith); read 0-based in EBAuras,
        /// which scales the applied AuraRegenEffect's per-tick heal by it. Default (unset) = no change.</summary>
        public const string AuraRegenStrength = "auraRegenStrength";

        /// <summary>1.0-based "Aura of Refuge" DR fraction on the AURA caster; read 0-based in EBAuras and baked
        /// into every applied aura effect, so everyone under the caster's aura takes less damage. Unset = 0.</summary>
        public const string AuraSanctuaryDr = "auraSanctuaryDr";

        /// <summary>1.0-based "Retribution Vengeance" reflect fraction on the AURA caster; read 0-based in EBAuras and
        /// baked into the applied Might (Vengeful Aura) effect - bearers reflect that share of melee hits as Holy.</summary>
        public const string AuraReflectFraction = "auraReflect";

        /// <summary>1.0-based multiplier on the class primary resource's regen-per-second (priest Meditation).
        /// Class-agnostic: <see cref="canrpgclasses.Core.Resources.EBResources"/> folds it into the regen tick, so
        /// any class can gain a "faster resource regen" talent. Unset blends to 1.0 (no change).</summary>
        public const string ResourceRegen = "resourceRegen";

        // ---- Vanilla engine stats. Spelled as the game spells them - "healingeffectivness" is missing an 'e'. ----
        public const string WalkSpeed = "walkspeed";
        public const string HungerRate = "hungerrate";
        public const string HealingEffectiveness = "healingeffectivness";
        public const string MeleeWeaponsDamage = "meleeWeaponsDamage";
        /// <summary>Vanilla ranged-weapon damage multiplier - bow arrows scale off this (not meleeWeaponsDamage):
        /// see EntityProjectileBase (damage *= rangedWeaponsDamage) and ItemSling. The hunter's bow affinity /
        /// bow talents set this so they actually buff the bow.</summary>
        public const string RangedWeaponsDamage = "rangedWeaponsDamage";
        public const string MaxHealthExtraPoints = "maxhealthExtraPoints";
        public const string AnimalSeekingRange = "animalSeekingRange";

        /// <summary>Our own 1.0-based "bow draw speed" multiplier (no vanilla equivalent - vanilla draw timing is
        /// hardcoded in ItemBow). Unused until a talent sets it; read by aimed_shot's full-draw check so its
        /// required hold scales with a faster draw. Default (never set) blends to 1.0 → no effect.</summary>
        public const string BowDrawSpeed = "bowDrawSpeed";

        /// <summary>1.0-based multiplier on the hunter's trap burst (damage + slow strength), set by the Trap
        /// Mastery talent and read by <see cref="canrpgclasses.Core.Execution.ZoneManager"/> when a trap fires.</summary>
        public const string TrapPower = "trapPower";

        /// <summary>Flag stat (>0.5 = on): a damaging trap also sets the enemies it catches on fire. Set by the
        /// hunter's Napalm Traps talent, read generically by <see cref="canrpgclasses.Core.Execution.ZoneManager"/>
        /// when a trap fires. A stat (not a talent id) so the core stays class-agnostic.</summary>
        public const string TrapIgnite = "trapIgnite";
    }
}
