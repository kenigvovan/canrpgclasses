using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.Effects;
using canrpgclasses.Hunter;
using canrpgclasses.scenarios.Infrastructure;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace canrpgclasses.scenarios;

/// <summary>
/// Pet looks and families (petFamily / petLook per variant), rare colours, the family specials, and cosmetics carried
/// from the stable onto the summoned pet.
/// </summary>
[Trait("Category", "E2E")]
public class PetSpeciesScenarios : RpgScenarioBase
{
    /// <summary>Makes <paramref name="species"/> the active stable pet and summons it.</summary>
    private async Task<EntityAgent> SummonSpecies(ITestPlayer p, string species)
    {
        if (species != PetSpecies.Default) PetStable.Add(p.Entity, species);
        ResetCooldowns(p, "canrpgclasses:summon_pet");
        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:summon_pet", AimContext.None), Caster(p).LastFailReason.ToString());
        await CastToEnd(p);
        await World.Until(() => HunterPetSystem.GetPet(p.Entity) != null, "the pet to appear", 60);
        EntityAgent pet = HunterPetSystem.GetPet(p.Entity)!;
        AddCleanup(() => { if (pet.Alive) pet.Die(EnumDespawnReason.Removed); });
        Assert.Equal(species, PetSpecies.Of(pet));
        return pet;
    }

    private void Special(ITestPlayer p)
    {
        FillResource(p);
        ResetCooldowns(p, "canrpgclasses:pet_special");
        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:pet_special", AimContext.None), Caster(p).LastFailReason.ToString());
    }

    // ---- species data ----------------------------------------------------------------------

    /// <summary>Guards the ByType resolution inside attributes: without it every profile lookup falls back.</summary>
    [AtlasScenario]
    public async Task Families_Should_ResolvePerLook()
    {
        ITestPlayer p = await JoinAs("hunter");
        var world = p.Entity.World;

        Assert.Equal(1.6f, PetSpecies.Family(PetSpecies.TypeFor(world, "bear-brown"))!["hpMul"].AsFloat(), 2);
        Assert.Equal("guard", PetSpecies.Family(PetSpecies.TypeFor(world, "bear-panda"))!["special"].AsString());
        Assert.Equal("evade", PetSpecies.Family(PetSpecies.TypeFor(world, "raccoon-pup"))!["special"].AsString());
        Assert.Equal(20, PetSpecies.MinTameLevel(world, "bear-polar"));
        Assert.Equal("fox", PetSpecies.FamilyOf(world, "raccoon-male"));
    }

    /// <summary>The balance promise: a look never changes numbers, only its family does.</summary>
    [AtlasScenario]
    public async Task EveryLookOfAFamily_Should_FightTheSame()
    {
        ITestPlayer p = await JoinAs("hunter");
        var world = p.Entity.World;
        var byFamily = new Dictionary<string, string>();

        foreach (string look in PetSpecies.AllLooks(world))
        {
            var f = PetSpecies.Family(PetSpecies.TypeFor(world, look))!;
            string numbers = $"{f["role"].AsString()}|{f["special"].AsString()}|{f["hpMul"].AsFloat()}|{f["dmgMul"].AsFloat()}|{f["minTameLevel"].AsInt()}";
            string family = f["family"].AsString()!;
            if (byFamily.TryGetValue(family, out string? seen)) Assert.True(seen == numbers, $"{look} differs from its family {family}");
            else byFamily[family] = numbers;
        }
        Assert.Equal(4, byFamily.Count);
    }

    [AtlasScenario]
    public async Task ForWild_Should_MapVanillaBeasts_AndNothingElse()
    {
        ITestPlayer p = await JoinAs("hunter");
        var world = p.Entity.World;

        Assert.Equal("fox-red-male", PetSpecies.ForWild(world, SpawnNear(p, "game:fox-red-adult-male")));
        Assert.Equal("fox-arctic-pup", PetSpecies.ForWild(world, SpawnNear(p, "game:fox-arctic-baby-female")));
        Assert.Equal("hyena-spotted-female", PetSpecies.ForWild(world, SpawnNear(p, "game:hyena-spotted-adult-female")));
        Assert.Equal("bear-polar", PetSpecies.ForWild(world, SpawnNear(p, "game:bear-polar-adult-male")));
        Assert.Equal("wolf-eurasian-pup", PetSpecies.ForWild(world, SpawnNear(p, "game:wolf-eurasian-baby-male")));
        Assert.Null(PetSpecies.ForWild(world, SpawnNear(p, "game:chicken-rooster")));
    }

    [AtlasScenario]
    public async Task Bear_Should_HaveMoreHealth_ThanTheWolf()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await SummonSpecies(p, "wolf");
        float wolfMax = MaxHealth(wolf);
        EntityAgent bear = await SummonSpecies(p, "bear-black");

