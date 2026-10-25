using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core.Progression;
using canrpgclasses.scenarios.Infrastructure;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.scenarios;

/// <summary>XP curve, kill XP from xp.json multipliers, and the level cap.</summary>
[Trait("Category", "E2E")]
public class ProgressionScenarios : RpgScenarioBase
{
    private static void KillBy(Entity victim, ITestPlayer killer) =>
        victim.Die(EnumDespawnReason.Death, new DamageSource
        {
            Source = EnumDamageSource.Player,
            SourceEntity = killer.Entity,
            CauseEntity = killer.Entity,
            Type = EnumDamageType.BluntAttack
        });

    [AtlasScenario]
    public async Task XpCurve_Should_FollowTheFormula()
    {
        ITestPlayer p = await JoinAs("warrior");

        Assert.Equal(0, EBProgression.XpForLevel(1));
        Assert.Equal((long)(20 * Math.Pow(9, 1.6)), EBProgression.XpForLevel(10));

        Progression(p).AddXp(EBProgression.XpForLevel(5));
        Assert.Equal(5, Progression(p).Level);
    }

    [AtlasScenario]
    public async Task Xp_Should_StopAtTheLevelCap()
    {
        ITestPlayer p = await JoinAs("warrior", level: 25);
        long xp = Progression(p).Xp;

        Progression(p).AddXp(100000);
        Assert.Equal(25, Progression(p).Level);
        Assert.Equal(xp, Progression(p).Xp);
    }

    [AtlasScenario]
    public async Task MaxLevel_Should_FollowTheGlobal()
    {
        SetGlobal("maxLevel", 10);
        ITestPlayer p = await JoinAs("warrior");

        Progression(p).AddXp(EBProgression.XpForLevel(20));
        Assert.Equal(10, Progression(p).Level);
    }

    [AtlasScenario]
    public async Task Kill_Should_GiveXp_ScaledByTheMultiplier()
    {
        ITestPlayer p = await JoinAs("warrior");
        Entity drifter = SpawnNear(p, "game:drifter-normal");
        // Shipped xp.json: game:drifter-* = 0.5.
        long expected = (long)(MaxHealth(drifter) * EBProgression.XpPerHealth * 0.5f);
        Assert.Equal(expected, XpRules.XpFor(drifter, 1));

        long before = Progression(p).Xp;
        KillBy(drifter, p);
        await World.Ticks(1);

        Assert.Equal(before + expected, Progression(p).Xp);
    }

    [AtlasScenario]
    public async Task UnlistedCreature_Should_GiveFullXp()
    {
        ITestPlayer p = await JoinAs("warrior");
        Entity chicken = SpawnNear(p, "game:chicken-rooster");

        Assert.Equal(1f, XpRules.Multiplier(chicken));
        Assert.Equal((long)(MaxHealth(chicken) * EBProgression.XpPerHealth), XpRules.XpFor(chicken, 1));
    }

    [AtlasScenario]
    public async Task PlayerKill_Should_GiveNoXp()
    {
        ITestPlayer killer = await JoinAs("warrior");
        ITestPlayer victim = await JoinAs("rogue");
        Survival(victim);
        long before = Progression(killer).Xp;

        KillBy(victim.Entity, killer);
        await World.Ticks(1);

        Assert.Equal(before, Progression(killer).Xp);
        victim.Entity.Revive();
    }

    [AtlasScenario]
    public async Task LevelFalloff_Should_CutXp_ForLowMobs()
    {
        await JoinAs("warrior");
        // Defaults: 2 levels of grace, -10% per level after, never below 10%.
        Assert.Equal(1f, XpRules.LevelFactor(10, 8));
        Assert.Equal(0.9f, XpRules.LevelFactor(10, 7), 3);
        Assert.Equal(0.1f, XpRules.LevelFactor(25, 1), 3);
        Assert.Equal(1f, XpRules.LevelFactor(5, 20));
    }
}
