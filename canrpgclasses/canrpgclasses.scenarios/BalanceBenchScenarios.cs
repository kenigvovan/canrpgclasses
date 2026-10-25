using System.Globalization;
using System.Text;
using Atlas.Api;
using Atlas.XUnit;
using canrpgclasses.Core;
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
/// its spells, and the damage goes to balance-report.md. Not a pass/fail test, so it has its own category.</summary>
[Trait("Category", "Balance")]
public class BalanceBenchScenarios : RpgScenarioBase
{
    private const int Level = 25;
    private const string VictimClass = "mage";
    // Vanilla base of a steel-tier swing and a recurve arrow; scaled by the attacker's weapon-damage stats.
    private const float SwingBase = 5f;
    private const float ArrowBase = 6.5f;
    private const int WindowMs = 3000;
    // Rows over these shares of the victim's real max HP get flagged.
    private const float HitWarn = 0.30f;
    private const float BurstWarn = 0.65f;

    private record Spec(string ClassId, int Tree, string? Setup = null, string? Label = null);

    private static readonly Spec[] Specs =
    {
        new("warrior", 0), new("warrior", 1), new("warrior", 2),
        new("paladin", 0), new("paladin", 1), new("paladin", 2),
        new("rogue", 0, Setup: "canrpgclasses:stealth"), new("rogue", 1), new("rogue", 2),
        new("hunter", 0), new("hunter", 1), new("hunter", 2),
        new("mage", 0), new("mage", 1), new("mage", 2),
        new("priest", 0), new("priest", 1), new("priest", 2),
        new("druid", 0),
        new("druid", 1, Setup: "canrpgclasses:feline_form", Label: "Feral (кот)"),
        new("druid", 1, Setup: "canrpgclasses:ursine_form", Label: "Feral (медведь)"),
        new("druid", 2),
        new("shaman", 0), new("shaman", 1), new("shaman", 2),
    };

    private record Row(string Spell, float Damage, string Note);

