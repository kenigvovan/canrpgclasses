using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.Control;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;
using canrpgclasses.scenarios.Infrastructure;

namespace canrpgclasses.scenarios;

/// <summary>
/// The server-side cast gate, on the priest's Warding Word: an instant self-shield with a mana cost and a
/// cooldown, which smart-casts onto the caster when nothing is aimed at.
/// </summary>
[Trait("Category", "E2E")]
public class CastScenarios : RpgScenarioBase
{
    private const string WardingWord = "canrpgclasses:warding_word";

    private async Task<ITestPlayer> GivenPriest()
    {
        ITestPlayer p = await JoinAs("priest");
        FillResource(p);
        return p;
    }

    [AtlasScenario]
    public async Task Cast_Should_SpendMana_StartCooldown_AndShieldTheCaster()
    {
        ITestPlayer p = await GivenPriest();
        Spell spell = RpgCombatApi.GetSpell(World.Api, WardingWord)!;
        float before = ResourceState.Get(p.Entity, Pool(p.Entity));

        Assert.True(RpgCombatApi.TryCast(p.Entity, WardingWord, AimContext.None), Caster(p).LastFailReason.ToString());

        Assert.True(ResourceState.Get(p.Entity, Pool(p.Entity)) < before, "mana was not spent");
        Assert.True(Cooldowns(p).IsOnCooldown(spell.CooldownKey), "no cooldown after the cast");
        Assert.True(p.Entity.WatchedAttributes.GetFloat(SpellExecutor.AbsorbKey, 0f) > 0f, "no absorb on the caster");
    }

    [AtlasScenario]
    public async Task Cast_Should_BeRefused_When_OnCooldown()
    {
        ITestPlayer p = await GivenPriest();
        Assert.True(RpgCombatApi.TryCast(p.Entity, WardingWord, AimContext.None), Caster(p).LastFailReason.ToString());
        FillResource(p);
        float before = ResourceState.Get(p.Entity, Pool(p.Entity));

        Assert.False(RpgCombatApi.TryCast(p.Entity, WardingWord, AimContext.None));
        Assert.Equal(CastFailReason.OnCooldown, Caster(p).LastFailReason);
        Assert.Equal(before, ResourceState.Get(p.Entity, Pool(p.Entity)));
    }

    [AtlasScenario]
    public async Task Cast_Should_BeRefused_When_ManaIsShort()
    {
        ITestPlayer p = await GivenPriest();
        ResourceState.Set(p.Entity, Pool(p.Entity), 0f);

        Assert.False(RpgCombatApi.TryCast(p.Entity, WardingWord, AimContext.None));
        Assert.Equal(CastFailReason.NoResource, Caster(p).LastFailReason);
    }

    [AtlasScenario]
    public async Task Cast_Should_BeRefused_When_Silenced()
    {
        ITestPlayer p = await GivenPriest();
        Assert.True(RpgCombatApi.ApplyControl(p.Entity, ControlType.Silence, 5f, null), "precondition: silence applied");
        AddCleanup(() => ControlState.ClearAll(p.Entity));

        Assert.False(RpgCombatApi.TryCast(p.Entity, WardingWord, AimContext.None));
        Assert.Equal(CastFailReason.Silenced, Caster(p).LastFailReason);
    }

    [AtlasScenario]
    public async Task OtherClassSpell_Should_NotBeAvailable()
    {
        ITestPlayer priest = await GivenPriest();
        ITestPlayer rogue = await JoinAs("rogue");

        Assert.True(SpellAccess.CanCast(priest.Entity, Mod, WardingWord));
        Assert.False(SpellAccess.CanCast(rogue.Entity, Mod, WardingWord));
    }
}
