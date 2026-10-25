using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;
using canrpgclasses.scenarios.Infrastructure;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.scenarios;

/// <summary>
/// Casts that take time (Charge, Channel), the global cooldown, combo points and regen. Charge: the priest's Mind
/// Shock (1.5s, 35 mana, 8s cooldown). Channel: the mage's Mystic Barrage (1.5s, 3 bolts, paid up front).
/// </summary>
[Trait("Category", "E2E")]
public class CastTimingScenarios : RpgScenarioBase
{
    private const string MindShock = "canrpgclasses:mind_shock";
    private const string Barrage = "canrpgclasses:mystic_barrage";

    private static float Mana(ITestPlayer p) => ResourceState.Get(p.Entity, Pool(p.Entity));

    private async Task<ITestPlayer> GivenFull(string classId)
    {
        ITestPlayer p = await JoinAs(classId);
        FillResource(p);
        return p;
    }

    [AtlasScenario]
    public async Task ChargeCast_Should_PayNothing_AtTheStart()
    {
        ITestPlayer p = await GivenFull("priest");
        Entity target = SpawnTarget(p);
        float before = Mana(p);

        Assert.True(RpgCombatApi.TryCast(p.Entity, MindShock, AimAt(target)), Caster(p).LastFailReason.ToString());

        Assert.True(Caster(p).IsCasting);
        Assert.Equal(before, Mana(p));
        Assert.False(Cooldowns(p).IsOnCooldown(MindShock));
        Assert.True(Cooldowns(p).IsOnCooldown(Spell.GlobalCooldownKey), "the GCD starts with the cast bar");
    }

    [AtlasScenario]
    public async Task ChargeCast_Should_PayAndHit_OnCompletion()
    {
        ITestPlayer p = await GivenFull("priest");
        Entity target = SpawnTarget(p);
        float health = Health(target);
        Assert.True(RpgCombatApi.TryCast(p.Entity, MindShock, AimAt(target)), Caster(p).LastFailReason.ToString());

        await CastToEnd(p);

        Assert.Equal(0, Caster(p).InterruptCount);
        Assert.True(Mana(p) < ResourceState.EffectiveMax(p.Entity, Pool(p.Entity)), "mana was not spent");
        Assert.True(Cooldowns(p).IsOnCooldown(MindShock));
        Assert.True(Health(target) < health, "the target took no damage");
    }

    [AtlasScenario]
    public async Task ChargeCast_Should_BreakOnMovement_AndCostNothing()
    {
        ITestPlayer p = await GivenFull("priest");
        Entity target = SpawnTarget(p);
        float before = Mana(p);
        Assert.True(RpgCombatApi.TryCast(p.Entity, MindShock, AimAt(target)), Caster(p).LastFailReason.ToString());

        p.Entity.Controls.Forward = true;
        try { await World.Ticks(2); }
        finally { p.Entity.Controls.Forward = false; }

        Assert.False(Caster(p).IsCasting);
        Assert.Equal(1, Caster(p).InterruptCount);
        Assert.Equal(before, Mana(p));
        Assert.False(Cooldowns(p).IsOnCooldown(MindShock));
    }

    [AtlasScenario]
    public async Task ChargeCast_Should_BreakOnDamage_ButNotOnHealing()
    {
        ITestPlayer p = await GivenFull("priest");
        Survival(p);
        Entity target = SpawnTarget(p);
        Assert.True(RpgCombatApi.TryCast(p.Entity, MindShock, AimAt(target)), Caster(p).LastFailReason.ToString());

        p.Entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, 1f);
        Assert.True(Caster(p).IsCasting, "a heal must not interrupt");

