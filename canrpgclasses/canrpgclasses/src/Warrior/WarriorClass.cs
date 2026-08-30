using Vintagestory.API.Common;
using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Warrior
{
    /// <summary>Front-line melee bruiser and tank. Rage starts empty with no passive regen - built from hits dealt
    /// and taken, decayed out of combat (see <see cref="WarriorRage"/>).</summary>
    [RpgClassRegistration("warrior")]
    public class WarriorClass : RpgClassDef
    {
        public WarriorClass()
        {
            DisplayName = "Warrior";
            TreeNames = new[] { "Arms", "Fury", "Protection" };
            IconName = "mailed-fist";

            // Stances are the class-defining feature, so they need no talent; the rest of the kit is talent-gated.
            BaseSpells = new[]
            {
                "canrpgclasses:offensive_stance", "canrpgclasses:guarded_stance", "canrpgclasses:reckless_stance",
                "canrpgclasses:brutal_strike", "canrpgclasses:leg_slash"
            };

            string[] heavyArmor = { "plate", "chain", "scale", "brigandine", "lamellar" };
            float bladeMastery = BalanceConfig.Affinity("warrior", "blade_mastery").F("value", 0.15f);
            float armoredBulwark = BalanceConfig.Affinity("warrior", "armored_bulwark").F("value", 0.06f);
            float clumsyArcher = BalanceConfig.Affinity("warrior", "clumsy_archer").F("value", 0.25f);
            GearAffinities = new()
            {
                new GearAffinity
                {
                    Key = "blade_mastery", Description = "+{0}% melee damage with a sword or axe",
                    DescArgs = new object[] { (int)System.Math.Round(bladeMastery * 100f) },
                    WeaponTools = new[] { EnumTool.Sword, EnumTool.Axe },
                    Modifiers = { (StatKeys.MeleeWeaponsDamage, bladeMastery) }
                },
                // The only class rewarded for heavy armor rather than burdened by it.
                new GearAffinity
                {
                    Key = "armored_bulwark", Description = "+{0}% damage reduction in heavy armor",
                    DescArgs = new object[] { (int)System.Math.Round(armoredBulwark * 100f) },
                    ArmorCodeContains = heavyArmor,
                    Modifiers = { (StatKeys.DamageReduction, armoredBulwark) }
                },
                new GearAffinity
                {
                    Key = "clumsy_archer", Description = "-{0}% ranged damage with a bow",
                    DescArgs = new object[] { (int)System.Math.Round(clumsyArcher * 100f) },
                    WeaponTools = new[] { EnumTool.Bow },
                    Modifiers = { (StatKeys.RangedWeaponsDamage, -clumsyArcher) }
                },
            };

            TreeMasteries = new()
            {
                new TreeMastery(0, StatKeys.MeleeWeaponsDamage, BalanceConfig.Global("warriorArmsMeleePerPoint", 0.01f)),
                new TreeMastery(1, WarriorStatKeys.RageGeneration, BalanceConfig.Global("warriorFuryRagePerPoint", 0.01f)),
                new TreeMastery(2, StatKeys.DamageReduction, BalanceConfig.Global("warriorProtDrPerPoint", 0.004f)),
            };

            // The toughest class: between the hunter (+5, 0.8/lvl) and above the paladin's floor.
            BaseStats = new()
            {
                (StatKeys.MaxHealthExtraPoints, BalanceConfig.Global("warriorBaseHealth", 8f)),
            };
            HpPerLevel = BalanceConfig.Global("warriorHpPerLevel", 0.9f);

            PrimaryResource = new ResourcePoolDef
            {
                Id = "rage",
                Max = 100f,
                RegenPerSec = 0f,   // rage never regenerates on its own - it is built/decayed by WarriorRage
                StartFull = false,  // starts empty, filled by combat
                ColorR = 0.85f, ColorG = 0.15f, ColorB = 0.15f // rage red
            };
            UsesComboPoints = false;
        }
    }
}
