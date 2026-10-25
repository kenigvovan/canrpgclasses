using System.Reflection;
using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses;
using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Progression;
using canrpgclasses.Core.Resources;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace canrpgclasses.scenarios.Infrastructure;

/// <summary>
/// Shared ground for the mod's scenarios. One server per class and no rollback between scenarios, so every
/// scenario joins its own player and undoes its balance edits in <see cref="Dispose"/>.
/// </summary>
public abstract class RpgScenarioBase : AtlasScenarioBase, IDisposable
{
    private static int lastPlayer;

    // The server holds 16 clients, so players of finished scenarios leave before the next join; Dispose runs
    // off the game thread and cannot do it. Keyed by the server API: Atlas makes a new session per scenario.
    private static readonly Dictionary<Type, (ICoreServerAPI Server, List<ITestPlayer> Players)> leftOnline = new();

    private readonly List<ITestPlayer> players = new();
    private readonly List<Action> cleanups = new();

    protected canrpgclassesModSystem Mod =>
        canrpgclassesModSystem.For(World.Api) ?? throw new InvalidOperationException("canrpgclasses is not loaded");

    protected async Task<ITestPlayer> Join(string name)
    {
        await DisconnectEarlierPlayers();
        // A random spawn spot in an unloaded chunk can race the join request and never complete it.
        World.Api.World.Config.SetString("spawnRadius", "0");
        ITestPlayer player = await World.JoinPlayer(name + Interlocked.Increment(ref lastPlayer));
        players.Add(player);
        return player;
    }

    /// <summary>Joins under exactly <paramref name="name"/>, for a rejoin of a player who left.</summary>
    protected async Task<ITestPlayer> JoinExact(string name)
    {
        World.Api.World.Config.SetString("spawnRadius", "0");
        ITestPlayer player = await World.JoinPlayer(name);
        players.Add(player);
        return player;
    }

    /// <summary>Disconnects the player and waits until the server has dropped it.</summary>
    protected async Task Leave(ITestPlayer p)
    {
        p.Player.Disconnect();
        await World.Until(() => !p.IsConnected, "the player to leave", 100);
    }

    /// <summary>Puts the players in one canparty party; disbanded on Dispose.</summary>
    protected void Party(ITestPlayer leader, params ITestPlayer[] members)
    {
        var manager = World.Api.ModLoader.GetModSystem<canparty.canpartyModSystem>()?.Manager
            ?? throw new InvalidOperationException("canparty is not loaded");
        Assert.True(manager.CreateParty(leader.Player), "precondition: party created");
        Assert.True(manager.GetPlayerParty(leader.Player, out canparty.PlayerParty party), "precondition: leader in a party");
        foreach (ITestPlayer m in members)
            Assert.True(manager.AddIntoParty(m.Player, party), "precondition: member added to the party");
        cleanups.Add(() => manager.DisbandParty(party));
    }

    /// <summary>Joins a player who has picked <paramref name="classId"/> and is at <paramref name="level"/>.</summary>
    protected async Task<ITestPlayer> JoinAs(string classId, int level = 1)
    {
        ITestPlayer player = await Join(classId);
        ClassChange.Commit(player.Entity, classId);
        Progression(player).SetLevel(level);
        return player;
    }

    private async Task DisconnectEarlierPlayers()
    {
        List<ITestPlayer> stale;
        lock (leftOnline)
        {
            if (!leftOnline.Remove(GetType(), out var left)) return;
            if (!ReferenceEquals(left.Server, World.Api)) return;
            stale = left.Players;
        }

        foreach (ITestPlayer p in stale)
        {
            if (p.IsConnected) p.Player.Disconnect();
        }
        await World.Until(() => stale.All(p => !p.IsConnected), "players of earlier scenarios to leave", 100);
    }

    // ---- behaviors -------------------------------------------------------------------------

    protected static EBProgression Progression(ITestPlayer p) =>
        p.Entity.GetBehavior<EBProgression>() ?? throw new InvalidOperationException("no EBProgression on the player");

