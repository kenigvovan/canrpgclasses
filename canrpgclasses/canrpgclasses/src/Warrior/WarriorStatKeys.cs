namespace canrpgclasses.Warrior
{
    /// <summary>Warrior stat and WatchedAttributes keys. The string values keep their old spelling because they are
    /// live persisted keys.</summary>
    public static class WarriorStatKeys
    {
        /// <summary>1.0-based multiplier on rage gained from landing melee hits (Fury mastery).</summary>
        public const string RageGeneration = "rageGeneration";

        /// <summary>Multiplier on Brutal Strike's damage, raised by Precise Strikes.</summary>
        public const string BrutalDamage = "heroicDamage";

        /// <summary>Multiplier on Maiming Strike's damage, raised by Precise Strikes.</summary>
        public const string MaimingDamage = "mortalStrikeDamage";

        /// <summary>Multiplier on Death Blow's damage, raised by the Headsman capstone.</summary>
        public const string DeathBlowDamage = "executeDamage";

        // ---- WatchedAttributes ----
        /// <summary>ElapsedMilliseconds of the last rage-granting hit; rate-limits rage since one swing can ping
        /// ReceiveDamage several times before the i-frame.</summary>
        public const string LastRageHitMs = "canrpgLastRageHitMs";

        /// <summary>Stance last rewarded with switch rage, so re-toggling the same stance grants nothing.</summary>
        public const string LastStance = "canrpgLastStance";

        /// <summary>Enrage (Frenzy) ICD deadline in ElapsedMilliseconds; keeps Enrage off permanent uptime.</summary>
        public const string EnrageIcdMs = "canrpgEnrageIcdMs";

        /// <summary>Retort ICD deadline in ElapsedMilliseconds.</summary>
        public const string RevengeIcdMs = "canrpgRevengeIcdMs";

        /// <summary>True if the proc is still on ICD. ElapsedMilliseconds resets per session, so a deadline further
        /// than icdMs away is a stale value from an earlier session and counts as expired.</summary>
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
