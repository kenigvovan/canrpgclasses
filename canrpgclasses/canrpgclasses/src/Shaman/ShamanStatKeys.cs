namespace canrpgclasses.Shaman
{
    /// <summary>Shaman stat keys. The string values keep their old spelling because they are live stat keys.</summary>
    public static class ShamanStatKeys
    {
        /// <summary>Multiplier on Forked Lightning's damage, raised by Storm Reach.</summary>
        public const string ForkedLightningDamage = "chainLightningDamage";

        /// <summary>Multiplier on Magma Burst's damage, raised by Magma Flow.</summary>
        public const string MagmaBurstDamage = "lavaBurstDamage";

        /// <summary>Multiplier on every weapon imbue's on-hit rider, baked into the snapshot.</summary>
        public const string ImbuePower = "imbuePower";

        /// <summary>Multiplier on Thunder Cleave's damage, raised by its mastery talent.</summary>
        public const string ThunderCleaveDamage = "stormstrikeDamage";

        /// <summary>Multiplier on Static Shield and Stone Ward procs, baked into the snapshot.</summary>
        public const string ShieldPower = "shieldPower";
    }
}