    // BALANCE_CLASSES=rogue,mage limits the run to those classes.
    [AtlasScenario(TimeoutMs = 45 * 60 * 1000)]
    public async Task Bench_AllSpecs_ToReport()
    {
        string? only = Environment.GetEnvironmentVariable("BALANCE_CLASSES");
        var classes = string.IsNullOrWhiteSpace(only) ? null
            : only.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        string path = Path.Combine(AppContext.BaseDirectory, "balance-report.md");

        ITestPlayer victim = await JoinAs(VictimClass, Level);
        Survival(victim);
        float refHp = MaxHealth(victim.Entity);
        Assert.True(refHp > 0f, "precondition: the victim has health");
        RaiseHealth(victim.Entity);

        var report = new StringBuilder();
        report.AppendLine($"# Баланс-бенч, {Level} лвл");
        report.AppendLine();
        report.AppendLine($"Цель: {VictimClass} {Level} лвл, реальное HP **{F(refHp)}** (резисты и абсорб цели настоящие, брони нет). " +
                          $"Урон считается за {WindowMs / 1000} с после каста. Белый удар = {F(SwingBase)} × meleeWeaponsDamage, стрела = {F(ArrowBase)} × rangedWeaponsDamage. " +
                          $"⚠ удар > {P(HitWarn)} HP, ⚠⚠ грубый бурст > {P(BurstWarn)} HP.");
        report.AppendLine();

        var top = new List<(string Spec, Row Row)>();
        var burstSummary = new List<(string Spec, float Burst)>();

        foreach (Spec spec in Specs)
        {
            if (classes != null && !classes.Contains(spec.ClassId)) continue;
            ITestPlayer p = await JoinAs(spec.ClassId, Level);
            Survival(p);
            string tree = Mod.Classes.Get(spec.ClassId)?.TreeName(spec.Tree) ?? spec.Tree.ToString();
            string name = $"{spec.ClassId} / {spec.Label ?? tree}";
            int spent = SpendBuild(p, spec);
            Place(p, victim);

            float melee = p.Entity.Stats.GetBlended(StatKeys.MeleeWeaponsDamage);
            float ranged = p.Entity.Stats.GetBlended(StatKeys.RangedWeaponsDamage);
            bool behind = DamageModifiers.IsBehind(victim.Entity, p.Entity);

            var rows = new List<Row>();
            float swing = await Probe(p, victim, null, spec);
            rows.Add(new Row("белый удар", swing, ""));

            foreach (string spellId in SpellAccess.Available(p.Entity, Mod))
            {
                if (spellId == spec.Setup) continue;
                rows.Add(await ProbeSpell(p, victim, spellId, spec, ranged));
            }

            report.AppendLine($"## {name}");
            report.AppendLine();
            report.AppendLine($"Очков в дереве: {spent}/{Progression(p).TotalTalentPoints}; meleeWeaponsDamage ×{F(melee)}, rangedWeaponsDamage ×{F(ranged)}; со спины: {(behind ? "да" : "нет")}.");
            report.AppendLine();
            report.AppendLine("| Спелл | Урон | % HP | Примечание |");
            report.AppendLine("|---|---|---|---|");
            foreach (Row r in rows.OrderByDescending(r => r.Damage))
            {
                string flag = r.Damage > HitWarn * refHp ? " ⚠" : "";
                report.AppendLine($"| {r.Spell} | {F(r.Damage)} | {P(r.Damage / refHp)}{flag} | {r.Note} |");
                if (r.Damage > 0f) top.Add((name, r));
            }

            // Rough: the three strongest spells plus two white swings, ignoring GCD and cast times.
            float burst = rows.Where(r => r.Spell != "белый удар").Select(r => r.Damage).OrderByDescending(d => d).Take(3).Sum() + 2f * swing;
            burstSummary.Add((name, burst));
            report.AppendLine();
            report.AppendLine($"Грубый бурст (топ-3 спелла + 2 белых удара): **{F(burst)}** = {P(burst / refHp)}{(burst > BurstWarn * refHp ? " ⚠⚠" : "")}");
            report.AppendLine();

            await Leave(p);
            // Partial results survive a timeout or a crash further down the list.
            File.WriteAllText(path, report.ToString());
            Console.WriteLine($"[balance] {name}: burst {F(burst)} ({P(burst / refHp)})");
        }

        report.AppendLine("## Сводка: грубый бурст по спекам");
        report.AppendLine();
        report.AppendLine("| Спек | Бурст | % HP |");
        report.AppendLine("|---|---|---|");
        foreach (var (spec, burst) in burstSummary.OrderByDescending(b => b.Burst))
            report.AppendLine($"| {spec} | {F(burst)} | {P(burst / refHp)}{(burst > BurstWarn * refHp ? " ⚠⚠" : "")} |");
        report.AppendLine();

        report.AppendLine("## Сводка: 20 самых сильных ударов");
        report.AppendLine();
        report.AppendLine("| Спек | Спелл | Урон | % HP |");
        report.AppendLine("|---|---|---|---|");
        foreach (var (spec, row) in top.OrderByDescending(t => t.Row.Damage).Take(20))
            report.AppendLine($"| {spec} | {row.Spell} | {F(row.Damage)} | {P(row.Damage / refHp)} |");

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

    private async Task<Row> ProbeSpell(ITestPlayer p, ITestPlayer victim, string spellId, Spec spec, float ranged)
    {
        string name = Mod.Spells.Get(spellId)?.DisplayName ?? spellId;
        try
        {
            var notes = new List<string>();
            float dmg = await Probe(p, victim, spellId, spec, notes, ranged);
            return new Row(name, dmg, string.Join("; ", notes));
        }
        catch (Exception ex)
        {
            return new Row(name, 0f, "ошибка: " + ex.Message);
        }
    }

    /// <summary>Health the victim loses within the window; a null spell measures one white swing.</summary>
    private async Task<float> Probe(ITestPlayer p, ITestPlayer victim, string? spellId, Spec spec,
        List<string>? notes = null, float ranged = 1f)
    {
        float melee = p.Entity.Stats.GetBlended(StatKeys.MeleeWeaponsDamage);
        Reset(p, victim);
        if (spec.Setup != null && spellId != null)
        {
            ResetCooldowns(p, spec.Setup);
            if (!RpgCombatApi.TryCast(p.Entity, spec.Setup, AimContext.None))
                notes?.Add($"подготовка не вышла: {Caster(p).LastFailReason}");
            await CastToEnd(p);
            ResetCooldowns(p, spec.Setup);
            FillResource(p);
        }

        float before = Health(victim.Entity);
        if (spellId == null)
        {
            Swing(victim.Entity, p.Entity, melee);
            return before - Health(victim.Entity);
        }

        ResetCooldowns(p, spellId);
        if (!RpgCombatApi.TryCast(p.Entity, spellId, AimAt(victim.Entity)))
        {
            notes?.Add($"не скастовался: {Caster(p).LastFailReason}");
            return 0f;
        }
        await CastToEnd(p);

        var wa = p.Entity.WatchedAttributes;
        // An empower lands on the next swing, so the row is that whole swing: what the victim actually takes.
        if (wa.HasAttribute(SpellExecutor.EmpowerDamageKey))
        {
            Swing(victim.Entity, p.Entity, melee);
            notes?.Add("усиленный удар, вместе с белым");
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
            notes?.Add($"заряженный выстрел: стрела {F(arrow)} + бонус {F(shotBonus)} (без митигации)");
        }
        if (dealt <= 0f && notes is { Count: 0 }) notes.Add("урона нет (бафф/хил/утилита)");
        return dealt;
    }

    private void Reset(ITestPlayer p, ITestPlayer victim)
    {
        foreach (Entity e in new[] { p.Entity, victim.Entity })
        {
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

    private static void RaiseHealth(Entity e)
    {
        var health = e.GetBehavior<EntityBehaviorHealth>() ?? throw new InvalidOperationException("no health behavior");
        health.BaseMaxHealth = 10000f;
        health.UpdateMaxHealth();
        health.Health = health.MaxHealth;
    }

    private static void Swing(Entity victim, Entity attacker, float melee) =>
        victim.ReceiveDamage(new DamageSource
        {
            Source = EnumDamageSource.Player,
            SourceEntity = attacker,
            CauseEntity = attacker,
            Type = EnumDamageType.SlashingAttack,
            IgnoreInvFrames = true
        }, SwingBase * melee);

    private static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);
    private static string P(float v) => (v * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
}
