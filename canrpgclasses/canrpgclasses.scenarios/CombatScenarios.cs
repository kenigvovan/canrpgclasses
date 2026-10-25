using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.HarmonyPatches;
using canrpgclasses.scenarios.Infrastructure;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.scenarios;

/// <summary>The damage patch: absorb shields and the combat tag.</summary>
[Trait("Category", "E2E")]
public class CombatScenarios : RpgScenarioBase
{
    private static float Absorb(Entity e) => e.WatchedAttributes.GetFloat(SpellExecutor.AbsorbKey, 0f);

    private async Task<ITestPlayer> GivenShieldedPriest()
    {
        ITestPlayer p = await JoinAs("priest");
        Survival(p);
        FillResource(p);
        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:warding_word", AimContext.None),
            Caster(p).LastFailReason.ToString());
        Assert.True(Absorb(p.Entity) > 0f, "precondition: Warding Word left an absorb");
        return p;
    }

    [AtlasScenario]
    public async Task Shield_Should_SoakAHitSmallerThanItself()
    {
        ITestPlayer p = await GivenShieldedPriest();
        Entity attacker = SpawnNear(p, "game:chicken-rooster");
        float shield = Absorb(p.Entity);
        float hit = Math.Min(1f, shield / 2f);

        Hit(p.Entity, hit, attacker);

        Assert.Equal(shield - hit, Absorb(p.Entity), 2);
    }

    [AtlasScenario]
    public async Task Shield_Should_Break_OnAHitBiggerThanItself()
    {
        ITestPlayer p = await GivenShieldedPriest();
        Entity attacker = SpawnNear(p, "game:chicken-rooster");

        Hit(p.Entity, Absorb(p.Entity) + 1f, attacker);

        Assert.Equal(0f, Absorb(p.Entity));
        Assert.False(p.Entity.WatchedAttributes.HasAttribute(CombatFlags.AbsorbUntil));
    }

    [AtlasScenario]
    public async Task Damage_Should_TagBothSidesInCombat()
    {
        ITestPlayer victim = await JoinAs("warrior");
        ITestPlayer attacker = await JoinAs("rogue");
        Survival(victim);
        Assert.False(StunPatches.IsInCombat(victim.Entity), "precondition: not in combat");

        Hit(victim.Entity, 1f, attacker.Entity);

        Assert.True(StunPatches.IsInCombat(victim.Entity));
        Assert.True(StunPatches.IsInCombat(attacker.Entity));
    }

    [AtlasScenario]
    public async Task Stealth_Should_BeRefused_InCombat()
    {
        ITestPlayer rogue = await JoinAs("rogue");
        Survival(rogue);
        FillResource(rogue);
        Entity attacker = SpawnNear(rogue, "game:chicken-rooster");
        Hit(rogue.Entity, 1f, attacker);

        Assert.False(RpgCombatApi.TryCast(rogue.Entity, "canrpgclasses:stealth", AimContext.None));
        Assert.Equal(CastFailReason.InCombat, Caster(rogue).LastFailReason);
    }

    [AtlasScenario]
    public async Task CombatTag_Should_Expire()
    {
        SetGlobal("combatTagSeconds", 1f);
        ITestPlayer p = await JoinAs("warrior");
        Survival(p);
        Entity attacker = SpawnNear(p, "game:chicken-rooster");
        Hit(p.Entity, 1f, attacker);
        Assert.True(StunPatches.IsInCombat(p.Entity), "precondition: tagged");

        await World.Until(() => !StunPatches.IsInCombat(p.Entity), "the combat tag to expire", 200);
    }
}
