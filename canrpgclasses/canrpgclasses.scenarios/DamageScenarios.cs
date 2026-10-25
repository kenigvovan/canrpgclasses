using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using canrpgclasses.Priest;
using canrpgclasses.scenarios.Infrastructure;
using effectshud.src;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.scenarios;

/// <summary>Spell damage, mitigation order and caps in the damage patch, healing, snapshot DoTs and modifiers.</summary>
[Trait("Category", "E2E")]
public class DamageScenarios : RpgScenarioBase
{
    // Inert unless the attacker carries BoostMarker; DamageModifiers has no unregister, so it stays registered.
    private const string BoostMarker = "scenarioDamageBoost";
    private static readonly DamageModifier Boost = (Entity victim, Entity? attacker, DamageSource source, ref float damage) =>
    {
        if (attacker?.WatchedAttributes.GetBool(BoostMarker) == true) damage *= 2f;
    };

    [AtlasScenario]
    public async Task SpellDamage_Should_ScaleWithTheCoefficient()
    {
        ITestPlayer p = await JoinAs("priest");
        Entity first = SpawnTarget(p), second = SpawnTarget(p);
        const string mindShock = "canrpgclasses:mind_shock";

        FillResource(p);
        float baseLoss = Health(first);
        Assert.True(RpgCombatApi.TryCast(p.Entity, mindShock, AimAt(first)), Caster(p).LastFailReason.ToString());
        await CastToEnd(p);
        baseLoss -= Health(first);
        Assert.True(baseLoss > 0f, "Mind Shock dealt no damage");

        SetSpellBalance("mind_shock", "coeff", 6f); // twice the shipped 3.0
        ResetCooldowns(p, mindShock);
        FillResource(p);
        float doubleLoss = Health(second);
        Assert.True(RpgCombatApi.TryCast(p.Entity, mindShock, AimAt(second)), Caster(p).LastFailReason.ToString());
        await CastToEnd(p);
        doubleLoss -= Health(second);

        Assert.InRange(doubleLoss / baseLoss, 1.9f, 2.1f);
    }

    [AtlasScenario]
    public async Task MagicResist_Should_StopAtItsCap()
    {
        ITestPlayer p = await JoinAs("priest");
        Entity plain = SpawnTarget(p), resistant = SpawnTarget(p);
        resistant.Stats.Set(StatKeys.MagicResist, "scenario", 5f); // far past the 60% cap

        float full = Loss(plain, () => SpellHit(plain, SpellSchool.Shadow, 10f, p.Entity));
        float resisted = Loss(resistant, () => SpellHit(resistant, SpellSchool.Shadow, 10f, p.Entity));

        Assert.InRange(resisted / full, 0.39f, 0.41f);
    }

    [AtlasScenario]
    public async Task MagicResist_Should_NotTouchPhysicalHits()
    {
        ITestPlayer p = await JoinAs("warrior");
        Entity plain = SpawnTarget(p), resistant = SpawnTarget(p);
        resistant.Stats.Set(StatKeys.MagicResist, "scenario", 5f);

        float full = Loss(plain, () => Hit(plain, 10f, p.Entity));
        float resisted = Loss(resistant, () => Hit(resistant, 10f, p.Entity));

        Assert.Equal(full, resisted, 2);
    }

    [AtlasScenario]
    public async Task DamageReduction_Should_StopAtItsCap()
    {
        ITestPlayer p = await JoinAs("warrior");
        Entity plain = SpawnTarget(p), armored = SpawnTarget(p);
        armored.Stats.Set(StatKeys.DamageReduction, "scenario", 5f); // far past the 50% cap

        float full = Loss(plain, () => SpellHit(plain, SpellSchool.Fire, 10f, p.Entity));
        float reduced = Loss(armored, () => SpellHit(armored, SpellSchool.Fire, 10f, p.Entity));

        Assert.InRange(reduced / full, 0.49f, 0.51f);
    }

    [AtlasScenario]
    public async Task Heal_Should_RestoreHealth_UpToTheMaximum()
    {
        ITestPlayer p = await JoinAs("paladin");
        Survival(p);
        FillResource(p);
        Hit(p.Entity, 5f);
        float hurt = Health(p.Entity);
        Assert.True(hurt < MaxHealth(p.Entity), "precondition: the paladin is hurt");

        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:blessed_light", AimContext.None), Caster(p).LastFailReason.ToString());
        await CastToEnd(p);

        Assert.True(Health(p.Entity) > hurt, "Blessed Light healed nothing");
        Assert.True(Health(p.Entity) <= MaxHealth(p.Entity));
    }

