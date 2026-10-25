using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.Control;
using canrpgclasses.scenarios.Infrastructure;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.scenarios;

/// <summary>ControlState: diminishing returns per bracket (full, x0.5, x0.25, immune) and break-on-damage.</summary>
[Trait("Category", "E2E")]
public class ControlScenarios : RpgScenarioBase
{
    private static float SecondsLeft(Entity e, string untilKey) =>
        (e.WatchedAttributes.GetLong(untilKey, 0) - e.World.ElapsedMilliseconds) / 1000f;

    private async Task<ITestPlayer> GivenTarget()
    {
        ITestPlayer p = await JoinAs("warrior");
        AddCleanup(() => ControlState.ClearAll(p.Entity));
        return p;
    }

    [AtlasScenario]
    public async Task RepeatedControl_Should_Shorten_ThenBecomeImmune()
    {
        ITestPlayer p = await GivenTarget();
        float[] expected = { 8f, 4f, 2f };

        foreach (float seconds in expected)
        {
            Assert.True(ControlState.Apply(p.Entity, ControlType.Silence, 8f, null));
            Assert.InRange(SecondsLeft(p.Entity, CombatFlags.SilenceUntil), seconds - 0.2f, seconds + 0.01f);
            ControlState.Clear(p.Entity, ControlType.Silence);
        }

        Assert.False(ControlState.Apply(p.Entity, ControlType.Silence, 8f, null), "the 4th silence must be immune");
        Assert.False(ControlState.IsActive(p.Entity, ControlType.Silence));
    }

    [AtlasScenario]
    public async Task StunAndFear_Should_ShareOneBracket()
    {
        ITestPlayer p = await GivenTarget();

        Assert.True(ControlState.Apply(p.Entity, ControlType.Stun, 4f, null));
        ControlState.Clear(p.Entity, ControlType.Stun);

        Assert.True(ControlState.Apply(p.Entity, ControlType.Fear, 4f, null));
        Assert.InRange(SecondsLeft(p.Entity, CombatFlags.FearUntil), 1.8f, 2.01f);
    }

    [AtlasScenario]
    public async Task OtherBrackets_Should_NotDiminishEachOther()
    {
        ITestPlayer p = await GivenTarget();
        for (int i = 0; i < 3; i++)
        {
            ControlState.Apply(p.Entity, ControlType.Silence, 4f, null);
            ControlState.Clear(p.Entity, ControlType.Silence);
        }

        Assert.True(ControlState.Apply(p.Entity, ControlType.Root, 4f, null));
        Assert.InRange(SecondsLeft(p.Entity, CombatFlags.RootUntil), 3.8f, 4.01f);
    }

    [AtlasScenario]
    public async Task Root_Should_StopMovement_ButNotCasting()
    {
        ITestPlayer p = await GivenTarget();
        ControlState.Apply(p.Entity, ControlType.Root, 4f, null);

        Assert.True(ControlState.PreventsMovement(p.Entity));
        Assert.False(ControlState.PreventsCasting(p.Entity));
        Assert.False(ControlState.PreventsActions(p.Entity));
    }

    [AtlasScenario]
    public async Task Transmute_Should_Break_OnDamage()
    {
        ITestPlayer p = await GivenTarget();
        Survival(p);
        Entity attacker = SpawnNear(p, "game:chicken-rooster");
        Assert.True(RpgCombatApi.ApplyControl(p.Entity, ControlType.Transmute, 8f, null, breakOnDamage: true));
        Assert.True(ControlState.IsActive(p.Entity, ControlType.Transmute));

        Hit(p.Entity, 1f, attacker);

        Assert.False(ControlState.IsActive(p.Entity, ControlType.Transmute));
    }

    [AtlasScenario]
    public async Task Stun_Should_Hold_ThroughDamage()
    {
        ITestPlayer p = await GivenTarget();
        Survival(p);
        Entity attacker = SpawnNear(p, "game:chicken-rooster");
        ControlState.Apply(p.Entity, ControlType.Stun, 8f, null);

        Hit(p.Entity, 1f, attacker);

        Assert.True(ControlState.IsActive(p.Entity, ControlType.Stun));
    }

    [AtlasScenario]
    public async Task DurationModifier_Should_MakeImmune()
    {
        ITestPlayer p = await GivenTarget();
        Func<Entity, ControlType, float, Entity?, float> immune = (target, type, seconds, _) =>
            target == p.Entity && type == ControlType.Stun ? 0f : seconds;
        RpgCombatApi.RegisterControlDurationModifier(World.Api, immune);
        AddCleanup(() => RpgCombatApi.UnregisterControlDurationModifier(World.Api, immune));

        Assert.False(RpgCombatApi.ApplyControl(p.Entity, ControlType.Stun, 4f, null));
        Assert.True(RpgCombatApi.ApplyControl(p.Entity, ControlType.Silence, 4f, null));
    }
}
