using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using canrpgclasses.scenarios.Infrastructure;

namespace canrpgclasses.scenarios;

/// <summary>
/// A data-only class from ModConfig/canrpgclasses-content.json (what the in-game editors write). The fixture is
/// the Battlemage example of the wiki's Data Content Packs page, so the page's example is checked too.
/// </summary>
[Trait("Category", "E2E")]
[AtlasDataFiles("fixtures/Content", TargetPath = "ModConfig")]
public class ContentScenarios : RpgScenarioBase
{
    [AtlasScenario]
    public async Task DataClass_Should_LoadWithoutProblems()
    {
        await Join("loader");

        Assert.NotNull(Mod.Classes.Get("battlemage"));
        Assert.True(Mod.Spells.TryGet("mymod:fire_slash", out _));
        Assert.True(Mod.Spells.TryGet("mymod:flame_wave", out _));
        Assert.NotNull(Mod.Talents.Get("battlemage:learn_flame_wave"));

        var problems = Mod.LastContentReport?.Problems ?? Array.Empty<string>();
        Assert.DoesNotContain(problems, p => p.Contains("battlemage") || p.Contains("mymod:"));
    }

    [AtlasScenario]
    public async Task DataClass_Should_BePickable_WithItsBaseSpells()
    {
        ITestPlayer p = await Join("picker");

        Assert.True(ClassChange.TryChange(p.Player, "battlemage", adminOverride: false, out string reason), reason);
        Assert.Equal("mana", ResourceState.PrimaryPool(p.Entity)?.Id);
        Assert.True(SpellAccess.CanCast(p.Entity, Mod, "mymod:fire_slash"));
        Assert.True(SpellAccess.CanCast(p.Entity, Mod, "mymod:flame_ward"));
        Assert.False(SpellAccess.CanCast(p.Entity, Mod, "mymod:flame_wave"), "flame_wave comes from a talent");
    }

    [AtlasScenario]
    public async Task DataSpell_Should_Cast_LikeACodedOne()
    {
        ITestPlayer p = await JoinAs("battlemage");
        FillResource(p);
        float before = ResourceState.Get(p.Entity, Pool(p.Entity));

        Assert.True(RpgCombatApi.TryCast(p.Entity, "mymod:flame_ward", AimContext.None), Caster(p).LastFailReason.ToString());

        Assert.True(ResourceState.Get(p.Entity, Pool(p.Entity)) < before, "mana was not spent");
        Assert.True(Cooldowns(p).IsOnCooldown("mymod:flame_ward"));
        Assert.True(p.Entity.WatchedAttributes.GetFloat(SpellExecutor.AbsorbKey, 0f) > 0f, "no absorb on the caster");
    }

    [AtlasScenario]
    public async Task DataTalents_Should_FollowTheTreeRules_AndGrantTheirSpell()
    {
        ITestPlayer p = await JoinAs("battlemage", level: 10);

        Assert.False(Talents(p).TrySpend("battlemage:learn_flame_wave"), "tier 1 with 0 points in the tree");
        for (int i = 0; i < 3; i++) Assert.True(Talents(p).TrySpend("battlemage:hot_blood"));
        Assert.True(Talents(p).TrySpend("battlemage:fire_sense"));

        Assert.True(Talents(p).TrySpend("battlemage:learn_flame_wave"));
        Assert.True(SpellAccess.CanCast(p.Entity, Mod, "mymod:flame_wave"));
    }

    [AtlasScenario]
    public async Task DataTalent_Should_SetItsStatPerRank()
    {
        ITestPlayer p = await JoinAs("battlemage", level: 10);
        const string key = "canrpgcontent_battlemage_hot_blood";

        for (int i = 0; i < 3; i++) Assert.True(Talents(p).TrySpend("battlemage:hot_blood"));
        Assert.Equal(6f, p.Entity.Stats["maxhealthExtraPoints"].ValuesByKey[key].Value);

        Talents(p).Respec();
        Assert.False(p.Entity.Stats["maxhealthExtraPoints"].ValuesByKey.ContainsKey(key));
        Assert.Equal(0, TalentState.Rank(p.Entity, "battlemage:hot_blood"));
    }
}

/// <summary>Content with dangling references: loaded as far as possible, every problem reported.</summary>
[Trait("Category", "E2E")]
[AtlasDataFiles("fixtures/BrokenContent", TargetPath = "ModConfig")]
public class BrokenContentScenarios : RpgScenarioBase
{
    [AtlasScenario]
    public async Task BrokenReferences_Should_AllBeReported()
    {
        await Join("broken");
        var problems = Mod.LastContentReport?.Problems ?? Array.Empty<string>();

        Assert.Contains(problems, p => p.Contains("mymod:no_such_spell"));
        Assert.Contains(problems, p => p.Contains("mymod:missing_spell"));
        Assert.Contains(problems, p => p.Contains("brokenclass:wrong_tree"));
        Assert.Contains(problems, p => p.Contains("no_such_class"));
        Assert.Contains(problems, p => p.Contains("brokenclass:ghost"));
        Assert.Contains(problems, p => p.Contains("has no 'id'"));
        // A broken class still loads; only its bad references are reported.
        Assert.NotNull(Mod.Classes.Get("brokenclass"));
    }

    [AtlasScenario]
    public async Task Reload_Should_ReportTheProblems()
    {
        ITestPlayer p = await Join("reloader");

        CommandResult reload = await p.ExecuteCommand("/canrpgreload");

        Assert.Contains("mymod:no_such_spell", reload.Message);
    }
}
