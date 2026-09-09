using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Progression;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Core.EB
{
    /// <summary>
    /// Server-side talent authority: validates and writes talent ranks (synced via WatchedAttributes)
    /// and (re)applies all stat-modifier talents. Combat/hook talents read ranks live via
    /// <see cref="TalentState.Rank"/>. The current class is stored here too; switching class respecs.
    /// </summary>
    public class EBTalents : EntityBehavior
    {
        public const string Name = "canrpgtalents";

        public EBTalents(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        private bool IsServer => entity.Api.Side == EnumAppSide.Server;
        private canrpgclassesModSystem? Mod => canrpgclassesModSystem.For(entity.Api);

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            // Re-apply on both sides whenever the synced ranks change, so client-side prediction
            // (e.g. walkspeed) matches the server. Writes still happen server-side only.
            entity.WatchedAttributes.RegisterModifiedListener(TalentState.RanksKey, ApplyStatTalents);
            entity.WatchedAttributes.RegisterModifiedListener("stats", OnStatsModified);
            ApplyStatTalents();
        }

        // Attributes also move outside this pass (a buff, a worn item, an admin edit), and every such change marks
        // "stats" dirty - so one listener replaces polling. Our own payouts write stats too, hence the guard.
        private bool applyingStats;
        private int lastAttributeSignature;
        private bool signatureKnown;

        private void OnStatsModified()
        {
            if (applyingStats || !Attributes.RpgAttributes.Any) return;

            int sig = Attributes.AttributeStats.Signature(entity);
            if (signatureKnown && sig == lastAttributeSignature) return;
            // Recorded before the pass too, so an entity the pass bails on doesn't re-enter on every stat write.
            lastAttributeSignature = sig;
            signatureKnown = true;
            ApplyStatTalents();
        }

        public bool TrySpend(string talentId)
        {
            if (!IsServer) return false;
            var mod = Mod;
            var prog = entity.GetBehavior<EBProgression>();
            if (mod == null || prog == null) return false;

            var talent = mod.Talents.Get(talentId);
            if (talent == null) return false;
            if (!TalentState.CanSpend(entity, talent, mod.Talents, prog.TotalTalentPoints, out _)) return false;

            SetRank(talentId, TalentState.Rank(entity, talentId) + 1);
            ApplyStatTalents();
            return true;
        }

        public void Respec()
        {
            if (!IsServer) return;
            entity.WatchedAttributes.RemoveAttribute(TalentState.RanksKey);
            entity.WatchedAttributes.MarkPathDirty(TalentState.RanksKey);
            entity.WatchedAttributes.SetString(EBAuras.ActiveKey, ""); // drop any active aura (esp. on class switch)
            ApplyStatTalents();
        }

        public void SetClass(string classId)
        {
            if (!IsServer || string.IsNullOrEmpty(classId)) return;
            entity.WatchedAttributes.SetString(TalentState.ClassKey, classId);
            Respec();
        }

        /// <summary>Respec, then re-spend points to match a saved build. Each point goes through the normal
        /// <see cref="TrySpend"/> validation (tier gates, prerequisites, available points), and we loop greedily so
        /// lower tiers unlock higher ones regardless of array order. Talents for another class are ignored. If the
        /// player has fewer points than the build needs, it applies as far as it can.</summary>
        public void ApplyLoadout(string[]? ids, int[]? ranks)
        {
            if (!IsServer || ids == null || ranks == null || ids.Length != ranks.Length) return;
            var mod = Mod;
            if (mod == null) return;

            string cls = TalentState.CurrentClass(entity);
            var target = new Dictionary<string, int>();
            for (int i = 0; i < ids.Length; i++)
            {
                var t = mod.Talents.Get(ids[i]);
                if (t == null || t.ClassId != cls) continue;
                target[ids[i]] = Math.Max(0, Math.Min(t.MaxRank, ranks[i]));
            }

            Respec();

            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach (var kv in target)
                    while (TalentState.Rank(entity, kv.Key) < kv.Value && TrySpend(kv.Key))
                        progress = true;
            }
        }

        private void SetRank(string id, int rank)
        {
            var tree = entity.WatchedAttributes.GetOrAddTreeAttribute(TalentState.RanksKey);
            tree.SetInt(id, rank);
            entity.WatchedAttributes.MarkPathDirty(TalentState.RanksKey);
        }

        private static string LevelStatSource(string stat) => "canrpglevelstat_" + stat;
        private static string ClassBaseSource(string stat) => "canrpgclassbase_" + stat;

        /// <summary>Removes the base and per-level stat sources of every class this character is not.</summary>
        private static void ClearForeignClassStats(EntityAgent agent, canrpgclassesModSystem mod, RpgClassDef? current)
        {
            foreach (var other in mod.Classes.All.Values)
            {
                if (other == current) continue;

                foreach (var (stat, _) in other.BaseStats)
                    if (current == null || !current.BaseStats.Exists(s => s.Stat == stat))
                        agent.RemoveStatIfPresent(stat, ClassBaseSource(stat));

                foreach (var (stat, _) in other.StatsPerLevel)
                    if (current == null || !current.StatsPerLevel.Exists(s => s.Stat == stat))
                        agent.RemoveStatIfPresent(stat, LevelStatSource(stat));

                if (other.HpPerLevel > 0f && (current == null || current.HpPerLevel <= 0f))
                    agent.RemoveStatIfPresent(StatKeys.MaxHealthExtraPoints, "canrpglevelhealth");
            }
        }

        /// <summary>Public hook: re-apply stat talents (e.g. after the client receives a balance-config sync,
        /// so client-side prediction uses the server's per-rank numbers).</summary>
        public void ReapplyStatTalents() => ApplyStatTalents();

        /// <summary>Re-applies every stat talent at its current rank (rank 0 clears that talent's stat source).</summary>
        private void ApplyStatTalents()
        {
            if (applyingStats) return; // our own writes come back through the "stats" listener
            applyingStats = true;
            try { ApplyStatTalentsCore(); }
            finally { applyingStats = false; }
        }

        private void ApplyStatTalentsCore()
        {
            var mod = Mod;
            if (mod == null || entity is not EntityAgent agent) return;
            foreach (var t in mod.Talents.All.Values)
            {
                t.ApplyStats(agent, TalentState.Rank(entity, t.Id));
            }

            // Tree mastery (spec scaling): each point invested in a tree grants a stat, data-driven per class.
            // A stat is keyed uniquely per (tree, stat) so several masteries - even on the same stat - stack.
            var cls = mod.Classes.Get(TalentState.CurrentClass(entity));
            if (cls != null)
            {
                foreach (var m in cls.TreeMasteries)
                {
                    int pts = TalentState.PointsInTree(entity, mod.Talents, m.TreeIndex);
                    agent.SetStatIfChanged(m.Stat, "canrpgmastery_" + m.TreeIndex + "_" + m.Stat, pts * m.PerPoint);
                }

                foreach (var (stat, val) in cls.BaseStats)
                    agent.SetStatIfChanged(stat, ClassBaseSource(stat), val);

                // Per-level stats, "level - 1" as everywhere else. HpPerLevel is the maxhealth shorthand;
                // StatsPerLevel is how a class hands out attribute points.
                int levels = Math.Max(0, (entity.GetBehavior<EBProgression>()?.Level ?? 1) - 1);
                if (cls.HpPerLevel > 0f)
                    agent.SetStatIfChanged(StatKeys.MaxHealthExtraPoints, "canrpglevelhealth", cls.HpPerLevel * levels);
                foreach (var (stat, perLevel) in cls.StatsPerLevel)
                    agent.SetStatIfChanged(stat, LevelStatSource(stat), perLevel * levels);
            }

            // A class switch must not leave the previous class's grants behind.
            ClearForeignClassStats(agent, mod, cls);

            // Attributes last: their payouts read the totals written above. Runs without a class too - talents
            // and gear can feed an attribute on their own.
            Attributes.AttributeStats.Apply(agent, TalentState.CurrentClass(entity),
                entity.GetBehavior<EBProgression>()?.Level ?? 1);
            lastAttributeSignature = Attributes.AttributeStats.Signature(entity);
            signatureKnown = true;

            // maxhealthExtraPoints (toughness/guardian) is cached by EntityBehaviorHealth - setting the stat
            // doesn't recompute MaxHealth on its own, so the new HP never shows (esp. on the client). Other
            // stats (melee damage, walkspeed…) are read live via GetBlended and need no nudge. Recompute here.
            entity.GetBehavior<EntityBehaviorHealth>()?.MarkDirty();
        }
    }
}