        Assert.InRange(MaxHealth(bear) / wolfMax, 1.55f, 1.65f);
    }

    // ---- colours ---------------------------------------------------------------------------

    [AtlasScenario]
    public async Task RollColor_Should_GiveTheRareColour_OnlyOnALuckyRoll()
    {
        ITestPlayer p = await JoinAs("hunter");
        var world = p.Entity.World;

        Assert.Equal(9, PetSpecies.RollColor(world, "wolf-eurasian", 1f)); // the 10th coat is the rare one
        for (int i = 0; i < 20; i++) Assert.NotEqual(9, PetSpecies.RollColor(world, "wolf-eurasian", 0f));
    }

    [AtlasScenario]
    public async Task CycleColor_Should_SkipRareColours_ThePetDoesNotOwn()
    {
        ITestPlayer p = await JoinAs("hunter");
        int common = PetStable.Add(p.Entity, "wolf-eurasian", 8);
        int rare = PetStable.Add(p.Entity, "wolf-eurasian", 9);

        PetStable.CycleColor(p.Entity, common);
        Assert.Equal(0, PetStable.Slot(p.Entity, common)!.GetInt("color")); // 9 is rare and not its own

        PetStable.Slot(p.Entity, rare)!.SetInt("color", 8);
        PetStable.CycleColor(p.Entity, rare);
        Assert.Equal(9, PetStable.Slot(p.Entity, rare)!.GetInt("color")); // its own rare coat comes back round
    }

    [AtlasScenario]
    public async Task SummonedPet_Should_WearTheSlotsColour()
    {
        ITestPlayer p = await JoinAs("hunter");
        PetStable.Add(p.Entity, "bear-brown", 2); // becomes the active pet
        ResetCooldowns(p, "canrpgclasses:summon_pet");
        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:summon_pet", AimContext.None));
        await CastToEnd(p);
        await World.Until(() => HunterPetSystem.GetPet(p.Entity) != null, "the bear to appear", 60);
        var bear = HunterPetSystem.GetPet(p.Entity)!;
        AddCleanup(() => { if (bear.Alive) bear.Die(EnumDespawnReason.Removed); });

        Assert.Equal(2, bear.WatchedAttributes.GetInt("textureIndex", -1));
    }

    // ---- specials --------------------------------------------------------------------------

    [AtlasScenario]
    public async Task WolfSpecial_Should_MakeTheNextBiteBleed()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await SummonSpecies(p, "wolf");
        Entity target = SpawnTarget(p);

        Special(p);
        Hit(target, 4f, wolf);

        Assert.NotEqual(-1, EffectTier(target, "jagged_blades"));
    }

    [AtlasScenario]
    public async Task HyenaSpecial_Should_CutHealing_OnTheNextBite()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent hyena = await SummonSpecies(p, "hyena-spotted-male");
        Entity target = SpawnTarget(p);

        Special(p);
        Hit(target, 4f, hyena);

        Assert.NotEqual(-1, EffectTier(target, WoundedEffectId.Id));
    }

    [AtlasScenario]
    public async Task BearSpecial_Should_CutTheDamageItTakes()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent bear = await SummonSpecies(p, "bear-brown");
        Entity attacker = SpawnTarget(p);

        float plain = Loss(bear, () => Hit(bear, 5f, attacker));
        Special(p);
        float guarded = Loss(bear, () => Hit(bear, 5f, attacker));

        Assert.InRange(guarded / plain, 0.55f, 0.65f); // shipped guardDamageCut 0.4
    }

    [AtlasScenario]
    public async Task FoxSpecial_Should_DodgeHits_ButNotKindredSpiritsShare()
    {
        ITestPlayer p = await JoinAs("hunter");
        Survival(p);
        EntityAgent fox = await SummonSpecies(p, "fox-red-male");
        SetSpellBalance("pet_special", "evadeChance", 1f);
        SetRank(p, "hunter:kindred_spirit", 3);
        Entity attacker = SpawnTarget(p);

        Special(p);

        Assert.Equal(0f, Loss(fox, () => Hit(fox, 5f, attacker)), 2);
        float shared = Loss(fox, () => Hit(p.Entity, 5f, attacker));
        Assert.True(shared > 0.5f, $"Kindred Spirit's share ({shared}) must not be dodged");
    }

    // ---- cosmetics -------------------------------------------------------------------------

    [AtlasScenario]
    public async Task WornItems_Should_FollowThePet_OntoEverySummon()
    {
        ITestPlayer p = await JoinAs("hunter");
        PetStable.SetActiveCosmetic(p.Entity, "neck", "canrpgclasses:petcollar");

        EntityAgent wolf = await SummonSpecies(p, "wolf");

        var worn = wolf.WatchedAttributes.GetTreeAttribute(EBPetCosmetics.Key);
        Assert.NotNull(worn);
        Assert.Equal("canrpgclasses:petcollar", worn!.GetString("neck"));
    }

    [AtlasScenario]
    public async Task Collar_Should_DescribeItselfAsAPetCosmetic()
    {
        ITestPlayer p = await JoinAs("hunter");
        var collar = p.Entity.World.GetItem(new AssetLocation("canrpgclasses:petcollar"));

        var spec = EBPetCosmetics.Spec(collar);
        Assert.NotNull(spec);
        Assert.Equal("neck", spec!["place"].AsString());
        Assert.Equal("band", spec["kind"].AsString());
    }
}
