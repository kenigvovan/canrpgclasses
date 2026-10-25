using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Hunter;
using canrpgclasses.scenarios.Infrastructure;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace canrpgclasses.scenarios;

/// <summary>
/// The hunter's wolf: health and damage that follow the owner, assist-only targeting, the Growl taunt, and the
/// reworked Kindred Spirit and Beast Kinship talents.
/// </summary>
[Trait("Category", "E2E")]
public class HunterPetScenarios : RpgScenarioBase
{
    // EBHunterPet re-checks every 2s.
    private const int PetTickWait = 90;

    private async Task<EntityAgent> SummonWolf(ITestPlayer p)
    {
        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:summon_pet", AimContext.None), Caster(p).LastFailReason.ToString());
        await CastToEnd(p);
        await World.Until(() => HunterPetSystem.GetPet(p.Entity) != null, "the wolf to appear", 60);
        EntityAgent wolf = HunterPetSystem.GetPet(p.Entity)!;
        AddCleanup(() => { if (wolf.Alive) wolf.Die(EnumDespawnReason.Removed); });
        return wolf;
    }

    /// <summary>Puts the wolf <paramref name="blocks"/> east of its owner; the engine applies a teleport a tick later.</summary>
    private async Task MoveWolf(EntityAgent wolf, ITestPlayer owner, double blocks)
    {
        var to = owner.Entity.Pos.XYZ.AddCopy(blocks, 0, 0);
        wolf.TeleportTo(to);
        await World.Until(() => wolf.Pos.XYZ.SquareDistanceTo(to) < 1.0, "the wolf to arrive", 60);
    }

    private static EntityBehaviorHealth WolfHealth(Entity wolf) =>
        wolf.GetBehavior<EntityBehaviorHealth>() ?? throw new InvalidOperationException("the wolf has no health");

    private void Order(ITestPlayer p, string command)
    {
        ResetCooldowns(p, "canrpgclasses:pet_" + command);
        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:pet_" + command, AimContext.None), Caster(p).LastFailReason.ToString());
    }

    // ---- health and damage follow the owner --------------------------------------------------

    [AtlasScenario]
    public async Task LiveWolf_Should_TakeTheOwnersLevelUp()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await SummonWolf(p);
        float before = WolfHealth(wolf).MaxHealth;

        Progression(p).SetLevel(10);

