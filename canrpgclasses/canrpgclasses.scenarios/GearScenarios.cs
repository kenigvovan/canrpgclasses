using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core.EB;
using GearAffinity = canrpgclasses.Core.Classes.GearAffinity;
using canrpgclasses.scenarios.Infrastructure;
using Vintagestory.API.Common;

namespace canrpgclasses.scenarios;

/// <summary>Gear affinities: a modifier source is on its stats exactly while the affinity matches the gear.</summary>
[Trait("Category", "E2E")]
public class GearScenarios : RpgScenarioBase
{
    private static IInventory CharacterInventory(ITestPlayer p) =>
        p.Player.InventoryManager.GetOwnInventory("character") ?? throw new InvalidOperationException("no character inventory");

    private void Wear(ITestPlayer p, string itemCode, EnumCharacterDressType slotType)
    {
        Item item = World.Api.World.GetItem(new AssetLocation(itemCode)) ?? throw new ArgumentException("no item " + itemCode);
        ItemSlot slot = CharacterInventory(p)[(int)slotType];
        slot.Itemstack = new ItemStack(item);
        slot.MarkDirty();
        AddCleanup(() => { slot.Itemstack = null; slot.MarkDirty(); });
    }

    private static void Refresh(ITestPlayer p) =>
        (p.Entity.GetBehavior<EBGearAffinity>() ?? throw new InvalidOperationException("no EBGearAffinity")).Refresh();

    /// <summary>Every affinity's sources present exactly when it matches the current gear.</summary>
    private void AssertAffinitiesFollowTheGear(ITestPlayer p, IEnumerable<GearAffinity> affinities)
    {
        var weapon = p.Player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible;
        var armor = CharacterInventory(p);
        foreach (var aff in affinities)
        {
            bool match = aff.Matches(weapon, armor);
            foreach (var (stat, _) in aff.Modifiers)
                Assert.True(HasSource(p.Entity, stat, "canrpgaffinity_" + aff.Key) == match,
                    $"{aff.Key} on {stat}: expected {(match ? "applied" : "absent")}");
        }
    }

    [AtlasScenario]
    public async Task Affinities_Should_FollowBareGear()
    {
        ITestPlayer p = await JoinAs("mage");
        Refresh(p);

        AssertAffinitiesFollowTheGear(p, Mod.Classes.Get("mage")!.GearAffinities);
        AssertAffinitiesFollowTheGear(p, GearAffinity.Global);
    }

    [AtlasScenario]
    public async Task MetalArmor_Should_GrantArmorWard_AndFlipClothAffinities()
    {
        ITestPlayer p = await JoinAs("mage");
        Wear(p, "game:armor-body-plate-iron", EnumCharacterDressType.ArmorBody);
        Refresh(p);

        var ward = GearAffinity.Global.Single(a => a.Key == "armor_ward");
        Assert.True(ward.Matches(null, CharacterInventory(p)), "precondition: plate counts as metal armor");
        AssertAffinitiesFollowTheGear(p, Mod.Classes.Get("mage")!.GearAffinities);
        AssertAffinitiesFollowTheGear(p, GearAffinity.Global);
    }

    [AtlasScenario]
    public async Task WeaponAffinity_Should_FollowTheHeldWeapon()
    {
        ITestPlayer p = await JoinAs("warrior");
        await p.GiveItem("game:blade-falx-iron");
        Refresh(p);

        var blade = Mod.Classes.Get("warrior")!.GearAffinities.Single(a => a.Key == "blade_mastery");
        Assert.True(blade.Matches(p.Player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible, CharacterInventory(p)),
            "precondition: a falx counts as a sword");
        AssertAffinitiesFollowTheGear(p, Mod.Classes.Get("warrior")!.GearAffinities);

        p.Player.InventoryManager.ActiveHotbarSlot!.Itemstack = null;
        p.Player.InventoryManager.ActiveHotbarSlot.MarkDirty();
        Refresh(p);
        AssertAffinitiesFollowTheGear(p, Mod.Classes.Get("warrior")!.GearAffinities);
    }
}
