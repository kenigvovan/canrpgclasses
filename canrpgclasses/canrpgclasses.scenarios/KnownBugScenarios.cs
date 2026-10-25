using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.Resources;
using canrpgclasses.Priest;
using canrpgclasses.scenarios.Infrastructure;
using effectshud.src;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.scenarios;

/// <summary>
/// Bugs found while writing the wiki. These fail until the bug is fixed; filter them out with
/// <c>Category!=KnownBug</c>.
/// </summary>
[Trait("Category", "KnownBug")]
public class KnownBugScenarios : RpgScenarioBase
{
    /// <summary>PriestDisciplineTalents.cs: MirrorShield returns early unless Source == Player, so mob swings never reflect.</summary>
    [AtlasScenario]
    public async Task MirrorShield_Should_ReflectAMobsHit()
    {
        ITestPlayer p = await JoinAs("priest");
        Survival(p);
        FillResource(p);
        SetRank(p, "priest:mirror_shield", 3);
        Assert.True(RpgCombatApi.TryCast(p.Entity, "canrpgclasses:warding_word", AimContext.None), Caster(p).LastFailReason.ToString());
        Entity mob = SpawnTarget(p);

        float mobLoss = Loss(mob, () => Hit(p.Entity, 2f, mob));

        Assert.True(mobLoss > 0f, "a shielded priest with Mirror Shield reflected nothing at a mob");
    }

    /// <summary>PriestEffects.cs: SoothingPrayerEffect reads BalanceConfig.Spell("renew") instead of "soothing_prayer".</summary>
    [AtlasScenario]
    public async Task SoothingPrayer_Should_UseItsOwnBalanceEntry()
    {
        ITestPlayer p = await JoinAs("priest");
        const string prayer = "canrpgclasses:soothing_prayer";

        FillResource(p);
        Assert.True(RpgCombatApi.TryCast(p.Entity, prayer, AimContext.None), Caster(p).LastFailReason.ToString());
        float shipped = PerTick(p.Entity);

        SetSpellBalance("soothing_prayer", "coeffPerTick", 2f); // shipped 0.5, so 4x
        p.Entity.GetBehavior<EBEffectsAffected>()!.TryGetEffect(PriestEffectIds.SoothingPrayer, out Effect old);
        old.SetExpiryImmediately();
        await World.Ticks(40);
        ResetCooldowns(p, prayer);
        FillResource(p);
        Assert.True(RpgCombatApi.TryCast(p.Entity, prayer, AimContext.None), Caster(p).LastFailReason.ToString());

        Assert.InRange(PerTick(p.Entity) / shipped, 3.9f, 4.1f);
    }

    private static float PerTick(Entity e)
    {
        Assert.True(e.GetBehavior<EBEffectsAffected>()!.TryGetEffect(PriestEffectIds.SoothingPrayer, out Effect effect), "no Soothing Prayer");
        return Assert.IsType<SoothingPrayerEffect>(effect).perTick;
    }

    /// <summary>EBResources compares ElapsedMilliseconds with the saved ComboLastMs; after a server restart the clock
    /// starts over, so a stamp from the last session is "in the future" and combo points never decay.</summary>
    [AtlasScenario]
    public async Task ComboDecay_Should_Survive_AStaleStamp()
    {
        SetGlobal("comboDecaySeconds", 1f);
        ITestPlayer p = await JoinAs("rogue");
        ResourceState.AddCombo(p.Entity, 3);
        // What a restart leaves behind: the stamp of a session that ran much longer than this one.
        p.Entity.WatchedAttributes.SetLong(ResourceState.ComboLastMsKey, p.Entity.World.ElapsedMilliseconds + 3_600_000L);

        await World.Until(() => ResourceState.Combo(p.Entity) == 0, "combo points to decay", 120);
    }
}
