using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Control;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using canrpgclasses.scenarios.Infrastructure;
using effectshud.src;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace canrpgclasses.scenarios;

/// <summary>Balance bench: every spec at level 25 with a full-tree build hits a level-25 cloth player with each of
/// its spells, and the damage goes to balance-reports/{class}.md. Not a pass/fail test, so it has its own category.</summary>
[Trait("Category", "Balance")]
public class BalanceBenchScenarios : RpgScenarioBase
{
    private const int Level = 25;
    private const string VictimClass = "mage";
    // Vanilla base of a steel-tier swing and a recurve arrow; scaled by the attacker's weapon-damage stats.
    private const float SwingBase = 5f;
    private const float ArrowBase = 6.5f;
    private const int WindowMs = 3000;
    private const int BuffSettleMs = 1200;
    // Rows over these shares of the victim's real max HP get flagged.
    private const float HitWarn = 0.30f;
    private const float BurstWarn = 0.65f;

    private const string Bow = "game:bow-recurve";
    private const string Shield = "game:shield-crude";
    private const string SummonPet = "canrpgclasses:summon_pet";
    private const string RecklessStance = "canrpgclasses:reckless_stance";
    private const string VengefulAura = "canrpgclasses:vengeful_aura";
    private const string MoltenGuard = "canrpgclasses:molten_guard";

    // Buffs are cast before each hit of the full-burst pass; the plain pass measures the hit without them.
    private record Spec(string ClassId, int Tree, string? Setup = null, string? Label = null, string? Gear = null,
        string[]? Buffs = null);

    private static readonly Spec[] Specs =
    {
        new("warrior", 0, Buffs: new[] { RecklessStance }),
        new("warrior", 1, Buffs: new[] { RecklessStance, "canrpgclasses:war_cry", "canrpgclasses:all_out_attack" }),
        new("warrior", 2, Gear: Shield, Buffs: new[] { RecklessStance }),
        new("paladin", 0, Buffs: new[] { VengefulAura }),
        new("paladin", 1, Gear: Shield, Buffs: new[] { VengefulAura }),
        new("paladin", 2, Buffs: new[] { VengefulAura }),
        new("rogue", 0, Setup: "canrpgclasses:stealth", Buffs: new[] { "canrpgclasses:venom_coat" }),
        new("rogue", 1, Buffs: new[] { "canrpgclasses:quickblades" }),
        new("rogue", 2),
        new("hunter", 0, Setup: SummonPet, Gear: Bow), new("hunter", 1, Setup: SummonPet, Gear: Bow),
        new("hunter", 2, Setup: SummonPet, Gear: Bow, Buffs: new[] { "canrpgclasses:beast_fury" }),
        new("mage", 0, Buffs: new[] { MoltenGuard }),
        new("mage", 1, Buffs: new[] { MoltenGuard, "canrpgclasses:icy_veins" }),
        new("mage", 2, Buffs: new[] { MoltenGuard, "canrpgclasses:mystic_surge" }),
        new("priest", 0), new("priest", 1),
        new("priest", 2, Buffs: new[] { "canrpgclasses:shadow_guise" }),
        new("druid", 0),
        new("druid", 1, Setup: "canrpgclasses:feline_form", Label: "Feral (cat)", Buffs: new[] { "canrpgclasses:stalk" }),
        new("druid", 1, Setup: "canrpgclasses:ursine_form", Label: "Feral (bear)"),
        new("druid", 2),
        new("shaman", 0),
        new("shaman", 1, Buffs: new[] { "canrpgclasses:flame_edge", "canrpgclasses:stone_strength_totem" }),
        new("shaman", 2),
    };

    // Buffed equals Damage when the spec has no buffs.
    private record Row(string Spell, float Damage, float Buffed, string Note);

    private const string SwingRow = "white swing";
    private const string BackgroundRow = "setup background";

    private const int BenchTimeoutMs = 20 * 60 * 1000;

