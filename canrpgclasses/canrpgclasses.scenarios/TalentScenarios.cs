using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using canrpgclasses.scenarios.Infrastructure;

namespace canrpgclasses.scenarios;

/// <summary>Spending rules of TalentState, on the priest's Shadow tree (tree 2).</summary>
[Trait("Category", "E2E")]
public class TalentScenarios : RpgScenarioBase
{
    private const string MindShock = "priest:learn_mind_shock";       // tier 0, 1 rank
    private const string DeeperShadow = "priest:deeper_shadow";       // tier 0, 3 ranks
    private const string ShadowBrand = "priest:learn_shadow_brand";   // tier 1, grants shadow_brand
    private const string ImprovedBrand = "priest:improved_brand";     // tier 1, needs ShadowBrand maxed

    [AtlasScenario]
    public async Task Level_Should_GiveOnePointPerLevelAboveOne()
    {
        ITestPlayer p = await JoinAs("priest", level: 25);
        Assert.Equal(24, Progression(p).TotalTalentPoints);
    }

    [AtlasScenario]
    public async Task Tier_Should_StayLocked_Until_FourPointsInTheTree()
    {
        ITestPlayer p = await JoinAs("priest", level: 10);

        Assert.False(Talents(p).TrySpend(ShadowBrand), "tier 1 must be locked with 0 points in the tree");

        Assert.True(Talents(p).TrySpend(MindShock));
        for (int i = 0; i < 2; i++) Assert.True(Talents(p).TrySpend(DeeperShadow));
        Assert.False(Talents(p).TrySpend(ShadowBrand), "tier 1 must be locked with 3 points in the tree");

        Assert.True(Talents(p).TrySpend(DeeperShadow));
        Assert.True(Talents(p).TrySpend(ShadowBrand), "tier 1 must open at 4 points in the tree");
    }

    [AtlasScenario]
    public async Task Talent_Should_NotGoPastMaxRank()
    {
        ITestPlayer p = await JoinAs("priest", level: 10);

        for (int i = 0; i < 3; i++) Assert.True(Talents(p).TrySpend(DeeperShadow));
        Assert.False(Talents(p).TrySpend(DeeperShadow));
        Assert.Equal(3, TalentState.Rank(p.Entity, DeeperShadow));
    }

    [AtlasScenario]
    public async Task Talent_Should_NeedItsPrerequisite()
    {
        ITestPlayer p = await JoinAs("priest", level: 10);
        Assert.True(Talents(p).TrySpend(MindShock));
        for (int i = 0; i < 3; i++) Assert.True(Talents(p).TrySpend(DeeperShadow));

        Assert.False(Talents(p).TrySpend(ImprovedBrand), "improved_brand needs learn_shadow_brand first");
        Assert.True(Talents(p).TrySpend(ShadowBrand));
        Assert.True(Talents(p).TrySpend(ImprovedBrand));
    }

    [AtlasScenario]
    public async Task Spending_Should_Stop_When_PointsRunOut()
    {
        ITestPlayer p = await JoinAs("priest", level: 2);

        Assert.True(Talents(p).TrySpend(DeeperShadow));
        Assert.False(Talents(p).TrySpend(DeeperShadow), "level 2 has a single point");
    }

    [AtlasScenario]
    public async Task OtherClassTalent_Should_BeRefused()
    {
        ITestPlayer p = await JoinAs("mage", level: 10);
        Assert.False(Talents(p).TrySpend(DeeperShadow));
    }

    [AtlasScenario]
    public async Task GrantingTalent_Should_UnlockItsSpell_Until_Respec()
    {
        ITestPlayer p = await JoinAs("priest", level: 10);
        const string spell = "canrpgclasses:mind_shock";
        Assert.False(SpellAccess.CanCast(p.Entity, Mod, spell), "precondition: mind_shock is not a base spell");

        Assert.True(Talents(p).TrySpend(MindShock));
        Assert.True(SpellAccess.CanCast(p.Entity, Mod, spell));

        CommandResult respec = await p.ExecuteCommand("/canrpgrespec");
        Assert.True(respec.Ok, "/canrpgrespec: " + respec.Message);
        Assert.Equal(0, TalentState.Rank(p.Entity, MindShock));
        Assert.False(SpellAccess.CanCast(p.Entity, Mod, spell));
    }
}