    protected static EBTalents Talents(ITestPlayer p) =>
        p.Entity.GetBehavior<EBTalents>() ?? throw new InvalidOperationException("no EBTalents on the player");

    protected static EBSpellCaster Caster(ITestPlayer p) =>
        p.Entity.GetBehavior<EBSpellCaster>() ?? throw new InvalidOperationException("no EBSpellCaster on the player");

    protected static EBSpellCooldowns Cooldowns(ITestPlayer p) =>
        p.Entity.GetBehavior<EBSpellCooldowns>() ?? throw new InvalidOperationException("no EBSpellCooldowns on the player");

    protected static ResourcePoolDef Pool(Entity e) =>
        ResourceState.PrimaryPool(e) ?? throw new InvalidOperationException("the player's class has no resource");

    /// <summary>Whether a stat carries a modifier under <paramref name="source"/>.</summary>
    protected static bool HasSource(Entity e, string stat, string source)
    {
        try { return e.Stats[stat].ValuesByKey.ContainsKey(source); }
        catch (KeyNotFoundException) { return false; }
    }

    /// <summary>The effectshud tier of an effect on the entity, -1 when absent.</summary>
    protected static int EffectTier(Entity e, string effectId) =>
        e.GetBehavior<effectshud.src.EBEffectsAffected>()?.GetEffectTier(effectId) ?? -1;

    /// <summary>Writes a talent rank directly, for a talent whose prerequisites don't matter to the scenario.</summary>
    protected void SetRank(ITestPlayer p, string talentId, int rank)
    {
        var ranks = p.Entity.WatchedAttributes.GetOrAddTreeAttribute(TalentState.RanksKey);
        ranks.SetInt(talentId, rank);
        cleanups.Add(() => ranks.RemoveAttribute(talentId));
    }

    protected static void FillResource(ITestPlayer p)
    {
        var pool = Pool(p.Entity);
        ResourceState.Set(p.Entity, pool, ResourceState.EffectiveMax(p.Entity, pool));
    }

    // ---- combat ----------------------------------------------------------------------------

    /// <summary>Test players join in creative, where they take no damage.</summary>
    protected static void Survival(ITestPlayer p)
    {
        p.Player.WorldData.CurrentGameMode = EnumGameMode.Survival;
        p.Player.BroadcastPlayerData();
    }

    /// <summary>A plain melee hit from <paramref name="attacker"/>, through the mod's damage patch. Ignores the
    /// victim's hit cooldown, so several hits in one tick all land.</summary>
    protected static bool Hit(Entity victim, float damage, Entity? attacker = null) =>
        victim.ReceiveDamage(new DamageSource
        {
            Source = attacker is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity,
            SourceEntity = attacker,
            CauseEntity = attacker,
            Type = EnumDamageType.BluntAttack,
            IgnoreInvFrames = true
        }, damage);

    /// <summary>A spell hit of <paramref name="school"/>, as SpellExecutor deals it.</summary>
    protected static bool SpellHit(Entity victim, SpellSchool school, float damage, Entity? attacker = null) =>
        victim.ReceiveDamage(new CanrpgDamageSource
        {
            Source = EnumDamageSource.Entity,
            SourceEntity = attacker,
            CauseEntity = attacker,
            School = school,
            Type = DamageSchools.For(school).EngineType,
            IgnoreInvFrames = true
        }, damage);

    protected static float Health(Entity e) =>
        e.WatchedAttributes.GetTreeAttribute("health")?.GetFloat("currenthealth", 0f) ?? 0f;

    protected static float MaxHealth(Entity e) =>
        e.WatchedAttributes.GetTreeAttribute("health")?.GetFloat("maxhealth", 0f) ?? 0f;

    /// <summary>Health lost to one hit.</summary>
    protected static float Loss(Entity victim, Action hit)
    {
        float before = Health(victim);
        hit();
        return before - Health(victim);
    }

    /// <summary>Aims a spell straight at <paramref name="target"/>, the way an AI caster does.</summary>
    protected static AimContext AimAt(Entity target) => new() { TargetEntity = target, AimAtTarget = true };

