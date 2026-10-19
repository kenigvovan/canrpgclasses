using Vintagestory.API.Common;
using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter
{
    [RpgClassRegistration("hunter")]
    public class HunterClass : RpgClassDef
    {
        public HunterClass()
        {
            DisplayName = "Hunter";
            TreeNames = new[] { "Marksmanship", "Survival", "Beast Mastery" };
            IconName = "bowman";

            // Available from level 1; the wolf and its orders are class-defining, so they need no talent.
            BaseSpells = new[]
            {
                "canrpgclasses:summon_pet", "canrpgclasses:measured_shot", "canrpgclasses:finishing_shot",
                "canrpgclasses:pet_aggressive", "canrpgclasses:pet_defensive", "canrpgclasses:pet_passive",
                "canrpgclasses:pet_attack", "canrpgclasses:pet_come", "canrpgclasses:pet_stay",
                "canrpgclasses:pet_dismiss"
            };

            string[] heavyArmor = { "plate", "chain", "scale", "brigandine", "lamellar" };
            float bowMastery = BalanceConfig.Affinity("hunter", "bow_mastery").F("value", 0.20f);
            float heavyWeapon = BalanceConfig.Affinity("hunter", "heavy_weapon").F("value", 0.25f);
            float lightArmor = BalanceConfig.Affinity("hunter", "light_armor").F("value", 0.06f);
            float heavyArmorPen = BalanceConfig.Affinity("hunter", "heavy_armor").F("value", 0.12f);
            GearAffinities = new()
            {
                // Arrows read rangedWeaponsDamage, not meleeWeaponsDamage.
                new GearAffinity
                {
                    Key = "bow_mastery", Description = "+{0}% damage with a bow",
                    DescArgs = new object[] { (int)System.Math.Round(bowMastery * 100f) },
                    WeaponTools = new[] { EnumTool.Bow },
                    Modifiers = { (StatKeys.RangedWeaponsDamage, bowMastery) }
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

            // No Beast Mastery entry: it scales the pet, which TreeMasteries (player stats only) can't reach.
            TreeMasteries = new()
            {
                new TreeMastery(0, StatKeys.SpellpowerFor(SpellSchool.PhysicalRanged), BalanceConfig.Global("hunterMarksRangedPerPoint", 0.015f)),
                new TreeMastery(1, StatKeys.WalkSpeed, BalanceConfig.Global("hunterSurvivalSpeedPerPoint", 0.003f)),
            };

            // Between the squishy rogue (+3, 0.7/lvl) and the plate paladin (+10, 1/lvl).
            BaseStats = new()
            {
                (StatKeys.MaxHealthExtraPoints, BalanceConfig.Global("hunterBaseHealth", 5f)),
            };
            HpPerLevel = BalanceConfig.Global("hunterHpPerLevel", 0.8f);

            PrimaryResource = new ResourcePoolDef
            {
                Id = "focus",
                Max = 100f,
                // Between energy (12) and mana (5); low enough that shot-spam actually drains focus.
                RegenPerSec = BalanceConfig.Global("hunterFocusRegenPerSec", 6f),
                StartFull = true,
                ColorR = 0.55f, ColorG = 0.85f, ColorB = 0.35f // hunter green
            };
            UsesComboPoints = true;
        }
    }
}