    [AtlasScenario]
    public async Task Dot_Should_KeepTheSpellPowerItWasCastWith()
    {
        ITestPlayer p = await JoinAs("priest");
        Entity target = SpawnTarget(p);
        const string brand = "canrpgclasses:shadow_brand";
        FillResource(p);
        Assert.True(RpgCombatApi.TryCast(p.Entity, brand, AimAt(target)), Caster(p).LastFailReason.ToString());
        float snapshot = PerTick(target);
        Assert.True(snapshot > 0f);

        string shadowPower = StatKeys.SpellpowerFor(SpellSchool.Shadow);
        p.Entity.Stats.Set(shadowPower, "scenario", 1f);
        AddCleanup(() => p.Entity.Stats.Remove(shadowPower, "scenario"));
        await World.Ticks(2);
        Assert.Equal(snapshot, PerTick(target));

        // A re-cast takes the new snapshot.
        ResetCooldowns(p, brand);
        FillResource(p);
        Assert.True(RpgCombatApi.TryCast(p.Entity, brand, AimAt(target)), Caster(p).LastFailReason.ToString());
        Assert.True(PerTick(target) > snapshot);
    }

    private static float PerTick(Entity target)
    {
        var effects = target.GetBehavior<EBEffectsAffected>() ?? throw new InvalidOperationException("no effects on the target");
        Assert.True(effects.TryGetEffect(PriestEffectIds.ShadowBrand, out Effect effect), "no Shadow Brand on the target");
        return Assert.IsAssignableFrom<SchoolDamageDotEffect>(effect).perTick;
    }

    [AtlasScenario]
    public async Task Evasion_Should_DodgePhysicalHits_Only()
    {
        SetSpellBalance("evasion", "chance", 1f);
        ITestPlayer p = await JoinAs("rogue");
        Survival(p);
        FillResource(p);
        Entity attacker = SpawnTarget(p);
        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:evasion", AimContext.None), Caster(p).LastFailReason.ToString());

        Assert.Equal(0f, Loss(p.Entity, () => Hit(p.Entity, 2f, attacker)));
        Assert.True(Loss(p.Entity, () => SpellHit(p.Entity, SpellSchool.Fire, 2f, attacker)) > 0f, "spells are not dodged");
    }

    [AtlasScenario]
    public async Task Empower_Should_AddToTheNextSwing_Once()
    {
        ITestPlayer p = await JoinAs("rogue");
        FillResource(p);
        Entity target = SpawnTarget(p);
        float plain = Loss(target, () => Hit(target, 1f, p.Entity));

        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:vicious_strike", AimContext.None), Caster(p).LastFailReason.ToString());
        Assert.True(p.Entity.WatchedAttributes.HasAttribute(SpellExecutor.EmpowerDamageKey), "precondition: armed");

        float empowered = Loss(target, () => Hit(target, 1f, p.Entity));
        Assert.True(empowered > plain, "the empowered swing dealt no bonus");
        Assert.False(p.Entity.WatchedAttributes.HasAttribute(SpellExecutor.EmpowerDamageKey));
        Assert.Equal(plain, Loss(target, () => Hit(target, 1f, p.Entity)), 2);
    }

    [AtlasScenario]
    public async Task TalentDamageModifier_Should_ApplyOnlyWithRanks()
    {
        ITestPlayer p = await JoinAs("mage", level: 10);
        Entity rootedA = SpawnTarget(p), rootedB = SpawnTarget(p);
        Core.Control.ControlState.Apply(rootedA, Core.Control.ControlType.Root, 30f, p.Entity);
        Core.Control.ControlState.Apply(rootedB, Core.Control.ControlType.Root, 30f, p.Entity);

        float without = Loss(rootedA, () => SpellHit(rootedA, SpellSchool.Frost, 10f, p.Entity));

        // Icebreaker sits behind Freezing Burst; only its rank matters here, so it is written directly.
        var ranks = p.Entity.WatchedAttributes.GetOrAddTreeAttribute(TalentState.RanksKey);
        ranks.SetInt("mage:icebreaker", 3);
        AddCleanup(() => ranks.RemoveAttribute("mage:icebreaker"));
        float with = Loss(rootedB, () => SpellHit(rootedB, SpellSchool.Frost, 10f, p.Entity));

        Assert.InRange(with / without, 1.14f, 1.16f); // +5% per rank
    }

    [AtlasScenario]
    public async Task PersistentModifier_Should_Apply()
    {
        DamageModifiers.RegisterPersistent(Boost);
        ITestPlayer p = await JoinAs("warrior");
        Entity a = SpawnTarget(p), b = SpawnTarget(p);

        float plain = Loss(a, () => Hit(a, 2f, p.Entity));
        p.Entity.WatchedAttributes.SetBool(BoostMarker, true);
        AddCleanup(() => p.Entity.WatchedAttributes.RemoveAttribute(BoostMarker));
        float boosted = Loss(b, () => Hit(b, 2f, p.Entity));

        Assert.InRange(boosted / plain, 1.95f, 2.05f);
    }
}
