using Vintagestory.API.Common;
using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman
{
    /// <summary>Mail-wearing hybrid: totems, chain spells, weapon imbues and reactive shields.</summary>
    [RpgClassRegistration("shaman")]
    public class ShamanClass : RpgClassDef
    {
        public ShamanClass()
        {
            DisplayName = "Shaman";
            TreeNames = new[] { "Elemental", "Enhancement", "Restoration" };
            IconName = "totem-mask";

            BaseSpells = new[]
            {
                "canrpgclasses:arc_bolt", "canrpgclasses:earthen_jolt", "canrpgclasses:mending_wave",
                "canrpgclasses:static_shield", "canrpgclasses:spirit_wolf", "canrpgclasses:snaring_totem",
                "canrpgclasses:water_walking"
            };

            // Only plate counts as heavy; unlike mage/priest, chain is native gear here.
            string[] plateArmor = { "plate", "brigandine", "lamellar" };
            float mail = BalanceConfig.Affinity("shaman", "mail_attunement").F("value", 0.08f);
            float burden = BalanceConfig.Affinity("shaman", "plate_burden").F("value", 0.15f);
            float weapon = BalanceConfig.Affinity("shaman", "weapon_of_elements").F("value", 0.08f);
            string nature = StatKeys.SpellpowerFor(SpellSchool.Nature);
            string fire = StatKeys.SpellpowerFor(SpellSchool.Fire);
            string frost = StatKeys.SpellpowerFor(SpellSchool.Frost);
            GearAffinities = new()
            {
                new GearAffinity
                {
                    Key = "mail_attunement", Description = "+{0}% spell power while not in plate armor",
                    DescArgs = new object[] { (int)System.Math.Round(mail * 100f) },
                    ArmorCodeContains = plateArmor, ArmorAbsent = true,
                    Modifiers = { (nature, mail), (fire, mail), (frost, mail) }
                },
                new GearAffinity
                {
                    Key = "plate_burden", Description = "-{0}% spell power while in plate armor",
                    DescArgs = new object[] { (int)System.Math.Round(burden * 100f) },
                    ArmorCodeContains = plateArmor,
                    Modifiers = { (nature, -burden), (fire, -burden), (frost, -burden) }
                },
                new GearAffinity
                {
                    Key = "weapon_of_elements", Description = "+{0}% spell power with a mace, axe or club",
                    DescArgs = new object[] { (int)System.Math.Round(weapon * 100f) },
                    WeaponTools = new[] { EnumTool.Mace, EnumTool.Axe, EnumTool.Club },
                    Modifiers = { (nature, weapon), (fire, weapon), (frost, weapon) }
                },
            };

            // Elemental scales both nature and fire, or its fire spells fall behind as points go in.
            float elemPerPoint = BalanceConfig.Global("shamanElementalPerPoint", 0.015f);
            TreeMasteries = new()
            {
                new TreeMastery(0, nature, elemPerPoint),
                new TreeMastery(0, fire, elemPerPoint),
                new TreeMastery(1, StatKeys.MeleeWeaponsDamage, BalanceConfig.Global("shamanEnhancementPerPoint", 0.01f)),
                new TreeMastery(2, StatKeys.HealingPower, BalanceConfig.Global("shamanRestorationPerPoint", 0.01f)),
            };

            // Mail hybrid: tougher than the cloth casters and the hunter, short of the warrior.
            BaseStats = new()
            {
                (StatKeys.MaxHealthExtraPoints, BalanceConfig.Global("shamanBaseHealth", 6f)),
            };
            HpPerLevel = BalanceConfig.Global("shamanHpPerLevel", 0.85f);

            PrimaryResource = new ResourcePoolDef
            {
                Id = "mana",
                Max = 100f,
                RegenPerSec = BalanceConfig.Global("shamanManaRegenPerSec", 5f),
                StartFull = true,
                ColorR = 0.2f, ColorG = 0.75f, ColorB = 0.85f // teal
            };
            UsesComboPoints = false;
        }
    }
}
