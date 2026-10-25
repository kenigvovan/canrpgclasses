using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Talents;
using canrpgclasses.scenarios.Infrastructure;

namespace canrpgclasses.scenarios;

/// <summary>Class pick and re-pick against classChangeReselectMode (core.json).</summary>
[Trait("Category", "E2E")]
public class ClassChangeScenarios : RpgScenarioBase
{
    [AtlasScenario]
    public async Task ClassChange_Should_StripTheOldClassesAura_AndItsBuff()
    {
        ITestPlayer p = await JoinAs("paladin");
        FillResource(p);
        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:aura_of_faith", AimContext.None), Caster(p).LastFailReason.ToString());
        await World.Until(() => RpgCombatApi.HasEffect(p.Entity, AuraEffectIds.Regen), "the aura buff", 60);

        Assert.True(ClassChange.TryChange(p.Player, "mage", adminOverride: true, out string reason), reason);

        Assert.Equal("", p.Entity.WatchedAttributes.GetString(AttrKeys.ActiveAura, ""));
        await World.Until(() => !RpgCombatApi.HasEffect(p.Entity, AuraEffectIds.Regen), "the aura buff to go", 60);
    }

    [AtlasScenario]
    public async Task FirstPick_Should_BeFree_When_ReselectIsAdminOnly()
    {
        SetGlobal("classChangeReselectMode", (int)ReselectMode.AdminOnly);
        ITestPlayer p = await Join("newbie");
        Assert.False(ClassChange.HasChosen(p.Entity), "precondition: a new player has not picked a class");

        Assert.True(ClassChange.TryChange(p.Player, "mage", adminOverride: false, out string reason), reason);
        Assert.Equal("mage", TalentState.CurrentClass(p.Entity));

        Assert.False(ClassChange.TryChange(p.Player, "priest", adminOverride: false, out reason));
        Assert.Equal("admin-only", reason);
        Assert.Equal("mage", TalentState.CurrentClass(p.Entity));
    }

    [AtlasScenario]
    public async Task Repick_Should_BeRefused_When_OnCooldown()
    {
        SetGlobal("classChangeReselectMode", (int)ReselectMode.Cooldown);
        ITestPlayer p = await Join("cooldown");

        Assert.True(ClassChange.TryChange(p.Player, "warrior", adminOverride: false, out string reason), reason);
        Assert.False(ClassChange.TryChange(p.Player, "rogue", adminOverride: false, out reason));
        Assert.Equal("on-cooldown", reason);
    }

    [AtlasScenario]
    public async Task Repick_Should_BeAllowed_When_Unrestricted()
    {
        SetGlobal("classChangeReselectMode", (int)ReselectMode.Unrestricted);
        ITestPlayer p = await Join("free");

        Assert.True(ClassChange.TryChange(p.Player, "warrior", adminOverride: false, out string reason), reason);
        Assert.True(ClassChange.TryChange(p.Player, "rogue", adminOverride: false, out reason), reason);
        Assert.Equal("rogue", TalentState.CurrentClass(p.Entity));
    }

    [AtlasScenario]
    public async Task FreeRepick_Should_SkipThePolicy_Once()
    {
        SetGlobal("classChangeReselectMode", (int)ReselectMode.AdminOnly);
        ITestPlayer p = await Join("granted");
        Assert.True(ClassChange.TryChange(p.Player, "mage", adminOverride: false, out string reason), reason);

        CommandResult grant = await p.ExecuteCommand($"/canrpgclassfree {p.Player.PlayerName}");
        Assert.True(grant.Ok, "/canrpgclassfree: " + grant.Message);

        Assert.True(ClassChange.TryChange(p.Player, "priest", adminOverride: false, out reason), reason);
        Assert.False(ClassChange.TryChange(p.Player, "shaman", adminOverride: false, out reason));
        Assert.Equal("admin-only", reason);
    }

    [AtlasScenario]
    public async Task SameClass_Should_BeRefused()
    {
        ITestPlayer p = await JoinAs("hunter");

        Assert.False(ClassChange.TryChange(p.Player, "hunter", adminOverride: true, out string reason));
        Assert.Equal("already-this-class", reason);
    }

    [AtlasScenario]
    public async Task UnknownClass_Should_BeRefused()
    {
        ITestPlayer p = await Join("typo");

        Assert.False(ClassChange.TryChange(p.Player, "necromancer_typo", adminOverride: true, out string reason));
        Assert.Equal("unknown-class", reason);
        Assert.False(ClassChange.HasChosen(p.Entity));
    }

    [AtlasScenario]
    public async Task ClassChange_Should_RefundTalents_And_KeepLevelPerClass()
    {
        SetGlobal("classChangeReselectMode", (int)ReselectMode.Unrestricted);
        ITestPlayer p = await JoinAs("priest", level: 10);
        Assert.True(Talents(p).TrySpend("priest:deeper_shadow"), "precondition: a talent point was spent");

        Assert.True(ClassChange.TryChange(p.Player, "mage", adminOverride: false, out string reason), reason);
        Assert.Equal(0, TalentState.Rank(p.Entity, "priest:deeper_shadow"));
        Assert.Equal(1, Progression(p).Level);

        Assert.True(ClassChange.TryChange(p.Player, "priest", adminOverride: false, out reason), reason);
        Assert.Equal(10, Progression(p).Level);
        Assert.Equal(0, TalentState.Rank(p.Entity, "priest:deeper_shadow"));
    }
}
