namespace canrpgclasses.Shaman
{
    /// <summary>
    /// Stat keys owned by the shaman: set by its talents, read by its spells and effects. Class-specific, so they
    /// live here rather than in the core StatKeys. The string values keep their original spelling - they are live
    /// stat keys, not display names.
    /// </summary>
    public static class ShamanStatKeys
    {
        /// <summary>Multiplier on Forked Lightning's damage, raised by Storm Reach.</summary>
        public const string ForkedLightningDamage = "chainLightningDamage";

        /// <summary>Multiplier on Magma Burst's damage, raised by Magma Flow.</summary>
        public const string MagmaBurstDamage = "lavaBurstDamage";

        /// <summary>Multiplier on every weapon imbue's on-hit rider - the burn, the chill, the extra swing. Baked
        /// into the imbue's snapshot.</summary>
        public const string ImbuePower = "imbuePower";

        /// <summary>Multiplier on Thunder Cleave's damage, raised by its mastery talent.</summary>
        public const string ThunderCleaveDamage = "stormstrikeDamage";

        /// <summary>Multiplier on the reactive shields' procs - Static Shield's shock, Stone Ward's heal. Baked
        /// into each shield's snapshot at apply time.</summary>
        public const string ShieldPower = "shieldPower";
    }
}
