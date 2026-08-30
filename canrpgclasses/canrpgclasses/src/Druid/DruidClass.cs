using Vintagestory.API.Common;
using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Druid
{
    /// <summary>The Druid - a shapeshifter with real model-swap forms. Trees: Balance (caster), Feral (Cat
    /// combo DPS + Bear rage tank, see <see cref="DruidRage"/>), Restoration (mana healer).</summary>
    [RpgClassRegistration("druid")]
    public class DruidClass : RpgClassDef
    {
        public DruidClass()
        {
            DisplayName = "Druid";
            TreeNames = new[] { "Balance", "Feral", "Restoration" };
            IconName = "flat-paw-print";

            BaseSpells = new[]
            {
                "canrpgclasses:nature_bolt", "canrpgclasses:lunar_flare", "canrpgclasses:verdant_renewal",
                "canrpgclasses:feline_form", "canrpgclasses:flesh_tear", "canrpgclasses:savage_bite",
                "canrpgclasses:bark_hide"
            };

            string[] plateArmor = { "plate", "brigandine", "lamellar" };
            float leather = BalanceConfig.Affinity("druid", "wild_attunement").F("value", 0.08f);
            float burden = BalanceConfig.Affinity("druid", "plate_burden").F("value", 0.15f);
            string nature = StatKeys.SpellpowerFor(SpellSchool.Nature);
            string arcane = StatKeys.SpellpowerFor(SpellSchool.Arcane);
            GearAffinities = new()
            {
                new GearAffinity
                {
                    Key = "wild_attunement", Description = "+{0}% spell and healing power while not in plate armor",
                    DescArgs = new object[] { (int)System.Math.Round(leather * 100f) },
                    ArmorCodeContains = plateArmor, ArmorAbsent = true,
                    Modifiers = { (nature, leather), (arcane, leather), (StatKeys.HealingPower, leather) }
                },
                new GearAffinity
                {
                    Key = "plate_burden", Description = "-{0}% spell and healing power while in plate armor",
                    DescArgs = new object[] { (int)System.Math.Round(burden * 100f) },
                    ArmorCodeContains = plateArmor,
                    Modifiers = { (nature, -burden), (arcane, -burden), (StatKeys.HealingPower, -burden) }
                },
            };

            // Feral abilities are cast while the form-lock blocks real weapon swings, so they scale off spell power
            // (Nature school) rather than MeleeWeaponsDamage, which only affects actual weapon hits.
            float balancePer = BalanceConfig.Global("druidBalancePerPoint", 0.015f);
            TreeMasteries = new()
            {
                new TreeMastery(0, nature, balancePer),
                new TreeMastery(0, arcane, balancePer),
                new TreeMastery(1, nature, BalanceConfig.Global("druidFeralPerPoint", 0.015f)),
                new TreeMastery(2, StatKeys.HealingPower, BalanceConfig.Global("druidRestoPerPoint", 0.01f)),
            };

            // Leather medium: tougher than the cloth casters, short of the mail shaman (the Bear form adds its own armor).
            BaseStats = new()
            {
                (StatKeys.MaxHealthExtraPoints, BalanceConfig.Global("druidBaseHealth", 5.5f)),
            };
            HpPerLevel = BalanceConfig.Global("druidHpPerLevel", 0.8f);

            PrimaryResource = new ResourcePoolDef
            {
                Id = "mana",
                Max = 100f,
                RegenPerSec = BalanceConfig.Global("druidManaRegenPerSec", 5f),
                StartFull = true,
                ColorR = 0.30f, ColorG = 0.80f, ColorB = 0.35f // wild green (distinct from mage violet / shaman teal / paladin blue)
            };
            UsesComboPoints = true; // Cat form combo points
        }
    }
}
