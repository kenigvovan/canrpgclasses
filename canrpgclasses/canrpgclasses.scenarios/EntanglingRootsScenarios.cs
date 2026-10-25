using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.Control;
using canrpgclasses.scenarios.Infrastructure;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.scenarios;

/// <summary>Druid's Entangling Roots: roots a mob and holds a vine model on it only while the root lasts.</summary>
[Trait("Category", "E2E")]
public class EntanglingRootsScenarios : RpgScenarioBase
{
    private static Entity[] Vines(Entity target) =>
        target.World.GetEntitiesAround(target.Pos.XYZ, 2f, 2f, e => e.Code?.Path == "druidroots");

    [AtlasScenario]
    public async Task Roots_Should_HoldAMob_AndShowVines_UntilTheRootEnds()
    {
        ITestPlayer p = await JoinAs("druid");
        FillResource(p);
        Entity target = SpawnTarget(p);
        AddCleanup(() => ControlState.ClearAll(target));

        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:entangling_roots", AimAt(target)), Caster(p).LastFailReason.ToString());
        await CastToEnd(p);

        Assert.True(ControlState.RootsMotion(target), "the mob is not rooted");
        await World.Until(() => Vines(target).Length == 1, "the vines to appear", 20);

        ControlState.Clear(target, ControlType.Root);
        await World.Until(() => Vines(target).Length == 0, "the vines to go away with the root", 20);
    }
}
