using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.Control;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Talents;
using canrpgclasses.scenarios.Infrastructure;
using Vintagestory.API.Common;

namespace canrpgclasses.scenarios;

/// <summary>What survives a relog and what a death clears.</summary>
[Trait("Category", "E2E")]
public class PersistenceScenarios : RpgScenarioBase
{
    private async Task<ITestPlayer> Relog(ITestPlayer p)
    {
        string name = p.Player.PlayerName, uid = p.Player.PlayerUID;
        await Leave(p);
        ITestPlayer back = await JoinExact(name);
        Assert.Equal(uid, back.Player.PlayerUID);
        return back;
    }

    [AtlasScenario]
    public async Task Relog_Should_KeepClassLevelAndTalents()
    {
        ITestPlayer p = await JoinAs("priest", level: 10);
        Assert.True(Talents(p).TrySpend("priest:deeper_shadow"));

        ITestPlayer back = await Relog(p);

        Assert.Equal("priest", TalentState.CurrentClass(back.Entity));
        Assert.Equal(10, Progression(back).Level);
        Assert.Equal(1, TalentState.Rank(back.Entity, "priest:deeper_shadow"));
    }

    [AtlasScenario]
    public async Task Relog_Should_KeepCooldowns()
    {
        ITestPlayer p = await JoinAs("priest");
        FillResource(p);
        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:warding_word", AimContext.None), Caster(p).LastFailReason.ToString());

        ITestPlayer back = await Relog(p);

        Assert.True(RpgCombatApi.IsOnCooldown(back.Entity, "canrpgclasses:warding_word"), "the cooldown was lost on relog");
    }

    [AtlasScenario]
    public async Task Relog_Should_KeepComboPoints()
    {
        ITestPlayer p = await JoinAs("rogue");
        ResourceState.AddCombo(p.Entity, 3);

        ITestPlayer back = await Relog(p);

        Assert.Equal(3, ResourceState.Combo(back.Entity));
    }

    [AtlasScenario]
    public async Task Death_Should_ClearCrowdControl()
    {
        ITestPlayer p = await JoinAs("warrior");
        Survival(p);
        Assert.True(ControlState.Apply(p.Entity, ControlType.Stun, 30f, null));
        Assert.True(ControlState.Apply(p.Entity, ControlType.Silence, 30f, null));

        p.Entity.Die(EnumDespawnReason.Death, new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Injury });
        await World.Ticks(1);

        Assert.False(ControlState.IsActive(p.Entity, ControlType.Stun));
        Assert.False(ControlState.IsActive(p.Entity, ControlType.Silence));
        p.Entity.Revive();
    }
}
