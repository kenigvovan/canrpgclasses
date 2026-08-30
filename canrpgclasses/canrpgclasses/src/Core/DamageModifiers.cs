using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.Core
{
    /// <summary>One registered damage perk. Runs in the victim's ReceiveDamage prefix on every hit, before core
    /// mitigation, so an implementation must check its own talent rank and bail fast.</summary>
    public delegate void DamageModifier(Entity victim, Entity? attacker, DamageSource source, ref float damage);

    /// <summary>
    /// Registry of class-content damage perks applied in the vanilla damage pipeline, so that pipeline never
    /// references class content by id. Published as an immutable array behind a volatile reference: in
    /// singleplayer both sides share these statics, so the server tick can read while a client scan republishes.
    /// </summary>
    public static class DamageModifiers
    {
        private static volatile DamageModifier[] current = Array.Empty<DamageModifier>();
        private static volatile DamageModifier[] persistent = Array.Empty<DamageModifier>();

        /// <summary>Atomically replaces the registered talent perks with a freshly scanned set. Persistent
        /// hooks (see <see cref="RegisterPersistent"/>) are unaffected.</summary>
        public static void Publish(IEnumerable<DamageModifier> modifiers)
            => current = new List<DamageModifier>(modifiers).ToArray();

        /// <summary>Registers a hook that survives republishes, for class content not tied to a talent rank.
        /// Idempotent by delegate identity, so re-running module init doesn't double-register.</summary>
        public static void RegisterPersistent(DamageModifier modifier)
        {
            var cur = persistent;
            if (Array.IndexOf(cur, modifier) >= 0) return;
            var next = new DamageModifier[cur.Length + 1];
            Array.Copy(cur, next, cur.Length);
            next[cur.Length] = modifier;
            persistent = next;
        }

        public static void Apply(Entity victim, Entity? attacker, DamageSource source, ref float damage)
        {
            // Talents live only on players, so a rank read on anything else is 0. Skipping the loop for mob-vs-mob
            // hits saves a read per perk on the bulk of ReceiveDamage traffic in a populated world.
            if (victim is EntityPlayer || attacker is EntityPlayer)
            {
                var mods = current;
                for (int i = 0; i < mods.Length; i++) mods[i](victim, attacker, source, ref damage);
            }
            // Persistent hooks can fire with no player in the hit at all (pet vs mob), so they always run.
            var pers = persistent;
            for (int i = 0; i < pers.Length; i++) pers[i](victim, attacker, source, ref damage);
        }

        /// <summary>True when the attacker stands roughly behind the target's facing - shared positional
        /// check for perks like Blindside / Exposed Flesh.</summary>
        public static bool IsBehind(Entity target, Entity attacker)
        {
            if (target?.Pos == null || attacker?.Pos == null) return false;
            float yaw = target.Pos.Yaw;
            // Matches EntityControls.CalcMovementVectors' forward, not EntityPos.GetViewVector: the two are
            // opposite in sign, and the view-vector convention inverted this check - front hits read as behind.
            double fx = Math.Sin(yaw);
            double fz = Math.Cos(yaw);
            double ax = attacker.Pos.X - target.Pos.X;
            double az = attacker.Pos.Z - target.Pos.Z;
            double len = Math.Sqrt(ax * ax + az * az);
            if (len < 1e-3) return false;
            double dot = (fx * ax + fz * az) / len;
            return dot < -0.3;
        }
    }
}
