using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.HarmonyPatches;
using canrpgclasses.Core.Resources;
using canrpgclasses.Hunter;
using canrpgclasses.Mage;
using canrpgclasses.Priest;
using canrpgclasses.Shaman;
using canrpgclasses.scenarios.Infrastructure;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.scenarios;

/// <summary>Each class's own mechanic, two scenarios per class.</summary>
[Trait("Category", "E2E")]
public class ClassMechanicScenarios : RpgScenarioBase
{
    private static float Primary(ITestPlayer p) => ResourceState.Get(p.Entity, Pool(p.Entity));

    private bool Cast(ITestPlayer p, string spellId, AimContext? aim = null)
    {
        bool ok = RpgCombatApi.TryCast(p.Entity, "canrpgclasses:" + spellId, aim ?? AimContext.None);
        Assert.True(ok, $"{spellId}: {Caster(p).LastFailReason}");
        return ok;
    }

    private string ActiveAura(ITestPlayer p) => p.Entity.WatchedAttributes.GetString(AttrKeys.ActiveAura, "");

    // ---- Warrior ---------------------------------------------------------------------------

    [AtlasScenario]
    public async Task Warrior_Rage_Should_BuildFromSwings_AndDecayOutOfCombat()
    {
        SetGlobal("combatTagSeconds", 0.5f);
        ITestPlayer p = await JoinAs("warrior");
        Entity target = SpawnTarget(p);
        Assert.Equal(0f, Primary(p));

        Hit(target, 1f, p.Entity);
        float built = Primary(p);
        Assert.True(built > 0f, "a white swing built no rage");

        await World.Until(() => Primary(p) < built, "rage to decay out of combat", 150);
    }

    [AtlasScenario]
    public async Task Warrior_StanceSwitch_Should_GiveRage_OnlyInCombat()
    {
        ITestPlayer p = await JoinAs("warrior");
        Entity target = SpawnTarget(p);

        Cast(p, "offensive_stance");
        Assert.Equal(0f, Primary(p));
        Assert.Equal("canrpgclasses:offensive_stance", ActiveAura(p));

        Hit(target, 1f, p.Entity); // tags the warrior in combat
        Assert.True(StunPatches.IsInCombat(p.Entity), "precondition: in combat");
        float before = Primary(p);
        ResetCooldowns(p, "canrpgclasses:guarded_stance");
        Cast(p, "guarded_stance");

        Assert.Equal("canrpgclasses:guarded_stance", ActiveAura(p));
        Assert.True(Primary(p) > before, "switching stance in combat gave no rage");
    }

    // ---- Rogue -----------------------------------------------------------------------------

    [AtlasScenario]
    public async Task Rogue_Stealth_Should_MakeInvisible_OutOfCombat()
    {
        ITestPlayer p = await JoinAs("rogue");

        Cast(p, "stealth");

        await World.Until(() => RpgCombatApi.HasEffect(p.Entity, "invisibility"), "the rogue to turn invisible", 30);
    }

    [AtlasScenario]
    public async Task Rogue_Regroup_Should_ResetTheEscapes()
    {
        ITestPlayer p = await JoinAs("rogue");
        FillResource(p);
        Cast(p, "slip_away");
        Assert.True(RpgCombatApi.IsOnCooldown(p.Entity, "canrpgclasses:slip_away"), "precondition: Slip Away on cooldown");

        ResetCooldowns(p, "canrpgclasses:regroup");
        Cast(p, "regroup");

        Assert.False(RpgCombatApi.IsOnCooldown(p.Entity, "canrpgclasses:slip_away"));
    }

    // ---- Hunter ----------------------------------------------------------------------------

    private async Task<EntityAgent> SummonWolf(ITestPlayer p)
    {
        Cast(p, "summon_pet");
        await CastToEnd(p);
        await World.Until(() => HunterPetSystem.GetPet(p.Entity) != null, "the wolf to appear", 60);
        EntityAgent wolf = HunterPetSystem.GetPet(p.Entity)!;
        AddCleanup(() => { if (wolf.Alive) wolf.Die(EnumDespawnReason.Removed); });
        return wolf;
    }

    [AtlasScenario]
    public async Task Hunter_SummonPet_Should_SpawnAWolf()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await SummonWolf(p);

