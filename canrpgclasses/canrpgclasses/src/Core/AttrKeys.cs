namespace canrpgclasses.Core
{
    /// <summary>
    /// This mod's persistent WatchedAttributes keys. The string values key into saved player state: rename the
    /// C# symbols freely, never the literals, or existing characters lose their class, talents and progress.
    /// </summary>
    public static class AttrKeys
    {
        // ---- Class selection (ClassChange) ----
        public const string ClassChosen = "canrpgClassChosen";
        public const string ClassChangedMs = "canrpgClassChangedMs";
        public const string ClassFreeRepick = "canrpgClassFreeRepick";

        // ---- Talents / current class (TalentState) ----
        public const string TalentRanks = "canrpgTalents";
        public const string CurrentClass = "canrpgClass";

        // ---- Resources (ResourceState) ----
        public const string Combo = "canrpgCombo";
        public const string ComboLastMs = "canrpgComboLastMs";

        // ---- Auras (EBAuras) ----
        public const string ActiveAura = "canrpgActiveAura";

        // ---- Cooldowns (EBSpellCooldowns) ----
        public const string Cooldowns = "canrpgCooldowns";

        // ---- Empowered next strike, stored on the caster and consumed by EBSpellCaster.DidAttack (SpellExecutor) ----
        public const string EmpowerDamage = "canrpgEmpowerDamage";
        // Death Blow-style deferred bonus: the MAX missing-health part (at 100% missing), scaled by the struck target's
        // actual missing fraction when the empowered swing lands. 0/unset for normal empowers.
        public const string EmpowerMissingDamage = "canrpgEmpowerMissingDmg";
        public const string EmpowerKnockback = "canrpgEmpowerKnockback";
        public const string EmpowerCombo = "canrpgEmpowerCombo";
        public const string EmpowerUntilMs = "canrpgEmpowerUntilMs";
        public const string EmpowerSpell = "canrpgEmpowerSpell";
        public const string EmpowerSchool = "canrpgEmpowerSchool";

        // ---- Our own weapon-coating tag (envenom/crippling_oil/seal_of_light) ----
        public const string WeaponCoat = "canrpgWeaponCoat";

        // The hunter's pet stores its owner's uid under the vanilla key the guard AI tasks read, so one value
        // drives follow behaviour and lets IsAlly treat the pet as friendly to its owner and their party.
        public const string GuardedByPlayerUid = "guardedPlayerUid";

        // ---- Empowered next bow shot: armed by a shot ability, consumed by the next arrow the caster fires.
        // Bonus damage/status/combo land on the arrow's hit; the extra-arrow count is consumed on release. ----
        public const string ShotDamage = "canrpgShotDamage";
        public const string ShotKnockback = "canrpgShotKnockback";
        public const string ShotComboGrant = "canrpgShotComboGrant";
        public const string ShotStatusId = "canrpgShotStatusId";
        public const string ShotStatusDur = "canrpgShotStatusDur";
        public const string ShotStatusAmp = "canrpgShotStatusAmp";
        public const string ShotSpell = "canrpgShotSpell";
        public const string ShotExtra = "canrpgShotExtra";
        public const string ShotUntilMs = "canrpgShotUntilMs";
        // aimed_shot: the moment the shot was armed + the min draw seconds it demands. The empowered arrow only
        // counts if the bow was drawn at least this long and the draw STARTED after arming (no pre-drawing).
        public const string ShotArmedMs = "canrpgShotArmedMs";
        public const string ShotFullDraw = "canrpgShotFullDraw";
        // Debuffs the empowered arrow lands on hit: wounding cuts incoming healing (fraction, for seconds);
        // draining spends this much of the victim's primary resource; ignite sets them on fire.
        public const string ShotHealCut = "canrpgShotHealCut";
        public const string ShotHealCutSecs = "canrpgShotHealCutSecs";
        public const string ShotDrain = "canrpgShotDrain";
        public const string ShotIgnite = "canrpgShotIgnite";

        // ---- Dynamic (per-class / per-pool) keys - the prefix lives here so it can't drift across call sites ----

        /// <summary>Per-class XP total (progress follows the entity's active class).</summary>
        public static string Xp(string classId) => "canrpgXp_" + classId;

        public static string Level(string classId) => "canrpgLevel_" + classId;

        public static string ResourcePool(string poolId) => "canrpgRes_" + poolId;
    }
}
