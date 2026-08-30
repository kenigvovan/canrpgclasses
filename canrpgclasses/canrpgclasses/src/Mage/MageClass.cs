using Vintagestory.API.Common;
using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage
{
    [RpgClassRegistration("mage")]
    public class MageClass : RpgClassDef
    {
        public MageClass()
        {
            DisplayName = "Mage";
            TreeNames = new[] { "Fire", "Frost", "Arcane" };
            IconName = "magic-palm";

            // Available from level 1; the rest of the kit is unlocked by talents. Ice Bolt is the starter nuke,
            // Flicker the escape, and the three Mage Armors are the class's signature level-1 toggles (like warrior
            // Stances / priest Shadow Guise - one active at a time).
            BaseSpells = new[]
            {
                "canrpgclasses:ice_bolt", "canrpgclasses:flicker",
                "canrpgclasses:mystic_guard", "canrpgclasses:frost_guard", "canrpgclasses:molten_guard"
            };

            string[] heavyArmor = { "plate", "chain", "scale", "brigandine", "lamellar" };
            float robes = BalanceConfig.Affinity("mage", "arcane_robes").F("value", 0.10f);
            float encumbered = BalanceConfig.Affinity("mage", "encumbered_arcana").F("value", 0.15f);
            float staff = BalanceConfig.Affinity("mage", "staff_channeling").F("value", 0.08f);
            string fire = StatKeys.SpellpowerFor(SpellSchool.Fire);
            string frost = StatKeys.SpellpowerFor(SpellSchool.Frost);
            string arcane = StatKeys.SpellpowerFor(SpellSchool.Arcane);
            GearAffinities = new()
            {
                new GearAffinity
                {
                    Key = "arcane_robes", Description = "+{0}% spell power while not in heavy armor",
                    DescArgs = new object[] { (int)System.Math.Round(robes * 100f) },
                    ArmorCodeContains = heavyArmor, ArmorAbsent = true,
                    Modifiers = { (fire, robes), (frost, robes), (arcane, robes) }
                },
                new GearAffinity
                {
                    Key = "encumbered_arcana", Description = "-{0}% spell power while in heavy armor",
                    DescArgs = new object[] { (int)System.Math.Round(encumbered * 100f) },
                    ArmorCodeContains = heavyArmor,
                    Modifiers = { (fire, -encumbered), (frost, -encumbered), (arcane, -encumbered) }
                },
                // Staff/club/mace stand in for a wand - EnumTool has no Wand.
                new GearAffinity
                {
                    Key = "staff_channeling", Description = "+{0}% spell power with a staff, club or mace",
                    DescArgs = new object[] { (int)System.Math.Round(staff * 100f) },
                    WeaponTools = new[] { EnumTool.Staff, EnumTool.Club, EnumTool.Mace },
                    Modifiers = { (fire, staff), (frost, staff), (arcane, staff) }
                },
            };

            TreeMasteries = new()
            {
                new TreeMastery(0, fire,   BalanceConfig.Global("mageFireDmgPerPoint", 0.015f)),
                new TreeMastery(1, frost,  BalanceConfig.Global("mageFrostDmgPerPoint", 0.015f)),
                new TreeMastery(2, arcane, BalanceConfig.Global("mageArcaneDmgPerPoint", 0.015f)),
            };

            // The frailest class, tied with the priest: a caster who dies fast if caught.
            BaseStats = new()
            {
                (StatKeys.MaxHealthExtraPoints, BalanceConfig.Global("mageBaseHealth", 4f)),
            };
            HpPerLevel = BalanceConfig.Global("mageHpPerLevel", 0.75f);

            PrimaryResource = new ResourcePoolDef
            {
                Id = "mana",
                Max = 100f,
                RegenPerSec = BalanceConfig.Global("mageManaRegenPerSec", 5f),
                StartFull = true,
                ColorR = 0.65f, ColorG = 0.35f, ColorB = 0.95f // vivid violet (distinct from priest gold / paladin blue)
            };
            UsesComboPoints = false;
        }
    }
}
