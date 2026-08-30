using Vintagestory.API.Common;
using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Resources;

namespace canrpgclasses.Rogues
{
    [RpgClassRegistration("rogue")]
    public class RogueClass : RpgClassDef
    {
        public RogueClass()
        {
            DisplayName = "Rogue";
            TreeNames = new[] { "Stealth", "Combat", "Utility" };
            IconName = "hooded-assassin";

            // Available from level 1; the rest of the kit is unlocked by talents.
            BaseSpells = new[] { "canrpgclasses:vicious_strike", "canrpgclasses:gut_strike", "canrpgclasses:stealth" };

            // Intrinsic gear affinities (no talent needed). The substrings below are vanilla armor
            // "construction" variants - a modded set only counts if its code contains one of them.
            string[] heavyArmor = { "plate", "chain", "scale", "brigandine", "lamellar" };
            float lightBlade = BalanceConfig.Affinity("rogue", "light_blade").F("value", 0.20f);
            float heavyWeapon = BalanceConfig.Affinity("rogue", "heavy_weapon").F("value", 0.25f);
            float lightArmor = BalanceConfig.Affinity("rogue", "light_armor").F("value", 0.08f);
            float heavyArmorPen = BalanceConfig.Affinity("rogue", "heavy_armor").F("value", 0.12f);
            GearAffinities = new()
            {
                new GearAffinity
                {
                    Key = "light_blade", Description = "+{0}% melee damage with a knife or dagger",
                    DescArgs = new object[] { (int)System.Math.Round(lightBlade * 100f) },
                    WeaponTools = new[] { EnumTool.Knife },
                    Modifiers = { (StatKeys.MeleeWeaponsDamage, lightBlade) }
                },
                new GearAffinity
                {
                    Key = "heavy_weapon", Description = "-{0}% melee damage with a heavy weapon",
                    DescArgs = new object[] { (int)System.Math.Round(heavyWeapon * 100f) },
                    WeaponTools = new[] { EnumTool.Axe, EnumTool.Hammer, EnumTool.Warhammer, EnumTool.Mace, EnumTool.Poleaxe, EnumTool.Halberd, EnumTool.Pike },
                    Modifiers = { (StatKeys.MeleeWeaponsDamage, -heavyWeapon) }
                },
                new GearAffinity
                {
                    Key = "light_armor", Description = "+{0}% movement speed while not in heavy armor",
                    DescArgs = new object[] { (int)System.Math.Round(lightArmor * 100f) },
                    ArmorCodeContains = heavyArmor, ArmorAbsent = true,
                    Modifiers = { (StatKeys.WalkSpeed, lightArmor) }
                },
                new GearAffinity
                {
                    Key = "heavy_armor", Description = "-{0}% movement speed in heavy armor",
                    DescArgs = new object[] { (int)System.Math.Round(heavyArmorPen * 100f) },
                    ArmorCodeContains = heavyArmor,
                    Modifiers = { (StatKeys.WalkSpeed, -heavyArmorPen) }
                },
            };

            TreeMasteries = new()
            {
                new TreeMastery(1, StatKeys.MeleeWeaponsDamage,   BalanceConfig.Global("rogueCombatDmgPerPoint", 0.01f)),
                new TreeMastery(0, StatKeys.WalkSpeed,            BalanceConfig.Global("rogueStealthSpeedPerPoint", 0.003f)),
                new TreeMastery(2, StatKeys.MaxHealthExtraPoints, BalanceConfig.Global("rogueUtilityHpPerPoint", 0.4f)),
            };

            // Intrinsic durability: a lightly-armoured rogue starts squishier than the plate classes.
            BaseStats = new()
            {
                (StatKeys.MaxHealthExtraPoints, BalanceConfig.Global("rogueBaseHealth", 3f)),
            };
            HpPerLevel = BalanceConfig.Global("rogueHpPerLevel", 0.7f);

            PrimaryResource = new ResourcePoolDef
            {
                Id = "energy",
                Max = 100f,
                RegenPerSec = BalanceConfig.Global("rogueEnergyRegenPerSec", 12f),
                StartFull = true,
                ColorR = 0.95f, ColorG = 0.82f, ColorB = 0.20f
            };
            UsesComboPoints = true;
        }
    }
}
