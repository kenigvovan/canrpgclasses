using Vintagestory.API.Common;
using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Resources;

namespace canrpgclasses.Paladin
{
    [RpgClassRegistration("paladin")]
    public class PaladinClass : RpgClassDef
    {
        public PaladinClass()
        {
            DisplayName = "Paladin";
            TreeNames = new[] { "Holy", "Protection", "Retribution" };
            IconName = "holy-grail";

            // Available from level 1; the rest of the kit is unlocked by talents. The three Auras are the class's
            // signature level-1 toggles (like the warrior's Stances / hunter's pet - one active at a time), so the
            // paladin has something to manage and a builder->spender loop is fleshed out from the very start.
            BaseSpells = new[]
            {
                "canrpgclasses:zealots_strike", "canrpgclasses:blessed_light",
                "canrpgclasses:aura_of_faith", "canrpgclasses:zealots_aura", "canrpgclasses:vengeful_aura"
            };

            string[] heavyArmor = { "plate", "chain", "scale", "brigandine", "lamellar" };
            float righteousArms = BalanceConfig.Affinity("paladin", "righteous_arms").F("value", 0.15f);
            float plateTraining = BalanceConfig.Affinity("paladin", "plate_training").F("value", 4f);
            float exposed = BalanceConfig.Affinity("paladin", "exposed").F("value", 0.08f);
            GearAffinities = new()
            {
                new GearAffinity
                {
                    Key = "righteous_arms", Description = "+{0}% melee damage with a mace or hammer",
                    DescArgs = new object[] { (int)System.Math.Round(righteousArms * 100f) },
                    WeaponTools = new[] { EnumTool.Mace, EnumTool.Hammer, EnumTool.Warhammer, EnumTool.Poleaxe },
                    Modifiers = { (StatKeys.MeleeWeaponsDamage, righteousArms) }
                },
                new GearAffinity
                {
                    Key = "plate_training", Description = "+{0} max health while wearing heavy armor",
                    DescArgs = new object[] { (int)System.Math.Round(plateTraining) },
                    ArmorCodeContains = heavyArmor,
                    Modifiers = { (StatKeys.MaxHealthExtraPoints, plateTraining) }
                },
                new GearAffinity
                {
                    Key = "exposed", Description = "-{0}% healing power while not in heavy armor",
                    DescArgs = new object[] { (int)System.Math.Round(exposed * 100f) },
                    ArmorCodeContains = heavyArmor, ArmorAbsent = true,
                    Modifiers = { (StatKeys.SpellpowerHoly, -exposed) }
                },
            };

            TreeMasteries = new()
            {
                new TreeMastery(0, StatKeys.HealingPower, BalanceConfig.Global("paladinHolyHealPerPoint", 0.02f)),
                new TreeMastery(2, StatKeys.SpellpowerHoly,      BalanceConfig.Global("paladinRetHolyDmgPerPoint", 0.015f)),
                new TreeMastery(1, StatKeys.MaxHealthExtraPoints, BalanceConfig.Global("paladinProtHpPerPoint", 0.5f)),
            };

            // Intrinsic durability: a holy warrior starts noticeably tankier than the squishy classes.
            BaseStats = new()
            {
                (StatKeys.MaxHealthExtraPoints, BalanceConfig.Global("paladinBaseHealth", 10f)),
            };
            HpPerLevel = BalanceConfig.Global("paladinHpPerLevel", 1f);

            PrimaryResource = new ResourcePoolDef
            {
                Id = "mana",
                Max = 100f,
                RegenPerSec = BalanceConfig.Global("paladinManaRegenPerSec", 5f),   // slow regen (mana, not energy)
                StartFull = true,
                ColorR = 0.35f, ColorG = 0.55f, ColorB = 0.95f // sky-blue
            };
            UsesComboPoints = true; // = Holy Power (builders accumulate, finishers spend)
        }
    }
}
