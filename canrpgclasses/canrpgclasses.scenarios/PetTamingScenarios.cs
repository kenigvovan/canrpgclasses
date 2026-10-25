using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Hunter;
using canrpgclasses.scenarios.Infrastructure;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace canrpgclasses.scenarios;

/// <summary>
/// PET_PLAN: Kill Command, Revive Pet, taming wild beasts into the stable, and the stable's switch/release rules.
/// </summary>
[Trait("Category", "E2E")]
public class PetTamingScenarios : RpgScenarioBase
{
    private async Task<EntityAgent> SummonPet(ITestPlayer p)
    {
        ResetCooldowns(p, "canrpgclasses:summon_pet");
        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:summon_pet", AimContext.None), Caster(p).LastFailReason.ToString());
        await CastToEnd(p);
        await World.Until(() => HunterPetSystem.GetPet(p.Entity) != null, "the pet to appear", 60);
        EntityAgent pet = HunterPetSystem.GetPet(p.Entity)!;
        AddCleanup(() => { if (pet.Alive) pet.Die(EnumDespawnReason.Removed); });
        return pet;
    }

    private static void SetHealthFraction(Entity e, float fraction)
    {
        var health = e.GetBehavior<EntityBehaviorHealth>() ?? throw new InvalidOperationException("no health behavior");
        health.Health = health.MaxHealth * fraction;
    }

    // ---- Kill Command ----------------------------------------------------------------------

    [AtlasScenario]
    public async Task KillCommand_Should_EmpowerOnlyTheNextBite()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await SummonPet(p);
        Entity first = SpawnTarget(p), second = SpawnTarget(p);
        FillResource(p);

        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:kill_command", AimAt(first)), Caster(p).LastFailReason.ToString());
        Assert.Equal(first, HunterPetSystem.ForcedTargetOf(wolf));

        float empowered = Loss(first, () => Hit(first, 4f, wolf));
        float plain = Loss(second, () => Hit(second, 4f, wolf));
        float after = Loss(first, () => Hit(first, 4f, wolf));