        Hit(p.Entity, 1f, target);
        Assert.False(Caster(p).IsCasting);
        Assert.Equal(1, Caster(p).InterruptCount);
    }

    [AtlasScenario]
    public async Task Channel_Should_PayOnce_AtTheStart()
    {
        ITestPlayer p = await GivenFull("mage");
        float before = Mana(p);

        Assert.True(RpgCombatApi.TryCast(p.Entity, Barrage, AimContext.None), Caster(p).LastFailReason.ToString());
        Assert.True(Caster(p).IsChanneling);
        float afterStart = Mana(p);
        Assert.True(afterStart < before, "a channel pays up front");
        Assert.True(Cooldowns(p).IsOnCooldown(Barrage));

        await CastToEnd(p);

        Assert.False(Caster(p).IsChanneling);
        Assert.True(Mana(p) >= afterStart, "a channel must not pay again per bolt");
    }

    [AtlasScenario]
    public async Task Channel_Should_HoldThroughDamage_ButBreakOnMovement()
    {
        ITestPlayer p = await GivenFull("mage");
        Survival(p);
        Entity attacker = SpawnTarget(p);
        Assert.True(RpgCombatApi.TryCast(p.Entity, Barrage, AimContext.None), Caster(p).LastFailReason.ToString());

        Hit(p.Entity, 1f, attacker);
        Assert.True(Caster(p).IsChanneling, "damage must not break a channel");

        p.Entity.Controls.Forward = true;
        try { await World.Ticks(2); }
        finally { p.Entity.Controls.Forward = false; }
        Assert.False(Caster(p).IsChanneling);
    }

    [AtlasScenario]
    public async Task GlobalCooldown_Should_BlockTheNextSpell_ExceptExemptOnes()
    {
        ITestPlayer p = await GivenFull("priest");
        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:warding_word", AimContext.None), Caster(p).LastFailReason.ToString());

        Assert.False(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:soothing_prayer", AimContext.None));
        Assert.Equal(CastFailReason.GlobalCooldown, Caster(p).LastFailReason);

        // Guiding Light does not trigger nor wait for the GCD.
        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:guiding_light", AimContext.None), Caster(p).LastFailReason.ToString());
    }

    [AtlasScenario]
    public async Task Builder_Should_AddAComboPoint_WhenTheEmpoweredHitLands()
    {
        ITestPlayer p = await GivenFull("rogue");
        Entity target = SpawnTarget(p);
        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:vicious_strike", AimContext.None), Caster(p).LastFailReason.ToString());
        Assert.Equal(0, ResourceState.Combo(p.Entity));

        Hit(target, 1f, p.Entity);

        Assert.Equal(1, ResourceState.Combo(p.Entity));
        ResourceState.AddCombo(p.Entity, 10);
        Assert.Equal(5, ResourceState.Combo(p.Entity));
    }

    [AtlasScenario]
    public async Task Finisher_Should_NeedPoints_AndSpendThemAll()
    {
        ITestPlayer p = await GivenFull("rogue");
        const string gutStrike = "canrpgclasses:gut_strike";

        Assert.False(RpgCombatApi.TryCast(p.Entity, gutStrike, AimContext.None));
        Assert.Equal(CastFailReason.NoCombo, Caster(p).LastFailReason);

        ResourceState.AddCombo(p.Entity, 3);
        Assert.True(RpgCombatApi.TryCast(p.Entity, gutStrike, AimContext.None), Caster(p).LastFailReason.ToString());
        Assert.Equal(0, ResourceState.Combo(p.Entity));
    }

    [AtlasScenario]
    public async Task Regen_Should_FollowTheMultiplier_AndComboShouldDecay()
    {
        ITestPlayer p = await JoinAs("priest");
        ResourceState.Set(p.Entity, Pool(p.Entity), 0f);
        await World.Until(() => Mana(p) > 0f, "mana to regenerate", 60);

        SetGlobal("resourceRegenMultiplier", 0f);
        ResourceState.Set(p.Entity, Pool(p.Entity), 0f);
        await World.Ticks(30);
        Assert.Equal(0f, Mana(p));

        ITestPlayer rogue = await JoinAs("rogue");
        SetGlobal("comboDecaySeconds", 1f);
        ResourceState.AddCombo(rogue.Entity, 3);
        await World.Until(() => ResourceState.Combo(rogue.Entity) == 0, "combo points to decay", 120);
    }
}