        Assert.True(HunterPetSystem.IsPet(wolf));
        Assert.True(wolf.Alive);
        Assert.True(RpgCombatApi.IsAlly(p.Entity, wolf), "the wolf must be its owner's ally");
    }

    [AtlasScenario]
    public async Task Hunter_PetDeath_Should_LockTheSummon()
    {
        ITestPlayer p = await JoinAs("hunter");
        EntityAgent wolf = await SummonWolf(p);
        string key = RpgCombatApi.GetSpell(World.Api, "summon_pet")!.CooldownKey;
        Cooldowns(p).ClearCooldown(key);

        wolf.Die(EnumDespawnReason.Death);
        await World.Ticks(2);

        Assert.True(Cooldowns(p).IsOnCooldown(key), "a dead wolf must lock the summon");
        Assert.True(Cooldowns(p).RemainingSeconds(key) > 10f, "the lockout is the long death timer, not the cast cooldown");
    }

    // ---- Priest ----------------------------------------------------------------------------

    [AtlasScenario]
    public async Task Priest_MindShock_Should_GrantAnOrb_AndPlagueShouldSpendThem()
    {
        ITestPlayer p = await JoinAs("priest");
        Entity target = SpawnTarget(p);
        FillResource(p);
        Cast(p, "mind_shock", AimAt(target));
        await CastToEnd(p);
        Assert.True(EffectTier(p.Entity, PriestEffectIds.ShadowOrbs) >= 1, "Mind Shock granted no Shadow Orb");

        FillResource(p);
        ResetCooldowns(p, "canrpgclasses:consuming_plague");
        Cast(p, "consuming_plague", AimAt(target));

        await World.Until(() => EffectTier(p.Entity, PriestEffectIds.ShadowOrbs) < 1, "the orbs to be spent", 60);
    }

    [AtlasScenario]
    public async Task Priest_Orbs_Should_StopAtTheCap()
    {
        ITestPlayer p = await JoinAs("priest");

        ShadowOrbs.Grant(p.Entity, 10);

        Assert.Equal(ShadowOrbs.Cap, EffectTier(p.Entity, PriestEffectIds.ShadowOrbs));
    }

    // ---- Mage ------------------------------------------------------------------------------

    [AtlasScenario]
    public async Task Mage_MysticBlast_Should_BuildCharges_ThatRaiseItsCost()
    {
        SetGlobal("resourceRegenMultiplier", 0f);
        ITestPlayer p = await JoinAs("mage");
        Entity target = SpawnTarget(p);

        FillResource(p);
        float before = Primary(p);
        Cast(p, "mystic_blast", AimAt(target));
        await CastToEnd(p);
        float firstCost = before - Primary(p);
        Assert.Equal(1, EffectTier(p.Entity, MageEffectIds.MysticCharges));

        FillResource(p);
        before = Primary(p);
        ResetCooldowns(p, "canrpgclasses:mystic_blast");
        Cast(p, "mystic_blast", AimAt(target));
        await CastToEnd(p);

        Assert.True(before - Primary(p) > firstCost, "a charge must raise the next blast's cost");
        Assert.Equal(2, EffectTier(p.Entity, MageEffectIds.MysticCharges));
    }

    [AtlasScenario]
    public async Task Mage_Guards_Should_BeExclusive()
    {
        ITestPlayer p = await JoinAs("mage");
        FillResource(p);
        AddCleanup(() => p.Entity.WatchedAttributes.SetString(AttrKeys.ActiveAura, ""));

        Cast(p, "mystic_guard");
        Assert.Equal("canrpgclasses:mystic_guard", ActiveAura(p));

        ResetCooldowns(p, "canrpgclasses:frost_guard");
        Cast(p, "frost_guard");
        Assert.Equal("canrpgclasses:frost_guard", ActiveAura(p));
    }

    // ---- Shaman ----------------------------------------------------------------------------

    private Entity? Totem(ITestPlayer p) =>
        World.Api.World.GetEntityById(p.Entity.WatchedAttributes.GetLong(ShamanTotemSystem.OwnerTotemIdKey, 0));

    [AtlasScenario]
    public async Task Shaman_NewTotem_Should_ReplaceTheOld()
    {
        ITestPlayer p = await JoinAs("shaman");
        FillResource(p);
        Cast(p, "ember_totem");
        await World.Until(() => Totem(p) != null, "the first totem", 30);
        Entity first = Totem(p)!;
        AddCleanup(() => { if (Totem(p) is { Alive: true } t) t.Die(EnumDespawnReason.Removed); });

        FillResource(p);
        ResetCooldowns(p, "canrpgclasses:ember_totem");
        Cast(p, "ember_totem");
        await World.Until(() => Totem(p) is { } t && t != first, "the second totem", 30);

        await World.Until(() => !first.Alive, "the first totem to go", 30);
    }

    [AtlasScenario]
    public async Task Shaman_Totem_Should_BeKillable()
    {
        ITestPlayer p = await JoinAs("shaman");
        FillResource(p);
        Cast(p, "ember_totem");
        await World.Until(() => Totem(p) != null, "the totem", 30);
        Entity totem = Totem(p)!;
        Entity attacker = SpawnTarget(p);

        Hit(totem, 50f, attacker);

        await World.Until(() => !totem.Alive, "the totem to die", 30);
    }

    // ---- Druid -----------------------------------------------------------------------------

    [AtlasScenario]
    public async Task Druid_Forms_Should_GateTheirAbilities()
    {
        ITestPlayer p = await JoinAs("druid");
        Entity target = SpawnTarget(p);
        FillResource(p);
        AddCleanup(() => p.Entity.WatchedAttributes.SetString(AttrKeys.ActiveAura, ""));

        Assert.False(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:flesh_tear", AimAt(target)));
        Assert.Equal(CastFailReason.WrongForm, Caster(p).LastFailReason);

        Cast(p, "feline_form");
        ResetCooldowns(p, "canrpgclasses:flesh_tear");
        Cast(p, "flesh_tear", AimAt(target));
        Assert.Equal(1, ResourceState.Combo(p.Entity));

        ResetCooldowns(p, "canrpgclasses:warding_word");
        Assert.False(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:warding_word", AimContext.None));
        Assert.Equal(CastFailReason.FormLocked, Caster(p).LastFailReason);

        // Shift back out, which also restores the player model.
        ResetCooldowns(p, "canrpgclasses:feline_form");
        Cast(p, "feline_form");
        Assert.Equal("", ActiveAura(p));
    }

    [AtlasScenario]
    public async Task Druid_Rage_Should_BuildOnlyInBearForm()
    {
        ITestPlayer p = await JoinAs("druid");
        Entity target = SpawnTarget(p);
        FillResource(p);
        var rage = ResourceState.SecondaryPool("rage") ?? throw new InvalidOperationException("no rage pool registered");
        AddCleanup(() => p.Entity.WatchedAttributes.SetString(AttrKeys.ActiveAura, ""));

        Hit(target, 1f, p.Entity);
        Assert.Equal(0f, ResourceState.Get(p.Entity, rage));

        Cast(p, "ursine_form");
        await World.Ticks(25); // past the swing rate limit
        Hit(target, 1f, p.Entity);
        Assert.True(ResourceState.Get(p.Entity, rage) > 0f, "a bear's swing built no rage");

        ResetCooldowns(p, "canrpgclasses:ursine_form");
        Cast(p, "ursine_form");
    }

    // ---- Paladin ---------------------------------------------------------------------------

    [AtlasScenario]
    public async Task Paladin_Devotion_Should_LandWithTheEmpoweredHit()
    {
        ITestPlayer p = await JoinAs("paladin");
        Entity target = SpawnTarget(p);
        FillResource(p);

        Cast(p, "zealots_strike");
        Assert.Equal(0, ResourceState.Combo(p.Entity));

        Hit(target, 1f, p.Entity);
        Assert.Equal(1, ResourceState.Combo(p.Entity));
    }

    [AtlasScenario]
    public async Task Paladin_Verdict_Should_SpendAllDevotion()
    {
        ITestPlayer p = await JoinAs("paladin");
        FillResource(p);

        Assert.False(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:righteous_verdict", AimContext.None));
        Assert.Equal(CastFailReason.NoCombo, Caster(p).LastFailReason);

        ResourceState.AddCombo(p.Entity, 3);
        Cast(p, "righteous_verdict");
        Assert.Equal(0, ResourceState.Combo(p.Entity));
    }
}