    /// <summary>Waits for a Charge cast or a channel to end, by completion or interruption.</summary>
    protected Task CastToEnd(ITestPlayer p, int timeoutTicks = 200) =>
        World.Until(() => !Caster(p).IsCasting, "the cast to end", timeoutTicks);

    /// <summary>Clears the spell's cooldown (its shared group, if any) and the global cooldown, to cast it again at once.</summary>
    protected void ResetCooldowns(ITestPlayer p, string spellId)
    {
        Cooldowns(p).ClearCooldown(Mod.Spells.Get(spellId)?.CooldownKey ?? spellId);
        Cooldowns(p).ClearCooldown(Spell.GlobalCooldownKey);
    }

    /// <summary>Spawns a creature next to the player; removed on Dispose if still alive.</summary>
    protected Entity SpawnNear(ITestPlayer p, string code)
    {
        Entity e = World.SpawnEntity(code, p.Position.AddCopy(2, 0, 0));
        cleanups.Add(() => { if (e.Alive) e.Die(EnumDespawnReason.Removed); });
        return e;
    }

    /// <summary>A target that survives a series of hits: a creature with its health raised to 1000.</summary>
    protected Entity SpawnTarget(ITestPlayer p)
    {
        Entity e = SpawnNear(p, "game:chicken-rooster");
        var health = e.GetBehavior<EntityBehaviorHealth>() ?? throw new InvalidOperationException("no health behavior");
        health.BaseMaxHealth = 1000f;
        health.UpdateMaxHealth();
        health.Health = 1000f;
        return e;
    }

    // ---- restrictions ----------------------------------------------------------------------

    /// <summary>Replaces the class restrictions (the restrictions file's JSON) until Dispose.</summary>
    protected void UseRestrictions(string json)
    {
        string snapshot = ClassRestrictions.Serialize();
        ClassRestrictions.ApplySerialized(json);
        cleanups.Add(() => ClassRestrictions.ApplySerialized(snapshot));
    }

    // ---- balance ---------------------------------------------------------------------------

    /// <summary>Sets a global balance number (core.json) until Dispose. Globals are read live, no registry rebuild.</summary>
    protected void SetGlobal(string key, float value)
    {
        string snapshot = BalanceConfig.Serialize();
        BalanceConfig.Set(BalanceConfig.BalanceCategory.Global, "", key, value);
        cleanups.Add(() => BalanceConfig.ApplySerialized(snapshot));
    }

    /// <summary>Sets a spell's balance number and rebuilds the registries, whose constructors read it; both are
    /// undone on Dispose.</summary>
    protected void SetSpellBalance(string localId, string key, float value)
    {
        string snapshot = BalanceConfig.Serialize();
        BalanceConfig.Set(BalanceConfig.BalanceCategory.Spell, localId, key, value);
        RebuildRegistries();
        cleanups.Add(() =>
        {
            BalanceConfig.ApplySerialized(snapshot);
            RebuildRegistries();
        });
    }

    /// <summary>What the Balance editor runs after an edit; private in the mod, hence reflection.</summary>
    protected void RebuildRegistries() =>
        (typeof(canrpgclassesModSystem).GetMethod("RebuildRegistries", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(canrpgclassesModSystem), "RebuildRegistries"))
        .Invoke(Mod, new object?[] { null });

    /// <summary>Runs on Dispose, after the scenario.</summary>
    protected void AddCleanup(Action cleanup) => cleanups.Add(cleanup);

    // ---------------------------------------------------------------------------------------

    public void Dispose()
    {
        for (int i = cleanups.Count - 1; i >= 0; i--) cleanups[i]();
        cleanups.Clear();

        if (players.Count > 0)
        {
            lock (leftOnline)
            {
                if (leftOnline.TryGetValue(GetType(), out var left) && ReferenceEquals(left.Server, World.Api))
                    left.Players.AddRange(players);
                else
                    leftOnline[GetType()] = (World.Api, new List<ITestPlayer>(players));
            }
        }
        GC.SuppressFinalize(this);
    }
}
