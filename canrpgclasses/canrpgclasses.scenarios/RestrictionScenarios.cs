using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core.Classes;
using canrpgclasses.scenarios.Infrastructure;

namespace canrpgclasses.scenarios;

/// <summary>canrpgclasses-restrictions.json: which RPG class a player may pick by vanilla class and race.</summary>
[Trait("Category", "E2E")]
public class RestrictionScenarios : RpgScenarioBase
{
    private static void SetVanillaClass(ITestPlayer p, string code) =>
        p.Entity.WatchedAttributes.SetString(ClassRestrictions.VanillaClassAttr, code);

    [AtlasScenario]
    public async Task VanillaRule_Should_LimitThePick()
    {
        UseRestrictions("""
            { "enabled": true, "vanillaClassRulesEnabled": true, "raceRulesEnabled": false,
              "byVanillaClass": { "blackguard": { "allow": ["warrior", "paladin"], "deny": [] } } }
            """);
        ITestPlayer p = await Join("blackguard");
        SetVanillaClass(p, "blackguard");

        Assert.False(ClassChange.TryChange(p.Player, "mage", adminOverride: false, out string reason));
        Assert.Equal(ClassRestrictions.ReasonClass, reason);
        Assert.False(ClassChange.HasChosen(p.Entity), "a refused first pick must not count as a pick");

        Assert.True(ClassChange.TryChange(p.Player, "warrior", adminOverride: false, out reason), reason);
    }

    [AtlasScenario]
    public async Task SpecificEntry_Should_ReplaceTheDefault()
    {
        UseRestrictions("""
            { "enabled": true, "vanillaClassRulesEnabled": true, "raceRulesEnabled": false,
              "byVanillaClass": {
                "*":      { "allow": [], "deny": ["mage"] },
                "tailor": { "allow": [], "deny": ["rogue"] } } }
            """);
        ITestPlayer p = await Join("tailor");
        SetVanillaClass(p, "tailor");

        Assert.True(ClassRestrictions.Allowed(p.Entity, "mage", out _), "the tailor entry replaces '*', it does not add to it");
        Assert.False(ClassRestrictions.Allowed(p.Entity, "rogue", out _));
    }

    [AtlasScenario]
    public async Task UnknownVanillaClass_Should_PassWhenAllowed()
    {
        UseRestrictions("""
            { "enabled": true, "vanillaClassRulesEnabled": true, "raceRulesEnabled": false, "allowWhenVanillaUnknown": true,
              "byVanillaClass": { "*": { "allow": ["warrior"], "deny": [] } } }
            """);
        ITestPlayer p = await Join("nobody");
        SetVanillaClass(p, "");

        Assert.True(ClassRestrictions.Allowed(p.Entity, "mage", out _));
    }

    [AtlasScenario]
    public async Task RaceRule_Should_LimitThePick()
    {
        ITestPlayer p = await Join("race");
        string race = ClassRestrictions.RaceOf(p.Entity);
        UseRestrictions($$"""
            { "enabled": true, "vanillaClassRulesEnabled": false, "raceRulesEnabled": true,
              "byRace": { "{{race}}": { "allow": [], "deny": ["druid"] } } }
            """);

        Assert.False(ClassChange.TryChange(p.Player, "druid", adminOverride: false, out string reason));
        Assert.Equal(ClassRestrictions.ReasonRace, reason);
    }

    [AtlasScenario]
    public async Task RpgClassRule_Should_RequireAVanillaClass()
    {
        UseRestrictions("""
            { "enabled": true, "vanillaClassRulesEnabled": true, "raceRulesEnabled": false,
              "byRpgClass": { "hunter": { "requireVanilla": ["hunter"] } } }
            """);
        ITestPlayer p = await Join("commoner");
        SetVanillaClass(p, "commoner");

        Assert.False(ClassRestrictions.Allowed(p.Entity, "hunter", out _));
        SetVanillaClass(p, "hunter");
        Assert.True(ClassRestrictions.Allowed(p.Entity, "hunter", out _));
    }

    [AtlasScenario]
    public async Task AdminOverride_Should_IgnoreRestrictions()
    {
        UseRestrictions("""
            { "enabled": true, "vanillaClassRulesEnabled": true, "raceRulesEnabled": false,
              "byVanillaClass": { "*": { "allow": [], "deny": ["shaman"] } } }
            """);
        ITestPlayer p = await Join("override");
        SetVanillaClass(p, "commoner");

        CommandResult set = await p.ExecuteCommand("/canrpgclass shaman");
        Assert.True(set.Ok, "/canrpgclass: " + set.Message);
        Assert.Equal("shaman", Core.Talents.TalentState.CurrentClass(p.Entity));
    }

    [AtlasScenario]
    public async Task DisabledRules_Should_AllowEverything()
    {
        UseRestrictions("""
            { "enabled": false, "vanillaClassRulesEnabled": true,
              "byVanillaClass": { "*": { "allow": [], "deny": ["mage"] } } }
            """);
        ITestPlayer p = await Join("off");
        SetVanillaClass(p, "commoner");

        Assert.True(ClassRestrictions.Allowed(p.Entity, "mage", out _));
    }
}
