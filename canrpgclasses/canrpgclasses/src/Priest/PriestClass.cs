using Vintagestory.API.Common;
using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Priest
{
    [RpgClassRegistration("priest")]
    public class PriestClass : RpgClassDef
    {
        public PriestClass()
        {
            DisplayName = "Priest";
            TreeNames = new[] { "Discipline", "Holy", "Shadow" };
            IconName = "ankh";

            // Available from level 1; the rest of the kit is unlocked by talents. Warding Word is the
            // class-defining level-1 tool (an absorb, unlike any other class's opener).
            BaseSpells = new[] { "canrpgclasses:piercing_light", "canrpgclasses:warding_word", "canrpgclasses:soothing_prayer" };

            string[] heavyArmor = { "plate", "chain", "scale", "brigandine", "lamellar" };
            float vestments = BalanceConfig.Affinity("priest", "sacred_vestments").F("value", 0.10f);
            float burdened = BalanceConfig.Affinity("priest", "burdened_faith").F("value", 0.15f);
            float staff = BalanceConfig.Affinity("priest", "staff_focus").F("value", 0.08f);
            string shadow = StatKeys.SpellpowerFor(SpellSchool.Shadow);
            GearAffinities = new()
            {
                new GearAffinity
                {
                    Key = "sacred_vestments", Description = "+{0}% spell power while not in heavy armor",
                    DescArgs = new object[] { (int)System.Math.Round(vestments * 100f) },
                    ArmorCodeContains = heavyArmor, ArmorAbsent = true,
                    Modifiers = { (StatKeys.SpellpowerHoly, vestments), (shadow, vestments) }
                },
                new GearAffinity
                {
                    Key = "burdened_faith", Description = "-{0}% spell power while in heavy armor",
                    DescArgs = new object[] { (int)System.Math.Round(burdened * 100f) },
                    ArmorCodeContains = heavyArmor,
                    Modifiers = { (StatKeys.SpellpowerHoly, -burdened), (shadow, -burdened) }
                },
                new GearAffinity
                {
                    Key = "staff_focus", Description = "+{0}% healing power with a staff or club",
                    DescArgs = new object[] { (int)System.Math.Round(staff * 100f) },
                    WeaponTools = new[] { EnumTool.Staff, EnumTool.Club },
                    Modifiers = { (StatKeys.HealingPower, staff) }
                },
            };

            // Discipline scales holy spell power because absorb shields scale off it too.
            TreeMasteries = new()
            {
                new TreeMastery(0, StatKeys.SpellpowerHoly, BalanceConfig.Global("priestDiscHolyPerPoint", 0.01f)),
                new TreeMastery(1, StatKeys.HealingPower,   BalanceConfig.Global("priestHolyHealPerPoint", 0.02f)),
                new TreeMastery(2, shadow,                  BalanceConfig.Global("priestShadowDmgPerPoint", 0.015f)),
            };

            // The frailest class: below the hunter (+5, 0.8/lvl); a caster who dies fast if caught.
            BaseStats = new()
            {
                (StatKeys.MaxHealthExtraPoints, BalanceConfig.Global("priestBaseHealth", 4f)),
            };
            HpPerLevel = BalanceConfig.Global("priestHpPerLevel", 0.75f);

            PrimaryResource = new ResourcePoolDef
            {
                Id = "mana",
                Max = 100f,
                RegenPerSec = BalanceConfig.Global("priestManaRegenPerSec", 5f),   // slow regen (mana)
                StartFull = true,
                ColorR = 0.95f, ColorG = 0.9f, ColorB = 0.55f // pale gold (distinct from the paladin's sky-blue mana)
            };
            UsesComboPoints = false; // no Holy Power / combo mechanic - the priest plays off cooldowns and mana
        }
    }
}
