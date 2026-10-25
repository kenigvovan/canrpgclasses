using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Hunter;
using canrpgclasses.scenarios.Infrastructure;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.scenarios;

/// <summary>PET_COLLECTION_PLAN K3-K5: the bestiary, its milestone rewards (slots, tricks, collars) and tricks.</summary>
[Trait("Category", "E2E")]
public class PetCollectionScenarios : RpgScenarioBase
{
    private static bool Holds(ITestPlayer p, string itemPath)
        => p.Entity.Player.InventoryManager.Find(slot => slot.Itemstack?.Collectible?.Code?.Path == itemPath) != null;

    [AtlasScenario]
    public async Task Journal_Should_CountEachLookAndColourOnce()
    {
        ITestPlayer p = await JoinAs("hunter");

        PetJournal.Record(p.Entity, "bear-polar", 0);
        PetJournal.Record(p.Entity, "bear-polar", 0);
        PetJournal.Record(p.Entity, "wolf-eurasian", 3);
        PetJournal.Record(p.Entity, "wolf", 0); // the starting wolf is never tamed, so it isn't collected

        var tree = PetJournal.Read(p.Entity);
        Assert.Equal(2, PetJournal.Entries(tree));
        Assert.True(PetJournal.Has(tree, "wolf-eurasian", 3));
        Assert.False(PetJournal.Has(tree, "wolf-eurasian", 4));
    }

    [AtlasScenario]
    public async Task Journal_Should_KeepAnEntry_AfterThePetIsReleased()
    {
        ITestPlayer p = await JoinAs("hunter");
        int slot = PetStable.Add(p.Entity, "fox-arctic-male");
        PetJournal.Record(p.Entity, "fox-arctic-male", 0);

        Assert.True(PetStable.Release(p.Entity, slot));

        Assert.True(PetJournal.Has(PetJournal.Read(p.Entity), "fox-arctic-male", 0));
    }

    [AtlasScenario]
    public async Task Journal_Should_CountPetsTamedBeforeItExisted()
    {
        ITestPlayer p = await JoinAs("hunter");
        PetStable.Add(p.Entity, "bear-brown", 2);

        PetJournal.Record(p.Entity, "hyena-spotted-male", 0); // first write seeds from the stable

        Assert.True(PetJournal.Has(PetJournal.Read(p.Entity), "bear-brown", 2));
    }

    [AtlasScenario]
    public async Task Milestone_Should_AddAStableSlot_OnlyOnce()
    {
        ITestPlayer p = await JoinAs("hunter");
        int before = PetStable.MaxSlots(p.Entity);

        PetJournal.Record(p.Entity, "bear-black", 0);
        PetJournal.Record(p.Entity, "bear-panda", 0); // 2 entries: the first slot milestone
        PetJournal.Record(p.Entity, "bear-black", 0);

        Assert.Equal(before + 1, PetStable.MaxSlots(p.Entity));
        Assert.True(PetJournal.Claimed(PetJournal.Read(p.Entity), 0));
    }

    [AtlasScenario]
    public async Task Milestones_Should_UnlockTricks_AndGiveCollars()
    {
        ITestPlayer p = await JoinAs("hunter");
        Assert.True(PetJournal.TrickUnlocked(p.Entity, "sit"), "sit is known from the start");
        Assert.False(PetJournal.TrickUnlocked(p.Entity, "lie"));

        foreach (string look in new[] { "bear-black", "bear-panda", "bear-sun", "bear-polar", "fox-red-male" })
            PetJournal.Record(p.Entity, look, 0);

        Assert.True(PetJournal.TrickUnlocked(p.Entity, "lie"), "4 entries unlock lie");
        Assert.True(Holds(p, "petcollarfine-red"), "5 entries give the red collar");
    }

    [AtlasScenario]
    public async Task CompleteFamily_Should_GiveItsCollar()
    {
        ITestPlayer p = await JoinAs("hunter");

        foreach (string look in new[] { "bear-brown", "bear-black", "bear-polar", "bear-panda" })
            PetJournal.Record(p.Entity, look, 0);
        Assert.False(Holds(p, "petcollarfine-fur"), "a bear is still missing");

        PetJournal.Record(p.Entity, "bear-sun", 0);
        Assert.True(Holds(p, "petcollarfine-fur"));
    }

    [AtlasScenario]
    public async Task RareEntry_Should_GiveTheWhiteCollar()
    {
        ITestPlayer p = await JoinAs("hunter");

        PetJournal.Record(p.Entity, "wolf-eurasian", 9); // the rare coat

        Assert.Equal(1, PetJournal.RareEntries(p.Entity.World, PetJournal.Read(p.Entity)));
        Assert.True(Holds(p, "petcollarfine-white"));
    }

    // ---- tricks (K5) ------------------------------------------------------------------------

    private async Task<EntityAgent> Summon(ITestPlayer p)
    {
        ResetCooldowns(p, "canrpgclasses:summon_pet");
        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:summon_pet", AimContext.None), Caster(p).LastFailReason.ToString());
        await CastToEnd(p);
        await World.Until(() => HunterPetSystem.GetPet(p.Entity) != null, "the pet to appear", 60);
        EntityAgent pet = HunterPetSystem.GetPet(p.Entity)!;
        AddCleanup(() => { if (pet.Alive) pet.Die(EnumDespawnReason.Removed); });
        return pet;
    }

    [AtlasScenario]
    public async Task Trick_Should_Play_UntilThePetMoves()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await Summon(p);
        var beh = wolf.GetBehavior<EBHunterPet>()!;

        HunterPetSystem.Trick(p.Entity, "sit");
        Assert.Equal("sit", beh.CurrentTrick);
        Assert.True(wolf.AnimManager.IsAnimationActive("sit"));

        wolf.TeleportTo(wolf.Pos.XYZ.AddCopy(2, 0, 0));
        await World.Until(() => beh.CurrentTrick == null, "the trick to stop", 40);
        Assert.False(wolf.AnimManager.IsAnimationActive("sit"));
    }

    [AtlasScenario]
    public async Task LockedTrick_Should_NotPlay()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await Summon(p);

        HunterPetSystem.Trick(p.Entity, "lie"); // needs 4 bestiary entries

        Assert.Null(wolf.GetBehavior<EBHunterPet>()!.CurrentTrick);
    }

    [AtlasScenario]
    public async Task Tricks_Should_FollowTheFamily()
    {
        ITestPlayer p = await JoinAs("hunter");
        PetStable.Add(p.Entity, "bear-polar");
        EntityAgent bear = await Summon(p);

        var tricks = HunterPetSystem.Tricks(bear);
        Assert.Contains("stand", tricks);
        Assert.DoesNotContain("howl", tricks);
    }

    [AtlasScenario]
    public async Task EveryReward_Should_BeARealItem()
    {
        ITestPlayer p = await JoinAs("hunter");

        foreach (var m in PetJournal.Milestones(p.Entity.Api))
            if (m["item"].AsString(null) is { } code)
                Assert.True(p.Entity.World.GetItem(new AssetLocation(code)) != null, $"{code} is not an item");
    }
}
