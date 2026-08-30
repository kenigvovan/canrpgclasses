using System;
using System.Collections.Generic;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Core.Content
{
    /// <summary>
    /// Validates a talent-tree edit before the server applies it: rejects ids of compiled talents (an edit would
    /// silently replace the real hook), unknown classes/trees, unsafe id characters and absurd sizes. The editor
    /// is admin-gated, but the packet is still client input landing in registries every player reads.
    /// </summary>
    public static class ContentEditGuard
    {
        public const int MaxJsonBytes = 64 * 1024;
        public const int MaxTalentsPerTree = 200;
        public const int MaxIdLength = 96;
        public const int MaxTextLength = 512;
        public const int MaxStatsPerTalent = 8;
        public const int MaxRankCap = 20;
        public const int MaxGridCoordinate = 64;
        public const float MaxStatMagnitude = 100f;

        /// <summary>Checks one incoming tree. Returns null when it may be applied, or the reason to refuse it.</summary>
        public static string? Reject(string classId, int treeIndex, List<TalentModel> incoming,
                                     RpgClassRegistry classes, TalentRegistry talents)
        {
            var cls = classes.Get(classId);
            if (cls == null) return $"no such class '{Trim(classId)}'";
            if (treeIndex < 0 || treeIndex >= cls.TreeCount)
                return $"class '{cls.Id}' has {cls.TreeCount} tree(s), so tree {treeIndex} doesn't exist";

            if (incoming.Count > MaxTalentsPerTree)
                return $"{incoming.Count} talents in one tree, the limit is {MaxTalentsPerTree}";

            var seen = new HashSet<string>();
            foreach (var t in incoming)
            {
                if (string.IsNullOrWhiteSpace(t.id)) return "a talent has no id";
                if (t.id!.Length > MaxIdLength) return $"talent id is longer than {MaxIdLength} characters";
                if (!IsCleanId(t.id)) return $"talent id '{Trim(t.id)}' may only use letters, digits, ':', '_', '-' and '.'";
                if (!seen.Add(t.id)) return $"talent id '{Trim(t.id)}' appears twice in the same tree";

                // The one that actually matters: never let an edit take over a talent whose behaviour is code.
                var existing = talents.Get(t.id);
                if (existing != null && existing is not ContentTalent)
                    return $"'{Trim(t.id)}' is a built-in talent and can't be overwritten from the editor";

                // A save replaces one tree, so an id already used by another tree would leave two entries with the
                // same id on disk - and only one of them would ever register.
                if (existing is ContentTalent && (existing.TreeIndex != treeIndex
                        || !string.Equals(existing.ClassId, classId, System.StringComparison.OrdinalIgnoreCase)))
                    return $"'{Trim(t.id)}' already exists in {existing.ClassId} tree {existing.TreeIndex}";

                if (t.maxRank < 1 || t.maxRank > MaxRankCap)
                    return $"'{Trim(t.id)}' has {t.maxRank} ranks, allowed is 1..{MaxRankCap}";
                if (t.tier < 0 || t.tier > MaxGridCoordinate || t.column < 0 || t.column > MaxGridCoordinate)
                    return $"'{Trim(t.id)}' sits outside the {MaxGridCoordinate}x{MaxGridCoordinate} grid";

                if (TooLong(t.name) || TooLong(t.description) || TooLong(t.icon))
                    return $"'{Trim(t.id)}' has a text field longer than {MaxTextLength} characters";
                if (t.requires != null && (t.requires.Length > MaxIdLength || !IsCleanId(t.requires)))
                    return $"'{Trim(t.id)}' has a malformed 'requires'";
                if (t.grantsSpell != null && (t.grantsSpell.Length > MaxIdLength || !IsCleanId(t.grantsSpell)))
                    return $"'{Trim(t.id)}' has a malformed 'grantsSpell'";

                if (t.stats != null)
                {
                    if (t.stats.Count > MaxStatsPerTalent)
                        return $"'{Trim(t.id)}' sets {t.stats.Count} stats, the limit is {MaxStatsPerTalent}";
                    foreach (var s in t.stats)
                    {
                        if (string.IsNullOrWhiteSpace(s.stat)) return $"'{Trim(t.id)}' has a stat entry with no name";
                        if (s.stat!.Length > MaxIdLength || !IsCleanId(s.stat))
                            return $"'{Trim(t.id)}' has a malformed stat name";
                        if (float.IsNaN(s.perRank) || float.IsInfinity(s.perRank))
                            return $"'{Trim(t.id)}' has a stat value that isn't a number";
                        if (System.Math.Abs(s.perRank) > MaxStatMagnitude)
                            return $"'{Trim(t.id)}' sets {s.stat} to {s.perRank} per rank, the limit is ±{MaxStatMagnitude}";
                    }
                }

                if (t.descArgs is { Length: > 16 }) return $"'{Trim(t.id)}' has too many descArgs";
            }

            return null;
        }

        public const int MaxTrees = 8;
        public const int MaxBaseSpells = 32;
        public const int MaxStatEntries = 16;
        public const float MaxBaseStatMagnitude = 1000f;
        public const float MaxResourceMax = 100000f;
        public const float MaxHpPerLevel = 200f;

        /// <summary>Checks an incoming class definition. Same contract as <see cref="Reject"/>: null means it may
        /// be applied.</summary>
        public static string? RejectClass(ClassModel m, RpgClassRegistry classes, SpellRegistry spells,
                                          TalentRegistry talents)
        {
            if (string.IsNullOrWhiteSpace(m.id)) return "the class has no id";
            if (m.id!.Length > MaxIdLength || !IsCleanId(m.id))
                return $"class id '{Trim(m.id)}' may only use letters, digits, ':', '_', '-' and '.'";

            // A code class carries behaviour a data record can't - gear affinities, its own mechanics - so a data
            // class of the same id would replace it and quietly turn all of that off.
            var existing = classes.Get(m.id);
            if (existing != null && existing is not ContentClassDef)
                return $"'{Trim(m.id)}' is a built-in class and can't be redefined from the editor";

            if (TooLong(m.name) || TooLong(m.description) || TooLong(m.role) || TooLong(m.icon))
                return "a text field is longer than " + MaxTextLength + " characters";

            int treeCount = m.trees?.Count ?? 0;
            if (treeCount < 1 || treeCount > MaxTrees)
                return $"a class needs 1..{MaxTrees} trees, this one has {treeCount}";
            foreach (var name in m.trees!)
            {
                if (string.IsNullOrWhiteSpace(name)) return "a tree has no name";
                if (name.Length > MaxTextLength) return "a tree name is too long";
            }

            // Dropping a tree that still holds talents would strand them: their TreeIndex would point past the
            // class, and a code talent can't simply be moved from here.
            foreach (var t in talents.ForClass(m.id))
                if (t.TreeIndex >= treeCount)
                    return $"tree {t.TreeIndex} still holds '{t.Id}' - empty it before removing the tree";

            if (m.baseSpells != null)
            {
                if (m.baseSpells.Count > MaxBaseSpells)
                    return $"{m.baseSpells.Count} base spells, the limit is {MaxBaseSpells}";
                foreach (var id in m.baseSpells)
                {
                    if (string.IsNullOrWhiteSpace(id)) return "a base spell entry is empty";
                    if (spells.Get(id) == null) return $"base spell '{Trim(id)}' doesn't exist";
                }
            }

            if (!Finite(m.hpPerLevel) || Math.Abs(m.hpPerLevel) > MaxHpPerLevel)
                return $"hpPerLevel must be within ±{MaxHpPerLevel}";

            if (m.baseStats != null)
            {
                if (m.baseStats.Count > MaxStatEntries)
                    return $"{m.baseStats.Count} base stats, the limit is {MaxStatEntries}";
                foreach (var s in m.baseStats)
                {
                    if (string.IsNullOrWhiteSpace(s.stat) || !IsCleanId(s.stat!) || s.stat!.Length > MaxIdLength)
                        return "a base stat has a malformed name";
                    if (!Finite(s.value) || Math.Abs(s.value) > MaxBaseStatMagnitude)
                        return $"base stat {s.stat} is outside ±{MaxBaseStatMagnitude}";
                }
            }

            if (m.treeMasteries != null)
            {
                if (m.treeMasteries.Count > MaxStatEntries)
                    return $"{m.treeMasteries.Count} tree masteries, the limit is {MaxStatEntries}";
                foreach (var t in m.treeMasteries)
                {
                    if (string.IsNullOrWhiteSpace(t.stat) || !IsCleanId(t.stat!) || t.stat!.Length > MaxIdLength)
                        return "a tree mastery has a malformed stat name";
                    if (t.tree < 0 || t.tree >= treeCount)
                        return $"a tree mastery points at tree {t.tree}, which this class doesn't have";
                    if (!Finite(t.perPoint) || Math.Abs(t.perPoint) > MaxStatMagnitude)
                        return $"tree mastery {t.stat} is outside ±{MaxStatMagnitude}";
                }
            }

            if (m.resource is { } r)
            {
                if (string.IsNullOrWhiteSpace(r.id) || !IsCleanId(r.id!) || r.id!.Length > MaxIdLength)
                    return "the resource has a malformed id";
                if (!Finite(r.max) || r.max <= 0f || r.max > MaxResourceMax)
                    return $"the resource maximum must be within 0..{MaxResourceMax}";
                if (!Finite(r.regenPerSec) || Math.Abs(r.regenPerSec) > r.max)
                    return "the resource regenerates faster than its own maximum per second";
                if (r.color != null)
                {
                    if (r.color.Length < 3) return "the resource colour needs three values";
                    foreach (float c in r.color)
                        if (!Finite(c) || c < 0f || c > 1f) return "resource colour values run 0..1";
                }
            }

            return null;
        }

        public const int MaxImpacts = 8;
        public const float MaxCoefficient = 20f;
        public const float MaxSeconds = 600f;
        public const float MaxRange = 64f;
        public const int MaxProjectiles = 16;

        /// <summary>Checks an incoming spell definition. Same contract as <see cref="Reject"/>: null means it may
        /// be applied.</summary>
        public static string? RejectSpell(SpellModel m, SpellRegistry spells)
        {
            if (string.IsNullOrWhiteSpace(m.id)) return "the spell has no id";
            if (m.id!.Length > MaxIdLength || !IsCleanId(m.id))
                return $"spell id '{Trim(m.id)}' may only use letters, digits, ':', '_', '-' and '.'";

            // Same rule as talents and classes: a code spell's behaviour is its C# class, and registration is by
            // id, so accepting one here would replace it with a data record that can't do what it did.
            var existing = spells.Get(m.id);
            if (existing != null && existing is not ContentSpell)
                return $"'{Trim(m.id)}' is a built-in spell and can't be redefined from the editor";

            if (TooLong(m.name) || TooLong(m.description) || TooLong(m.icon))
                return "a text field is longer than " + MaxTextLength + " characters";

            if (!Finite(m.range) || m.range < 0f || m.range > MaxRange)
                return $"range must be within 0..{MaxRange}";
            if (!Finite(m.castSeconds) || m.castSeconds < 0f || m.castSeconds > MaxSeconds)
                return $"cast time must be within 0..{MaxSeconds}";
            if (m.channelTicks < 0 || m.channelTicks > 100)
                return "channel ticks must be within 0..100";
            if (!Finite(m.resourceCost) || m.resourceCost < 0f || m.resourceCost > MaxResourceMax)
                return "the resource cost is out of range";
            if (!Finite(m.cooldown) || m.cooldown < 0f || m.cooldown > MaxSeconds)
                return $"cooldown must be within 0..{MaxSeconds}";
            if (m.cooldownGroup != null && (m.cooldownGroup.Length > MaxIdLength || !IsCleanId(m.cooldownGroup)))
                return "the cooldown group is malformed";
            if (m.tier < 0 || m.tier > 10) return "tier must be within 0..10";
            if (m.comboPointsGenerated < 0 || m.comboPointsGenerated > 10)
                return "combo points generated must be within 0..10";

            if (m.delivery is { } d)
            {
                if (!Finite(d.velocity) || d.velocity <= 0f || d.velocity > 10f)
                    return "projectile velocity must be within 0..10";
                if (d.count < 1 || d.count > MaxProjectiles)
                    return $"projectile count must be within 1..{MaxProjectiles}";
                if (!Finite(d.spreadDegrees) || d.spreadDegrees < 0f || d.spreadDegrees > 180f)
                    return "projectile spread must be within 0..180";
                if (d.entity != null && (d.entity.Length > MaxIdLength || !IsCleanId(d.entity)))
                    return "the projectile entity is malformed";
            }

            if (m.requires?.form != null
                && (m.requires.form.Length > MaxIdLength || !IsCleanId(m.requires.form)))
                return "the required form is malformed";

            if (m.impacts != null)
            {
                if (m.impacts.Count > MaxImpacts)
                    return $"{m.impacts.Count} impacts, the limit is {MaxImpacts}";
                foreach (var im in m.impacts)
                {
                    string? bad = RejectImpact(im);
                    if (bad != null) return bad;
                }
            }

            return null;
        }

        private static string? RejectImpact(ImpactModel im)
        {
            if (!Finite(im.chance) || im.chance < 0f || im.chance > 1f)
                return "an impact's chance must be within 0..1";
            if (!Finite(im.coefficient) || Math.Abs(im.coefficient) > MaxCoefficient)
                return $"an impact's coefficient is outside ±{MaxCoefficient}";
            if (!Finite(im.perComboPoint) || Math.Abs(im.perComboPoint) > MaxCoefficient)
                return $"an impact's per-combo-point value is outside ±{MaxCoefficient}";
            if (!Finite(im.knockback) || Math.Abs(im.knockback) > 100f)
                return "an impact's knockback is out of range";
            if (!Finite(im.missingHealthFraction) || im.missingHealthFraction < 0f || im.missingHealthFraction > 1f)
                return "an impact's missing-health fraction must be within 0..1";

            if (im.effectId != null && (im.effectId.Length > MaxIdLength || !IsCleanId(im.effectId)))
                return "an impact's effect id is malformed";
            if (!Finite(im.seconds) || im.seconds < 0f || im.seconds > MaxSeconds)
                return $"an impact's duration must be within 0..{MaxSeconds}";
            if (!Finite(im.secondsPerComboPoint) || Math.Abs(im.secondsPerComboPoint) > MaxSeconds)
                return "an impact's duration per combo point is out of range";
            if (im.amplifier < 0 || im.amplifier > 1000 || im.amplifierCap < 0 || im.amplifierCap > 1000)
                return "an impact's amplifier must be within 0..1000";

            if (!Finite(im.shieldCoefficient) || Math.Abs(im.shieldCoefficient) > MaxCoefficient)
                return $"an impact's shield coefficient is outside ±{MaxCoefficient}";
            if (!Finite(im.shieldSeconds) || im.shieldSeconds < 0f || im.shieldSeconds > MaxSeconds)
                return "an impact's shield duration is out of range";

            if (!Finite(im.teleportDistance) || Math.Abs(im.teleportDistance) > MaxRange)
                return "an impact's teleport distance is out of range";
            if (!Finite(im.resourceGain) || Math.Abs(im.resourceGain) > MaxResourceMax)
                return "an impact's resource gain is out of range";
            if (!Finite(im.aggroRange) || im.aggroRange < 0f || im.aggroRange > MaxRange)
                return "an impact's aggro range is out of range";
            if (im.cleanseMax < 0 || im.cleanseMax > 100)
                return "an impact's cleanse count must be within 0..100";

            if (!Finite(im.zoneSeconds) || im.zoneSeconds < 0f || im.zoneSeconds > MaxSeconds)
                return "a zone's duration is out of range";
            // A zone ticking faster than ten times a second is a server-load problem, not a design choice.
            if (!Finite(im.zoneTickSeconds) || im.zoneTickSeconds < 0.1f || im.zoneTickSeconds > MaxSeconds)
                return "a zone must tick no faster than every 0.1s";
            if (!Finite(im.zoneRadius) || im.zoneRadius < 0f || im.zoneRadius > MaxRange)
                return "a zone's radius is out of range";

            return null;
        }

        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        private static bool TooLong(string? s) => s != null && s.Length > MaxTextLength;

        private static bool IsCleanId(string s)
        {
            foreach (char c in s)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                          || c == ':' || c == '_' || c == '-' || c == '.';
                if (!ok) return false;
            }
            return true;
        }

        /// <summary>Keeps a rejected value from flooding the log or the chat line it is reported on.</summary>
        private static string Trim(string? s)
            => string.IsNullOrEmpty(s) ? "" : (s!.Length <= 40 ? s : s.Substring(0, 40) + "…");
    }
}
