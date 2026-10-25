using System.Reflection;
using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Spells;
using canrpgclasses.scenarios.Infrastructure;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.scenarios;

/// <summary>The extension points an add-on mod uses.</summary>
[Trait("Category", "E2E")]
public class AddonApiScenarios : RpgScenarioBase
{
    [AtlasScenario]
    public async Task RegisterImpact_Should_ReplaceTheHandler()
    {
        ITestPlayer p = await JoinAs("priest");
        FillResource(p);

        // The registry has no getter, so the shipped handler is saved and put back through reflection.
        var table = (Dictionary<ImpactAction, ImpactHandler>)typeof(SpellExecutor)
            .GetField("impacts", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        ImpactHandler original = table[ImpactAction.Shield];
        AddCleanup(() => SpellExecutor.RegisterImpact(ImpactAction.Shield, original));

        int calls = 0;
        SpellExecutor.RegisterImpact(ImpactAction.Shield, (ctx, target, impact) => calls++);
        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:warding_word", AimContext.None), Caster(p).LastFailReason.ToString());

        Assert.Equal(1, calls);
        Assert.Equal(0f, p.Entity.WatchedAttributes.GetFloat(SpellExecutor.AbsorbKey, 0f));
    }

    [AtlasScenario]
    public async Task TryCast_Should_RefuseQuietly_ForAnEntityWithoutACaster()
    {
        ITestPlayer p = await JoinAs("priest");
        Entity chicken = SpawnTarget(p);

        Assert.False(RpgCombatApi.TryCast((EntityAgent)chicken, "canrpgclasses:warding_word", AimContext.None));
        Assert.False(RpgCombatApi.IsCasting(chicken));
    }

    [AtlasScenario]
    public async Task ScanTargets_Should_IncludeTheCoreAssembly()
    {
        await Join("scan");

        var targets = ModExtensions.ScanTargets(World.Api);

        Assert.Contains(typeof(canrpgclassesModSystem).Assembly, targets);
        Assert.DoesNotContain(typeof(AddonApiScenarios).Assembly, targets);
    }
}
