namespace canrpgclasses.Warrior
{
    /// <summary>Warrior stat and WatchedAttributes keys, kept in the Warrior module so the core carries no warrior
    /// content. The string values keep their original spelling - they are live stat keys, not display names.</summary>
    public static class WarriorStatKeys
    {
        /// <summary>1.0-based multiplier on rage GAINED from landing melee hits (Fury tree mastery). Read by
        /// <see cref="WarriorRage"/> when a warrior's swing lands. Default (unset) blends to 1.0 → no bonus.</summary>
        public const string RageGeneration = "rageGeneration";

        /// <summary>Multiplier on Brutal Strike's damage, raised by Precise Strikes.</summary>
        public const string BrutalDamage = "heroicDamage";

        /// <summary>Multiplier on Maiming Strike's damage, raised by Precise Strikes.</summary>
        public const string MaimingDamage = "mortalStrikeDamage";

        /// <summary>Multiplier on Death Blow's damage, raised by the Headsman capstone.</summary>
        public const string DeathBlowDamage = "executeDamage";

        // ---- WatchedAttributes ----
        /// <summary>Timestamp (ElapsedMilliseconds) of the warrior's last rage-granting melee hit, so the rage
        /// hook can rate-limit and not farm rage off a single swing that pings ReceiveDamage before the i-frame.</summary>
        public const string LastRageHitMs = "canrpgLastRageHitMs";

        /// <summary>The stance the warrior was last rewarded rage for entering, so switching to a DIFFERENT stance
        /// grants the switch bonus but re-toggling the same stance doesn't (see WarriorRage stance-switch reward).</summary>
        public const string LastStance = "canrpgLastStance";

        /// <summary>ElapsedMilliseconds deadline until which the Enrage proc (Frenzy) can't re-trigger - the ICD that
        /// keeps Enrage from reaching permanent uptime. Read via the relog-safe guard in <see cref="OnIcd"/>.</summary>
        public const string EnrageIcdMs = "canrpgEnrageIcdMs";

        /// <summary>ElapsedMilliseconds deadline until which the Retort proc can't re-reset Shield Strike (its ICD).</summary>
        public const string RevengeIcdMs = "canrpgRevengeIcdMs";

        /// <summary>Relog-safe internal-cooldown check for a proc gated by a WatchedAttribute deadline. Returns true if
        /// the proc is still on cooldown. A fresh ICD is always stored as <c>now + icdMs</c>, so a live ICD has
        /// <c>until - now ≤ icdMs</c>; a value left over from a previous session (World.ElapsedMilliseconds resets on
        /// restart) reads as <c>until - now &gt; icdMs</c> and is treated as expired - so a stale deadline can never
        /// block the proc forever. Call <see cref="ArmIcd"/> on a successful proc to (re)arm it.</summary>
        public static bool OnIcd(Vintagestory.API.Common.Entities.Entity e, string key, float icdSeconds)
        {
            long now = e.World.ElapsedMilliseconds;
            long until = e.WatchedAttributes.GetLong(key, 0);
            long icdMs = (long)(icdSeconds * 1000f);
            return now < until && until - now <= icdMs;
        }

        public static void ArmIcd(Vintagestory.API.Common.Entities.Entity e, string key, float icdSeconds)
            => e.WatchedAttributes.SetLong(key, e.World.ElapsedMilliseconds + (long)(icdSeconds * 1000f));
    }
}
