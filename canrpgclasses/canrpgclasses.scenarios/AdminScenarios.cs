using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.Attributes;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Content;
using canrpgclasses.Core.Spells;
using canrpgclasses.scenarios.Infrastructure;
using Vintagestory.API.Config;

namespace canrpgclasses.scenarios;

/// <summary>
/// What the admin panel's editors run on the server, step by step (the packet handlers are private): balance
/// edits and their file, the ContentEditGuard checks, content edits, /canrpgattr and the class-change fee.
/// </summary>
[Trait("Category", "E2E")]
public class AdminScenarios : RpgScenarioBase
{
    private const string OverridesFile = "canrpgclasses-balance-overrides.json";
    private const string ContentFile = "canrpgclasses-content.json";

    private static string ModConfigPath(string file) => Path.Combine(GamePaths.ModConfig, file);

    /// <summary>Restores the balance numbers and removes the overrides file this scenario wrote.</summary>
    private void KeepBalanceClean()
    {
        string snapshot = BalanceConfig.Serialize();
        AddCleanup(() =>
        {
            File.Delete(ModConfigPath(OverridesFile));
            BalanceConfig.ApplySerialized(snapshot);
            RebuildRegistries();
        });
    }

    [AtlasScenario]
    public async Task BalanceEdit_Should_ChangeTheSpell_AndWriteTheOverridesFile()
    {
        await Join("balance");
        KeepBalanceClean();

        BalanceConfig.Set(BalanceConfig.BalanceCategory.Spell, "warding_word", "resource", 7f);
        BalanceConfig.SaveOverrides(World.Api);
        RebuildRegistries();

        Assert.Equal(7f, Mod.Spells.Get("canrpgclasses:warding_word")!.Cost.Resource);
        Assert.True(File.Exists(ModConfigPath(OverridesFile)));
    }

    [AtlasScenario]
    public async Task OverridesFile_Should_WinOverTheShippedNumbers_OnLoad()
    {
        await Join("reload");
        KeepBalanceClean();
        BalanceConfig.Set(BalanceConfig.BalanceCategory.Spell, "warding_word", "resource", 7f);
        BalanceConfig.SaveOverrides(World.Api);

        // An unsaved edit is lost; the saved one comes back from the file.
        BalanceConfig.Set(BalanceConfig.BalanceCategory.Spell, "warding_word", "resource", 99f);
        BalanceConfig.LoadOverrides(World.Api);

        Assert.Equal(7f, BalanceConfig.Spell("warding_word").F("resource", -1f));
    }

    [AtlasScenario]
    public async Task ClassEditor_Should_RefuseToRedefineABuiltInClass()
    {
        await Join("classguard");
        var model = new ClassModel { id = "mage", name = "Fake", trees = new() { "One" } };

        string? reason = ContentEditGuard.RejectClass(model, Mod.Classes, Mod.Spells, Mod.Talents);

        Assert.NotNull(reason);
        Assert.Contains("built-in", reason);
    }

    [AtlasScenario]
    public async Task SpellEditor_Should_RefuseBadSpells()
    {
        await Join("spellguard");

        Assert.Contains("built-in", ContentEditGuard.RejectSpell(new SpellModel { id = "canrpgclasses:warding_word" }, Mod.Spells));
        Assert.NotNull(ContentEditGuard.RejectSpell(new SpellModel { id = "bad id!" }, Mod.Spells));
        Assert.NotNull(ContentEditGuard.RejectSpell(new SpellModel { id = "mymod:far", range = 500f }, Mod.Spells));

        var crowded = new SpellModel { id = "mymod:crowded", impacts = new() };
        for (int i = 0; i <= ContentEditGuard.MaxImpacts; i++) crowded.impacts.Add(new ImpactModel { action = "Damage" });
        Assert.NotNull(ContentEditGuard.RejectSpell(crowded, Mod.Spells));

        Assert.Null(ContentEditGuard.RejectSpell(new SpellModel { id = "mymod:fine", range = 10f }, Mod.Spells));
    }

    [AtlasScenario]
    public async Task TreeEditor_Should_RefuseBadTalents()
    {
        await Join("treeguard");
        List<TalentModel> One(TalentModel t) => new() { t };

        Assert.Contains("built-in", ContentEditGuard.Reject("priest", 2,
            One(new TalentModel { id = "priest:deeper_shadow", maxRank = 1 }), Mod.Classes, Mod.Talents));
        Assert.NotNull(ContentEditGuard.Reject("priest", 2,
            One(new TalentModel { id = "priest:too_many_ranks", maxRank = ContentEditGuard.MaxRankCap + 1 }), Mod.Classes, Mod.Talents));
        Assert.NotNull(ContentEditGuard.Reject("priest", 2, One(new TalentModel
        {
            id = "priest:too_strong", maxRank = 1,
            stats = new() { new TalentStatModel { stat = "maxhealthExtraPoints", perRank = ContentEditGuard.MaxStatMagnitude + 1 } }
        }), Mod.Classes, Mod.Talents));
        Assert.NotNull(ContentEditGuard.Reject("priest", 9, One(new TalentModel { id = "priest:no_tree", maxRank = 1 }), Mod.Classes, Mod.Talents));

        Assert.Null(ContentEditGuard.Reject("priest", 2, One(new TalentModel { id = "priest:fine", maxRank = 2 }), Mod.Classes, Mod.Talents));
    }

