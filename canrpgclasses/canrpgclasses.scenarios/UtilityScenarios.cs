using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Portals;
using canrpgclasses.Core.Spells;
using canrpgclasses.scenarios.Infrastructure;

namespace canrpgclasses.scenarios;

/// <summary>Utility buffs (server side: the synced flag the client reads), portal points and the summon ritual.</summary>
[Trait("Category", "E2E")]
public class UtilityScenarios : RpgScenarioBase
{
    private async Task AssertFlagFrom(ITestPlayer p, string spellId, string flag)
    {
        FillResource(p);
        ResetCooldowns(p, spellId);
        Assert.True(RpgCombatApi.TryCast(p.Entity, spellId, AimContext.None), $"{spellId}: {Caster(p).LastFailReason}");
        await World.Until(() => p.Entity.WatchedAttributes.GetBool(flag), $"{flag} to be raised", 40);
    }

    [AtlasScenario]
    public async Task FeatherFall_Should_RaiseTheSlowFallFlag()
    {
        ITestPlayer p = await JoinAs("mage");
        await AssertFlagFrom(p, "canrpgclasses:feather_fall", UtilityFlags.SlowFall);
    }

    [AtlasScenario]
    public async Task WaterWalking_And_GuidingLight_Should_RaiseTheirFlags()
    {
        ITestPlayer shaman = await JoinAs("shaman");
        await AssertFlagFrom(shaman, "canrpgclasses:water_walking", UtilityFlags.WaterWalk);

        ITestPlayer priest = await JoinAs("priest");
        await AssertFlagFrom(priest, "canrpgclasses:guiding_light", UtilityFlags.MagicLight);
    }

    [AtlasScenario]
    public async Task PortalCommand_Should_AddAndRemoveAdminPoints()
    {
        ITestPlayer p = await Join("portaladmin");
        const string name = "scenario_point";
        AddCleanup(() => PortalAnchors.Remove(name));

        CommandResult add = await p.ExecuteCommand($"/canrpgportal add {name}");
        Assert.True(add.Ok, "/canrpgportal add: " + add.Message);
        Assert.NotNull(PortalAnchors.Find(name));

        CommandResult list = await p.ExecuteCommand("/canrpgportal list");
        Assert.Contains(name, list.Message);

        CommandResult remove = await p.ExecuteCommand($"/canrpgportal remove {name}");
        Assert.True(remove.Ok, "/canrpgportal remove: " + remove.Message);
        Assert.Null(PortalAnchors.Find(name));
    }

    [AtlasScenario]
    public async Task AdminOnlyAnchors_Should_RefusePersonalAnchors()
    {
        SetSpellBalance("waypoint", "anchorMode", (int)AnchorMode.Admin);
        ITestPlayer p = await JoinAs("mage");
        FillResource(p);
        Assert.Equal(AnchorMode.Admin, PortalAnchors.Mode);

        Assert.False(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:portal_anchor", AimContext.None));
        Assert.Equal(CastFailReason.Blocked, Caster(p).LastFailReason);
    }

    [AtlasScenario]
    public async Task DisabledWaypoint_Should_LeaveTheSpellbook()
    {
        ITestPlayer p = await JoinAs("mage");
        Assert.True(Mod.Spells.Get("canrpgclasses:waypoint")!.Enabled, "precondition: the shipped config enables Waypoint");

        SetSpellBalance("waypoint", "enabled", 0f);

        Assert.False(Mod.Spells.Get("canrpgclasses:waypoint")!.Enabled);
        Assert.DoesNotContain("canrpgclasses:waypoint", SpellAccess.Available(p.Entity, Mod));
    }

    [AtlasScenario]
    public async Task SummonRitual_Should_NeedAPartyMember()
    {
        ITestPlayer priest = await JoinAs("priest"), friend = await JoinAs("warrior");
        FillResource(priest);
        var pick = new AimContext { Choice = friend.Player.PlayerUID };

        Assert.False(RpgCombatApi.TryCast(priest.Entity, "canrpgclasses:summon_ritual", pick));
        Assert.Equal(CastFailReason.Blocked, Caster(priest).LastFailReason);

        Party(priest, friend);
        Assert.True(RpgCombatApi.TryCast(priest.Entity, "canrpgclasses:summon_ritual", pick), Caster(priest).LastFailReason.ToString());
        Assert.True(Caster(priest).IsCasting, "the ritual has a cast bar");
        AddCleanup(() => RpgCombatApi.Interrupt(priest.Entity));
    }
}