        await World.Until(() => WolfHealth(wolf).MaxHealth > before + 0.01f, "the wolf's max health to grow", PetTickWait);
        Assert.Equal(HunterPetSystem.PetMaxHealth(p.Entity, wolf), WolfHealth(wolf).BaseMaxHealth, 2);
    }

    [AtlasScenario]
    public async Task LiveWolf_Should_TakeAlpha_AndStayAsHurtAsItWas()
    {
        ITestPlayer p = await JoinAs("hunter", level: 10);
        EntityAgent wolf = await SummonWolf(p);
        var health = WolfHealth(wolf);
        health.Health = health.MaxHealth / 2f;
        float before = health.MaxHealth;

        Assert.True(Talents(p).TrySpend("hunter:alpha"));

        await World.Until(() => health.MaxHealth > before + 0.01f, "Alpha to reach the wolf", PetTickWait);
        Assert.InRange(health.Health / health.MaxHealth, 0.48f, 0.52f);
    }

    [AtlasScenario]
    public async Task WolfDamage_Should_ScaleWithTheOwnersLevel()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await SummonWolf(p);
        Entity low = SpawnTarget(p), high = SpawnTarget(p);

        float atOne = Loss(low, () => Hit(low, 4f, wolf));
        Progression(p).SetLevel(25);
        float atTwentyFive = Loss(high, () => Hit(high, 4f, wolf));

        float expected = HunterPetSystem.PetLevelDamageMultiplier(p.Entity);
        Assert.InRange(expected, 2.19f, 2.21f); // shipped hunterPetDamagePerLevel 0.05
        Assert.InRange(atTwentyFive / atOne, expected - 0.05f, expected + 0.05f);
    }

    // ---- targeting -------------------------------------------------------------------------

    [AtlasScenario]
    public async Task Wolf_Should_NotPickFights_OnItsOwn()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await SummonWolf(p);
        Entity bystander = SpawnTarget(p);
        var melee = wolf.GetBehavior<EntityBehaviorTaskAI>()!.TaskManager.GetTask<AiTaskPetMelee>()!;

        Assert.Equal(PetMode.Aggressive, HunterPetSystem.PetModeOf(wolf));
        Assert.False(melee.IsTargetableEntity(bystander, 10f), "an aggressive wolf picked a target on its own");

        HunterPetSystem.AssignPetTarget(p.Entity, bystander);
        Assert.True(melee.IsTargetableEntity(bystander, 10f), "a commanded target must be attackable");
    }

    [AtlasScenario]
    public async Task Pet_Should_Leave_WhenTheOwnerStopsBeingAHunter()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await SummonWolf(p);

        Assert.True(ClassChange.TryChange(p.Player, "mage", adminOverride: true, out string reason), reason);

        await World.Until(() => !wolf.Alive, "the pet to leave", PetTickWait);
        Assert.Null(HunterPetSystem.GetPet(p.Entity));
    }

    [AtlasScenario]
    public async Task Wolf_Should_NotBiteACorpse()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await SummonWolf(p);
        Entity foe = SpawnTarget(p);
        var melee = wolf.GetBehavior<EntityBehaviorTaskAI>()!.TaskManager.GetTask<AiTaskPetMelee>()!;

        Hit(wolf, 1f, foe); // the foe becomes the wolf's attacker
        Assert.True(melee.IsTargetableEntity(foe, 10f), "the attacker must be a target while alive");

        foe.Die(EnumDespawnReason.Death);
        Assert.False(melee.IsTargetableEntity(foe, 10f), "the wolf kept biting a corpse");
    }

    [AtlasScenario]
    public async Task Aggressive_Should_JoinTheOwnersAttacks_DefensiveShouldNot()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await SummonWolf(p);
        Entity first = SpawnTarget(p), second = SpawnTarget(p);

        Hit(first, 1f, p.Entity);
        Assert.Equal(first, HunterPetSystem.ForcedTargetOf(wolf));

        Order(p, "defensive");
        HunterPetSystem.ClearPetTarget(wolf);
        Hit(second, 1f, p.Entity);
        Assert.Null(HunterPetSystem.ForcedTargetOf(wolf));
    }

    [AtlasScenario]
    public async Task Defensive_Should_StillDefendTheOwner()
    {
        ITestPlayer p = await JoinAs("hunter");
        Survival(p);
        EntityAgent wolf = await SummonWolf(p);
        Order(p, "defensive");
        Entity attacker = SpawnTarget(p);

        Hit(p.Entity, 1f, attacker);

        Assert.Equal(attacker, HunterPetSystem.ForcedTargetOf(wolf));
    }

    [AtlasScenario]
    public async Task Growl_Should_TurnHostileCreaturesOntoTheWolf()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await SummonWolf(p);
        Entity drifter = SpawnNear(p, "game:drifter-normal");
        var melee = drifter.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager.GetTask<AiTaskMeleeAttack>();
        Assert.NotNull(melee);

        Order(p, "growl");

        Assert.Equal(wolf, melee!.targetEntity);
    }

    [AtlasScenario]
    public async Task Growl_Should_DoNothing_WithoutAWolf()
    {
        ITestPlayer p = await JoinAs("hunter");

        Order(p, "growl"); // a no-op, not an error
        Assert.Null(HunterPetSystem.GetPet(p.Entity));
    }

    // ---- talents ---------------------------------------------------------------------------

    [AtlasScenario]
    public async Task KindredSpirit_Should_MoveDamageToANearbyWolf()
    {
        ITestPlayer p = await JoinAs("hunter");
        Survival(p);
        EntityAgent wolf = await SummonWolf(p);
        await MoveWolf(wolf, p, 2);
        SetRank(p, "hunter:kindred_spirit", 3); // 24%
        Entity attacker = SpawnTarget(p);

        float wolfBefore = Health(wolf);
        float ownerLoss = Loss(p.Entity, () => Hit(p.Entity, 5f, attacker));
        float wolfLoss = wolfBefore - Health(wolf);

        Assert.InRange(wolfLoss, 1.1f, 1.3f);
        Assert.InRange(ownerLoss, 3.7f, 3.9f);
    }

    [AtlasScenario]
    public async Task KindredSpirit_Should_NotReachAFarWolf()
    {
        ITestPlayer p = await JoinAs("hunter");
        Survival(p);
        EntityAgent wolf = await SummonWolf(p);
        Order(p, "stay"); // or the follow task teleports it straight back
        await MoveWolf(wolf, p, 30);
        SetRank(p, "hunter:kindred_spirit", 3);
        Entity attacker = SpawnTarget(p);

        float wolfBefore = Health(wolf);
        Hit(p.Entity, 5f, attacker);

        Assert.Equal(wolfBefore, Health(wolf));
    }

    [AtlasScenario]
    public async Task BeastKinship_Should_HealTheWolf_OutOfCombat()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await SummonWolf(p);
        SetRank(p, "hunter:beast_kinship", 3);
        var health = WolfHealth(wolf);
        health.Health = health.MaxHealth / 2f;
        float hurt = health.Health;

        await World.Until(() => health.Health > hurt + 0.01f, "the wolf to regenerate", PetTickWait);
    }

    [AtlasScenario]
    public async Task BeastKinship_Should_NotHeal_WithoutRanks()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await SummonWolf(p);
        var health = WolfHealth(wolf);
        health.Health = health.MaxHealth / 2f;
        float hurt = health.Health;

        await World.Ticks(PetTickWait);

        Assert.Equal(hurt, health.Health, 2);
    }

    // ---- HUD data --------------------------------------------------------------------------

    [AtlasScenario]
    public async Task PetStatus_Should_ReportTheWolf_ForTheHuds()
    {
        ITestPlayer p = await JoinAs("hunter");
        Assert.Equal(-1f, HunterPetSystem.PetStatus(p.Entity).Fraction);

        EntityAgent wolf = await SummonWolf(p);
        CommandResult named = await p.ExecuteCommand("/canrpgpet name Rex");
        Assert.True(named.Ok, "/canrpgpet name: " + named.Message);
        WolfHealth(wolf).Health = WolfHealth(wolf).MaxHealth / 4f;

        var (fraction, name) = HunterPetSystem.PetStatus(p.Entity);
        Assert.InRange(fraction, 0.24f, 0.26f);
        Assert.Equal("Rex", name);
    }
}