    [AtlasScenario]
    public async Task ContentEdit_Should_AddACastableSpell()
    {
        ITestPlayer p = await JoinAs("priest");
        string snapshot = ContentStore.Serialize();
        AddCleanup(() =>
        {
            ContentStore.Adopt(snapshot);
            File.Delete(ModConfigPath(ContentFile));
            RebuildRegistries();
        });
        var spell = new SpellModel
        {
            id = "mymod:editor_ward", name = "Editor Ward", school = "Holy", castMode = "Instant",
            resourceCost = 5, cooldown = 3, target = new TargetModel { type = "Caster" },
            impacts = new() { new ImpactModel { action = "Shield", shieldCoefficient = 1, shieldSeconds = 5 } }
        };
        Assert.Null(ContentEditGuard.RejectSpell(spell, Mod.Spells));

        ContentStore.ReplaceSpell(spell);
        ContentStore.Save(World.Api);
        RebuildRegistries();

        Assert.True(Mod.Spells.TryGet("mymod:editor_ward", out _));
        FillResource(p);
        Assert.True(RpgCombatApi.TryCast(p.Entity, "mymod:editor_ward", AimContext.None), Caster(p).LastFailReason.ToString());
        Assert.True(File.Exists(ModConfigPath(ContentFile)));
    }

    [AtlasScenario]
    public async Task AttrCommand_Should_RaiseTheAttribute_AndResetIt()
    {
        ITestPlayer p = await JoinAs("warrior");
        AttributeDef strength = RpgAttributes.Get("strength")!;
        float before = strength.Points(p.Entity);
        float payoutBefore = p.Entity.Stats.GetBlended("meleeWeaponsDamage");

        CommandResult add = await p.ExecuteCommand("/canrpgattr strength 10");
        Assert.True(add.Ok, "/canrpgattr: " + add.Message);
        AddCleanup(() => p.Entity.Stats.Remove("strength", RpgAttributes.AdminSource));
        Assert.Equal(before + 10f, strength.Points(p.Entity), 2);
        Assert.True(p.Entity.Stats.GetBlended("meleeWeaponsDamage") > payoutBefore, "the points paid out nothing");

        CommandResult reset = await p.ExecuteCommand("/canrpgattr reset strength");
        Assert.True(reset.Ok, "/canrpgattr reset: " + reset.Message);
        Assert.Equal(before, strength.Points(p.Entity), 2);
    }

    [AtlasScenario]
    public async Task AttrCommand_Should_ClampTheAdminBonus()
    {
        ITestPlayer p = await JoinAs("warrior");
        AddCleanup(() => p.Entity.Stats.Remove("strength", RpgAttributes.AdminSource));

        await p.ExecuteCommand("/canrpgattr strength 999999");

        Assert.Equal(ContentEditGuard.MaxBaseStatMagnitude, p.Entity.StatSource("strength", RpgAttributes.AdminSource));
    }

    [AtlasScenario]
    public async Task Softcap_Should_HalveThePointsPastIt()
    {
        ITestPlayer p = await JoinAs("warrior");
        AttributeDef strength = RpgAttributes.Get("strength")!;
        Assert.True(strength.Softcap > 0f && strength.SoftcapFactor < 1f, "precondition: strength has a softcap");
        AddCleanup(() => p.Entity.Stats.Remove("strength", RpgAttributes.AdminSource));

        float raw = strength.Points(p.Entity);
        await p.ExecuteCommand(FormattableString.Invariant($"/canrpgattr strength {strength.Softcap + 100f - raw}"));

        Assert.Equal(strength.Softcap + 100f, strength.Points(p.Entity), 1);
        Assert.Equal(strength.Softcap + 100f * strength.SoftcapFactor, strength.EffectivePoints(p.Entity), 1);
    }

    [AtlasScenario]
    public async Task FeeMode_Should_FallBackToAdminOnly_WithoutAnEconomy()
    {
        SetGlobal("classChangeReselectMode", (int)ReselectMode.Fee);
        ITestPlayer p = await JoinAs("warrior");
        if (Core.Economy.EconomyBridge.Available(World.Api)) return; // with caneconomy the fee applies; nothing to check

        Assert.Equal(ReselectMode.AdminOnly, ClassChange.Mode(World.Api));
        Assert.False(ClassChange.TryChange(p.Player, "mage", adminOverride: false, out string reason));
        Assert.Equal("admin-only", reason);
    }
}