    [AtlasScenario(TimeoutMs = BenchTimeoutMs)] public Task Bench_Warrior() => Bench("warrior");
    [AtlasScenario(TimeoutMs = BenchTimeoutMs)] public Task Bench_Paladin() => Bench("paladin");
    [AtlasScenario(TimeoutMs = BenchTimeoutMs)] public Task Bench_Rogue() => Bench("rogue");
    [AtlasScenario(TimeoutMs = BenchTimeoutMs)] public Task Bench_Hunter() => Bench("hunter");
    [AtlasScenario(TimeoutMs = BenchTimeoutMs)] public Task Bench_Mage() => Bench("mage");
    [AtlasScenario(TimeoutMs = BenchTimeoutMs)] public Task Bench_Priest() => Bench("priest");
    [AtlasScenario(TimeoutMs = BenchTimeoutMs)] public Task Bench_Druid() => Bench("druid");
    [AtlasScenario(TimeoutMs = BenchTimeoutMs)] public Task Bench_Shaman() => Bench("shaman");

    private async Task Bench(string classId)
    {
        // The scenario runs inside the game process, so BaseDirectory is the game folder; write next to the source.
        string path = Path.Combine(SourceDir(), "balance-reports", $"{classId}.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        ITestPlayer victim = await JoinAs(VictimClass, Level);
        Survival(victim);
        float refHp = MaxHealth(victim.Entity);
        Assert.True(refHp > 0f, "precondition: the victim has health");
        RaiseHealth(victim.Entity);

        var report = new StringBuilder();
        report.AppendLine($"# Balance bench: {classId}, level {Level}");
        report.AppendLine();
        report.AppendLine($"Target: {VictimClass} level {Level}, real HP **{F(refHp)}** (the target's resists and absorbs are real, no armor). " +
                          $"Damage is counted over {WindowMs / 1000} s after the cast. White swing = {F(SwingBase)} × meleeWeaponsDamage, arrow = {F(ArrowBase)} × rangedWeaponsDamage. " +
                          $"When a spec has a setup (stealth, form, pet), its own damage over the window is subtracted from every spell row. " +
                          $"Full burst = the same hit after the spec's damage buffs. " +
                          $"⚠ hit > {P(HitWarn)} HP, ⚠⚠ rough burst > {P(BurstWarn)} HP.");
        report.AppendLine();

        var top = new List<(string Spec, Row Row)>();
        var burstSummary = new List<(string Spec, float Burst, float Buffed)>();

        foreach (Spec spec in Specs.Where(s => s.ClassId == classId))
        {
            ITestPlayer p = await JoinAs(spec.ClassId, Level);
            Survival(p);
            string tree = Mod.Classes.Get(spec.ClassId)?.TreeName(spec.Tree) ?? spec.Tree.ToString();
            string name = $"{spec.ClassId} / {spec.Label ?? tree}";
            int spent = SpendBuild(p, spec);
            if (spec.Gear != null) Equip(p, spec.Gear);
            Place(p, victim);

            float melee = p.Entity.Stats.GetBlended(StatKeys.MeleeWeaponsDamage);
            float ranged = p.Entity.Stats.GetBlended(StatKeys.RangedWeaponsDamage);
            bool behind = DamageModifiers.IsBehind(victim.Entity, p.Entity);

            bool hasBuffs = spec.Buffs is { Length: > 0 };
            var rows = new List<Row>();
            float swing = await Probe(p, victim, null, spec);
            float swingBuffed = hasBuffs ? await Probe(p, victim, null, spec, buffed: true) : swing;
            rows.Add(new Row(SwingRow, swing, swingBuffed, ""));

            float background = spec.Setup != null ? await Probe(p, victim, NoCast, spec) : 0f;
            float backgroundBuffed = spec.Setup != null && hasBuffs ? await Probe(p, victim, NoCast, spec, buffed: true) : background;
            if (background > 0f || backgroundBuffed > 0f)
                rows.Add(new Row(BackgroundRow, background, backgroundBuffed, "subtracted from spell rows"));

            foreach (string spellId in SpellAccess.Available(p.Entity, Mod))
            {
                if (spellId == spec.Setup) continue;
                rows.Add(await ProbeSpell(p, victim, spellId, spec, ranged, background, backgroundBuffed));
            }

            report.AppendLine($"## {name}");
            report.AppendLine();
            report.AppendLine($"Points in tree: {spent}/{Progression(p).TotalTalentPoints}; meleeWeaponsDamage ×{F(melee)}, rangedWeaponsDamage ×{F(ranged)}; from behind: {(behind ? "yes" : "no")}.");
            if (hasBuffs)
                report.AppendLine($"Full-burst buffs: {string.Join(", ", spec.Buffs!.Select(id => Mod.Spells.Get(id)?.DisplayName ?? id))}.");
            report.AppendLine();
            report.AppendLine("| Spell | Damage | % HP | Full burst | % HP | Note |");
            report.AppendLine("|---|---|---|---|---|---|");
            foreach (Row r in rows.OrderByDescending(r => r.Buffed).ThenByDescending(r => r.Damage))
            {
                report.AppendLine($"| {r.Spell} | {F(r.Damage)} | {P(r.Damage / refHp)}{HitFlag(r.Damage, refHp)} | " +
                                  $"{F(r.Buffed)} | {P(r.Buffed / refHp)}{HitFlag(r.Buffed, refHp)} | {r.Note} |");
                if (r.Buffed > 0f) top.Add((name, r));
            }

            // Rough: the three strongest spells, ignoring GCD and cast times; an empowered strike already carries its swing.
            var spellRows = rows.Where(r => r.Spell != SwingRow && r.Spell != BackgroundRow).ToList();
            float burst = spellRows.Select(r => r.Damage).OrderByDescending(d => d).Take(3).Sum();
            float burstBuffed = spellRows.Select(r => r.Buffed).OrderByDescending(d => d).Take(3).Sum();
            burstSummary.Add((name, burst, burstBuffed));
            report.AppendLine();
            report.AppendLine($"Rough burst (top 3 spells, empowered strikes include their swing): **{F(burst)}** = {P(burst / refHp)}{BurstFlag(burst, refHp)}" +
                              (hasBuffs ? $"; full burst **{F(burstBuffed)}** = {P(burstBuffed / refHp)}{BurstFlag(burstBuffed, refHp)}" : ""));
            report.AppendLine();

            await Leave(p);
            // Partial results survive a timeout or a crash further down the list.
            File.WriteAllText(path, report.ToString());
            Console.WriteLine($"[balance] {name}: burst {F(burst)} ({P(burst / refHp)}), full {F(burstBuffed)} ({P(burstBuffed / refHp)})");
        }

        report.AppendLine("## Summary: rough burst per spec");
        report.AppendLine();
        report.AppendLine("| Spec | Burst | % HP | Full burst | % HP |");
        report.AppendLine("|---|---|---|---|---|");
        foreach (var (spec, burst, buffed) in burstSummary.OrderByDescending(b => b.Buffed))
            report.AppendLine($"| {spec} | {F(burst)} | {P(burst / refHp)}{BurstFlag(burst, refHp)} | {F(buffed)} | {P(buffed / refHp)}{BurstFlag(buffed, refHp)} |");
        report.AppendLine();

        report.AppendLine("## Summary: 20 hardest hits (full burst)");
        report.AppendLine();
        report.AppendLine("| Spec | Spell | Damage | Full burst | % HP |");
        report.AppendLine("|---|---|---|---|---|");
        foreach (var (spec, row) in top.OrderByDescending(t => t.Row.Buffed).Take(20))
            report.AppendLine($"| {spec} | {row.Spell} | {F(row.Damage)} | {F(row.Buffed)} | {P(row.Buffed / refHp)} |");

        File.WriteAllText(path, report.ToString());
        World.Api.Logger.Notification("[canrpgclasses] balance report: {0}", path);
    }

    // ---- build -----------------------------------------------------------------------------

    /// <summary>Fills the spec's tree tier by tier, then the other trees, through the real spending rules.</summary>
    private int SpendBuild(ITestPlayer p, Spec spec)
    {
        int treeCount = Mod.Classes.Get(spec.ClassId)?.TreeCount ?? 3;
        FillTree(p, spec.ClassId, spec.Tree);
        int inTree = TalentState.PointsInTree(p.Entity, Mod.Talents, spec.Tree);
        for (int t = 0; t < treeCount; t++)
            if (t != spec.Tree) FillTree(p, spec.ClassId, t);
        return inTree;
    }

    private void FillTree(ITestPlayer p, string classId, int tree)
    {
        Talent[] order = Mod.Talents.ForClassTree(classId, tree).OrderBy(t => t.Tier).ThenBy(t => t.Column).ToArray();
        bool spent = true;
        while (spent)
        {
            spent = false;
            foreach (Talent t in order)
                if (Talents(p).TrySpend(t.Id)) { spent = true; break; }
        }
    }

    // ---- probes ----------------------------------------------------------------------------

    private async Task<Row> ProbeSpell(ITestPlayer p, ITestPlayer victim, string spellId, Spec spec, float ranged,
        float background, float backgroundBuffed)
    {
        string name = Mod.Spells.Get(spellId)?.DisplayName ?? spellId;
        try
        {
            var notes = new List<string>();
            float dmg = Math.Max(0f, await Probe(p, victim, spellId, spec, notes, ranged) - background);
            float buffed = dmg;
            // A buff measured on top of itself would only double-count, and a spell that missed plain will miss again.
            if (spec.Buffs is { Length: > 0 } && !spec.Buffs.Contains(spellId) && dmg > 0f)
            {
                var buffNotes = new List<string>();
                buffed = Math.Max(0f, await Probe(p, victim, spellId, spec, buffNotes, ranged, buffed: true) - backgroundBuffed);
                notes.AddRange(buffNotes.Where(n => n.StartsWith("buff ")));
            }
            return new Row(name, dmg, buffed, string.Join("; ", notes));
        }
        catch (Exception ex)
        {
            return new Row(name, 0f, 0f, "error: " + ex.Message);
        }
    }

    // Setup only, no cast: what the setup itself deals over the window (a summoned pet's auto-attacks).
    private const string NoCast = "";

    /// <summary>Health the victim loses within the window; a null spell measures one white swing.</summary>
    private async Task<float> Probe(ITestPlayer p, ITestPlayer victim, string? spellId, Spec spec,
        List<string>? notes = null, float ranged = 1f, bool buffed = false)
    {
        Reset(p, victim);
        if (spec.Setup != null && spellId != null && !await Prepare(p, spec.Setup))
            notes?.Add($"setup failed: {Caster(p).LastFailReason}");
        if (buffed)
        {
            foreach (string buff in spec.Buffs!)
                if (!await Prepare(p, buff))
                    notes?.Add($"buff {Mod.Spells.Get(buff)?.DisplayName ?? buff} failed: {Caster(p).LastFailReason}");
            // Effects tick once a second and a totem buffs on its pulse; let both land before the hit.
            long settle = World.Api.World.ElapsedMilliseconds + BuffSettleMs;
            while (World.Api.World.ElapsedMilliseconds < settle) await World.Ticks(1);
            FillResource(p);
        }
        // Read after the buffs, so a melee-strength buff reaches the hand-made swing too.
        float melee = p.Entity.Stats.GetBlended(StatKeys.MeleeWeaponsDamage);

        float before = Health(victim.Entity);
        if (spellId == null)
        {
            Swing(victim.Entity, p.Entity, melee);
            return before - Health(victim.Entity);
        }

        if (spellId != NoCast)
        {
            ResetCooldowns(p, spellId);
            if (!RpgCombatApi.TryCast(p.Entity, spellId, AimAt(victim.Entity)))
            {
                notes?.Add($"cast failed: {Caster(p).LastFailReason}");
                return 0f;
            }
            await CastToEnd(p);
        }

        var wa = p.Entity.WatchedAttributes;
        // An empower lands on the next swing, so the row is that whole swing: what the victim actually takes.
        if (wa.HasAttribute(SpellExecutor.EmpowerDamageKey))
        {
            Swing(victim.Entity, p.Entity, melee);
            notes?.Add("empowered strike, swing included");
        }
        float shotBonus = wa.GetLong(AttrKeys.ShotUntilMs, 0) > World.Api.World.ElapsedMilliseconds ? wa.GetFloat(AttrKeys.ShotDamage, 0f) : 0f;

        long until = World.Api.World.ElapsedMilliseconds + WindowMs;
        while (World.Api.World.ElapsedMilliseconds < until) await World.Ticks(1);

        float dealt = before - Health(victim.Entity);
        if (shotBonus > 0f)
        {
            // The armed shot rides a vanilla arrow; added by hand, without the victim's mitigation.
            float arrow = ArrowBase * ranged;
            dealt += arrow + shotBonus;
            notes?.Add($"armed shot: arrow {F(arrow)} + bonus {F(shotBonus)} (unmitigated)");
        }
        if (dealt <= 0f && notes is { Count: 0 }) notes.Add("no damage (buff/heal/utility)");
        return dealt;
    }

    /// <summary>Casts a self-targeted setup or buff and leaves the caster ready for the next cast.</summary>
    private async Task<bool> Prepare(ITestPlayer p, string spellId)
    {
        ResetCooldowns(p, spellId);
        bool cast = RpgCombatApi.TryCast(p.Entity, spellId, AimContext.None);
        await CastToEnd(p);
        ResetCooldowns(p, spellId);
        FillResource(p);
        return cast;
    }

    private void Reset(ITestPlayer p, ITestPlayer victim)
    {
        foreach (Entity e in new[] { p.Entity, victim.Entity })
        {
            // Form, stance or aura, pet and totem would otherwise leak into every later probe.
            ClassCleanup.Strip(e, Mod);
            e.WatchedAttributes.RemoveAttribute(CombatFlags.FormActionLock);
            if (e.GetBehavior<EBEffectsAffected>()?.activeEffects is { } active)
                foreach (string id in active.Keys.ToList()) SpellExecutor.RemoveEffect(e, id);
            ControlState.ClearAll(e);
            e.IsOnFire = false;
            var wa = e.WatchedAttributes;
            wa.RemoveAttribute(SpellExecutor.EmpowerDamageKey);
            wa.RemoveAttribute(AttrKeys.ShotUntilMs);
            wa.RemoveAttribute(CombatFlags.Absorb);
            wa.SetBool(CombatFlags.InCombat, false);
            wa.RemoveAttribute(CombatFlags.InCombatUntil);
        }
        ResourceState.ResetCombo(p.Entity);
        ResourceState.AddCombo(p.Entity, 5);
        FillResource(p);
        var health = victim.Entity.GetBehavior<EntityBehaviorHealth>()!;
        health.Health = health.MaxHealth;
        Place(p, victim);
    }

    /// <summary>Attacker 3 blocks behind the victim, both facing +X.</summary>
    private static void Place(ITestPlayer p, ITestPlayer victim)
    {
        Vec3d at = victim.Entity.Pos.XYZ;
        p.Entity.TeleportTo(at.AddCopy(-3, 0, 0));
        victim.Entity.Pos.Yaw = GameMath.PIHALF;
        p.Entity.Pos.Yaw = GameMath.PIHALF;
    }

    /// <summary>A bow goes to the active hotbar slot, anything else (a shield) to the left hand.</summary>
    private void Equip(ITestPlayer p, string itemCode)
    {
        Item item = World.Api.World.GetItem(new AssetLocation(itemCode)) ?? throw new ArgumentException("no item " + itemCode);
        ItemSlot slot = item.Tool == EnumTool.Bow ? p.Player.InventoryManager.ActiveHotbarSlot : p.Entity.LeftHandItemSlot;
        slot.Itemstack = new ItemStack(item);
        slot.MarkDirty();
        AddCleanup(() => { slot.Itemstack = null; slot.MarkDirty(); });
        p.Entity.GetBehavior<canrpgclasses.Core.EB.EBGearAffinity>()?.Refresh();
    }

    private static string SourceDir([CallerFilePath] string file = "") => Path.GetDirectoryName(file)!;

    private static void RaiseHealth(Entity e)
    {
        var health = e.GetBehavior<EntityBehaviorHealth>() ?? throw new InvalidOperationException("no health behavior");
        health.BaseMaxHealth = 10000f;
        health.UpdateMaxHealth();
        health.Health = health.MaxHealth;
    }

    // Mirrors vanilla EntityAgent.OnInteract: DidAttack after a landed hit is what weapon imbues, coats and stacks ride.
    private static void Swing(Entity victim, Entity attacker, float melee)
    {
        var source = new DamageSource
        {
            Source = EnumDamageSource.Player,
            SourceEntity = attacker,
            CauseEntity = attacker,
            Type = EnumDamageType.SlashingAttack,
            IgnoreInvFrames = true
        };
        if (victim.ReceiveDamage(source, SwingBase * melee) && attacker is EntityAgent a && victim is EntityAgent v)
            a.DidAttack(source, v);
    }

    private static string HitFlag(float dmg, float refHp) => dmg > HitWarn * refHp ? " ⚠" : "";
    private static string BurstFlag(float dmg, float refHp) => dmg > BurstWarn * refHp ? " ⚠⚠" : "";

    private static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);
    private static string P(float v) => (v * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
}
