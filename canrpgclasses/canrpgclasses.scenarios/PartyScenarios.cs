using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Resources;
using canrpgclasses.scenarios.Infrastructure;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.scenarios;

/// <summary>Friend or foe (SpellExecutor.IsAlly over canparty) and paladin auras on the party.</summary>
[Trait("Category", "E2E")]
public class PartyScenarios : RpgScenarioBase
{
    private const string AuraOfFaith = "canrpgclasses:aura_of_faith";

    [AtlasScenario]
    public async Task Strangers_Should_NotBeAllies()
    {
        ITestPlayer a = await JoinAs("priest"), b = await JoinAs("warrior");

        Assert.False(RpgCombatApi.IsAlly(a.Entity, b.Entity));
        Assert.True(RpgCombatApi.IsAlly(a.Entity, a.Entity), "everyone is their own ally");
    }

    [AtlasScenario]
    public async Task PartyMembers_Should_BeAllies()
    {
        ITestPlayer a = await JoinAs("priest"), b = await JoinAs("warrior");
        Party(a, b);

        Assert.True(RpgCombatApi.IsAlly(a.Entity, b.Entity));
        Assert.True(RpgCombatApi.IsAlly(b.Entity, a.Entity));
    }

    [AtlasScenario]
    public async Task GuardedCreature_Should_CountAsItsOwnersAlly()
    {
        ITestPlayer owner = await JoinAs("hunter"), friend = await JoinAs("priest"), stranger = await JoinAs("rogue");
        Party(owner, friend);
        Entity pet = SpawnTarget(owner);
        pet.WatchedAttributes.SetString(AttrKeys.GuardedByPlayerUid, owner.Player.PlayerUID);

        Assert.True(RpgCombatApi.IsAlly(owner.Entity, pet));
        Assert.True(RpgCombatApi.IsAlly(friend.Entity, pet), "the owner's party shares the pet");
        Assert.False(RpgCombatApi.IsAlly(stranger.Entity, pet));
    }

    [AtlasScenario]
    public async Task AllyResolver_Should_DecideItsOwnPairs_Only()
    {
        ITestPlayer a = await JoinAs("priest"), b = await JoinAs("warrior"), c = await JoinAs("rogue");
        Func<Entity, Entity, bool?> resolver = (x, y) =>
            (x == a.Entity && y == b.Entity) || (x == b.Entity && y == a.Entity) ? true : null;
        RpgCombatApi.RegisterAllyResolver(World.Api, resolver);
        AddCleanup(() => RpgCombatApi.UnregisterAllyResolver(World.Api, resolver));

        Assert.True(RpgCombatApi.IsAlly(a.Entity, b.Entity));
        Assert.False(RpgCombatApi.IsAlly(a.Entity, c.Entity), "null falls back to the core rule");
    }

    [AtlasScenario]
    public async Task Aura_Should_ReachNearbyPartyMembers_Only()
    {
        ITestPlayer paladin = await JoinAs("paladin");
        ITestPlayer near = await JoinAs("warrior"), far = await JoinAs("rogue"), stranger = await JoinAs("mage");
        Party(paladin, near, far);
        await near.TeleportTo(paladin.Position.AddCopy(3, 0, 0));
        await stranger.TeleportTo(paladin.Position.AddCopy(0, 0, 3));
        await far.TeleportTo(paladin.Position.AddCopy(20, 0, 0));
        FillResource(paladin);

        Assert.True(RpgCombatApi.TryCast(paladin.Entity, AuraOfFaith, AimContext.None), Caster(paladin).LastFailReason.ToString());
        AddCleanup(() => paladin.Entity.WatchedAttributes.SetString(AttrKeys.ActiveAura, ""));
        await World.Until(() => RpgCombatApi.HasEffect(near.Entity, AuraEffectIds.Regen), "the aura to reach the ally", 60);

        Assert.True(RpgCombatApi.HasEffect(paladin.Entity, AuraEffectIds.Regen), "the caster is under its own aura");
        Assert.False(RpgCombatApi.HasEffect(far.Entity, AuraEffectIds.Regen), "20 blocks is outside the 10-block radius");
        Assert.False(RpgCombatApi.HasEffect(stranger.Entity, AuraEffectIds.Regen), "auras skip non-party players");
    }

    [AtlasScenario]
    public async Task Aura_Should_DrainMana_AndStopWhenItRunsOut()
    {
        ITestPlayer paladin = await JoinAs("paladin");
        FillResource(paladin);
        Assert.True(RpgCombatApi.TryCast(paladin.Entity, AuraOfFaith, AimContext.None), Caster(paladin).LastFailReason.ToString());
        Assert.Equal(AuraOfFaith, paladin.Entity.WatchedAttributes.GetString(AttrKeys.ActiveAura, ""));

        // Regen off, so the upkeep is the only thing moving the pool.
        SetGlobal("resourceRegenMultiplier", 0f);
        float full = ResourceState.Get(paladin.Entity, Pool(paladin.Entity));
        await World.Ticks(45);
        Assert.True(ResourceState.Get(paladin.Entity, Pool(paladin.Entity)) < full, "the aura costs nothing");

        ResourceState.Set(paladin.Entity, Pool(paladin.Entity), 0f);
        await World.Until(() => paladin.Entity.WatchedAttributes.GetString(AttrKeys.ActiveAura, "") == "",
            "the aura to switch off without mana", 60);
    }
}