        Assert.True(empowered > plain + 1f, $"the commanded bite ({empowered}) should beat a plain one ({plain})");
        Assert.Equal(plain, after, 2); // spent on the first bite
    }

    // ---- Revive Pet ------------------------------------------------------------------------

    [AtlasScenario]
    public async Task RevivePet_Should_BeRefused_WhileThePetLives()
    {
        ITestPlayer p = await JoinAs("hunter");
        await SummonPet(p);
        FillResource(p);

        Assert.False(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:revive_pet", AimContext.None));
    }

    [AtlasScenario]
    public async Task RevivePet_Should_BringAFallenPetBack_AtPartialHealth()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await SummonPet(p);
        FillResource(p);

        wolf.Die(EnumDespawnReason.Death);
        await World.Until(() => HunterPetSystem.GetPet(p.Entity) == null, "the pet to fall", 40);
        Assert.True(Cooldowns(p).IsOnCooldown("canrpgclasses:summon_pet"), "a death should lock out the plain summon");

        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:revive_pet", AimContext.None), Caster(p).LastFailReason.ToString());
        await CastToEnd(p);
        await World.Until(() => HunterPetSystem.GetPet(p.Entity) != null, "the pet to return", 60);

        Assert.InRange(HunterPetSystem.PetStatus(p.Entity).Fraction, 0.45f, 0.55f);
        Assert.False(Cooldowns(p).IsOnCooldown("canrpgclasses:summon_pet"), "revive should lift the lockout");
    }

    // ---- Taming ----------------------------------------------------------------------------

    [AtlasScenario]
    public async Task Tame_Should_Refuse_AHealthyBeast()
    {
        ITestPlayer p = await JoinAs("hunter", level: 20);
        Entity fox = SpawnNear(p, "game:fox-red-adult-male");
        FillResource(p);

        Assert.False(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:tame_beast", AimAt(fox)));
        Assert.Equal(1, PetStable.Count(p.Entity));
    }

    [AtlasScenario]
    public async Task Tame_Should_Refuse_BelowTheSpeciesLevel()
    {
        ITestPlayer p = await JoinAs("hunter", level: 12); // bears need 20
        Entity bear = SpawnNear(p, "game:bear-brown-adult-male");
        SetHealthFraction(bear, 0.2f);
        FillResource(p);

        Assert.False(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:tame_beast", AimAt(bear)));
    }

    [AtlasScenario]
    public async Task Tame_Should_TurnAWeakenedBeast_IntoTheActivePet()
    {
        ITestPlayer p = await JoinAs("hunter", level: 20);
        Survival(p);
        Entity fox = SpawnNear(p, "game:fox-red-adult-male");
        SetHealthFraction(fox, 0.2f);
        FillResource(p);

        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:tame_beast", AimAt(fox)), Caster(p).LastFailReason.ToString());
        await CastToEnd(p, 300);
        await World.Until(() => HunterPetSystem.GetPet(p.Entity) != null, "the tamed pet to be summoned", 60);
        var pet = HunterPetSystem.GetPet(p.Entity)!;
        AddCleanup(() => { if (pet.Alive) pet.Die(EnumDespawnReason.Removed); });

        Assert.False(fox.Alive, "the wild fox should be gone");
        Assert.Equal(2, PetStable.Count(p.Entity));
        Assert.Equal("fox-red-male", PetStable.ActiveSpecies(p.Entity));
        Assert.Equal("fox-red-male", PetSpecies.Of(pet));
    }

    /// <summary>What you see is what you get: the pet wears the exact coat the wild beast had.</summary>
    [AtlasScenario]
    public async Task Tame_Should_KeepTheWildCoat()
    {
        ITestPlayer p = await JoinAs("hunter", level: 20);
        Survival(p);
        Entity wolf = SpawnNear(p, "game:wolf-eurasian-adult-female");
        wolf.WatchedAttributes.SetInt("textureIndex", 9); // the rare coat
        SetHealthFraction(wolf, 0.2f);
        FillResource(p);

        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:tame_beast", AimAt(wolf)), Caster(p).LastFailReason.ToString());
        await CastToEnd(p, 300);
        await World.Until(() => HunterPetSystem.GetPet(p.Entity) != null, "the tamed pet to be summoned", 60);
        var pet = HunterPetSystem.GetPet(p.Entity)!;
        AddCleanup(() => { if (pet.Alive) pet.Die(EnumDespawnReason.Removed); });

        Assert.Equal("wolf-eurasian", PetSpecies.Of(pet));
        Assert.Equal(9, pet.WatchedAttributes.GetInt("textureIndex", -1));
        Assert.True(PetStable.IsRare(PetStable.Active(p.Entity)));
        Assert.True(PetJournal.Has(PetJournal.Read(p.Entity), "wolf-eurasian", 9), "the tame goes into the bestiary");
    }

    [AtlasScenario]
    public async Task WildCoats_Should_BeRare_OnlyAtTheConfiguredChance()
    {
        ITestPlayer p = await JoinAs("hunter");

        SetGlobal("wildRareCoatChance", 1f);
        Assert.Equal(9, SpawnNear(p, "game:wolf-eurasian-adult-male").WatchedAttributes.GetInt("textureIndex", -1));

        SetGlobal("wildRareCoatChance", 0f);
        for (int i = 0; i < 10; i++)
            Assert.NotEqual(9, SpawnNear(p, "game:wolf-eurasian-adult-male").WatchedAttributes.GetInt("textureIndex", -1));
    }

    [AtlasScenario]
    public async Task OldStable_Should_MigrateSpecies_ToLooks()
    {
        ITestPlayer p = await JoinAs("hunter");
        var tree = PetStable.Tree(p.Entity);
        PetStable.Add(p.Entity, "bear-brown", 1);
        PetStable.Add(p.Entity, "fox-red-male", 0);
        // Rewind to a pre-collection stable.
        tree.GetTreeAttribute("s1").SetString("species", "bear");
        tree.GetTreeAttribute("s2").SetString("species", "fox");
        tree.GetTreeAttribute("s2").SetInt("color", 1);
        tree.SetInt("v", 1);

        PetStable.Tree(p.Entity);

        Assert.Equal("bear-brown", PetStable.Slot(p.Entity, 1)!.GetString("species"));
        Assert.Equal(1, PetStable.Slot(p.Entity, 1)!.GetInt("color"));
        Assert.Equal("fox-arctic-male", PetStable.Slot(p.Entity, 2)!.GetString("species")); // the old rare arctic coat
        Assert.Equal("wolf", PetStable.Slot(p.Entity, 0)!.GetString("species"));
    }

    // ---- Stable ----------------------------------------------------------------------------

    [AtlasScenario]
    public async Task Stable_Should_SwapThePetOut_WhenAnotherIsChosen()
    {
        ITestPlayer p = await JoinAs("hunter");
        PetStable.Add(p.Entity, "bear-brown");
        PetStable.SetActive(p.Entity, 0);
        await SummonPet(p);

        HunterPetSystem.StableAction(p.Entity, "select", 1);

        await World.Until(() => PetSpecies.Of(HunterPetSystem.GetPet(p.Entity)) == "bear-brown", "the bear to replace the wolf", 40);
        var bear = HunterPetSystem.GetPet(p.Entity)!;
        AddCleanup(() => { if (bear.Alive) bear.Die(EnumDespawnReason.Removed); });
    }

    [AtlasScenario]
    public async Task Stable_Should_NotReleaseTheOnlyPet()
    {
        ITestPlayer p = await JoinAs("hunter");

        HunterPetSystem.StableAction(p.Entity, "release", 0);

        Assert.Equal(1, PetStable.Count(p.Entity));
    }

    [AtlasScenario]
    public async Task Stable_Should_GiveBackWornItems_OnRelease()
    {
        ITestPlayer p = await JoinAs("hunter");
        PetStable.Add(p.Entity, "fox-red-male"); // now active
        PetStable.SetActiveCosmetic(p.Entity, "neck", "canrpgclasses:petcollar");

        HunterPetSystem.StableAction(p.Entity, "release", 1);

        Assert.Equal(1, PetStable.Count(p.Entity));
        bool gotCollar = false;
        p.Entity.WalkInventory(slot =>
        {
            if (slot.Itemstack?.Collectible?.Code?.Path == "petcollar") gotCollar = true;
            return !gotCollar;
        });
        Assert.True(gotCollar, "the released pet's collar should come back to the owner");
    }
}
