using System.Collections.Generic;

namespace canrpgclasses.Core.Spells
{
    /// <summary>How a spell selects its targets.</summary>
    public class SpellTarget
    {
        public TargetType Type = TargetType.Caster;
        public int Cap = 1;

        /// <summary>Which side this spell may affect (Aim/Area). Default Enemy. Ally = friendly smart-cast.</summary>
        public TargetAffinity Affinity = TargetAffinity.Enemy;

        // Aim
        public bool AimRequired;

        // Area
        public float AreaHorizontalRangeMultiplier = 1f;
        public float AreaVerticalRangeMultiplier = 1f;
        public bool AreaIncludeCaster;

        /// <summary>Area spell ground carpet, emitted once per cast. Null = none.</summary>
        public ParticleSpec? AreaParticles;
    }

    /// <summary>How a spell reaches its targets.</summary>
    public class SpellDelivery
    {
        public DeliveryType Type = DeliveryType.Direct;
        public int DelayTicks;

        // Projectile
        public float ProjectileVelocity = 0.8f;
        /// <summary>Projectile entity to spawn: "spellprojectile" is the spinning knife, "spellorb" a gravity-free
        /// glowing ball with a per-school trail. Both are <c>EntitySpellProjectile</c>.</summary>
        public string ProjectileEntity = "spellprojectile";
        public int ProjectileCount = 1;
        /// <summary>For ProjectileCount>1: the horizontal arc the projectiles fan over; 360 = a full ring.</summary>
        public float ProjectileSpreadDegrees;

        // StashEffect: stores an effect on the caster that fires its impacts on the given triggers.
        public string? StashEffectId;
        public List<SpellTrigger> StashTriggers = new List<SpellTrigger>();
    }

    /// <summary>A single effect applied to a resolved target.</summary>
    public class SpellImpact
    {
        public float Chance = 1f;
        public ImpactAction Action;

        /// <summary>Apply this impact to the caster instead of the resolved target (e.g. self-buff on an Aim spell).</summary>
        public bool AffectCaster;

        // Damage
        public float DamageSpellPowerCoefficient = 1f;
        public float Knockback = 1f;
        /// <summary>Combo finisher: extra damage coefficient added per spent combo point (see ResourceState).</summary>
        public float DamagePerComboPoint;
        /// <summary>Combo finisher: extra seconds added to this impact's StatusEffect/Stun duration per spent combo point.</summary>
        public float DurationPerComboPoint;

        // Heal
        public float HealSpellPowerCoefficient = 1f;
        /// <summary>Combo finisher: extra heal coefficient added per spent combo point (e.g. Holy Power for Radiant Word).</summary>
        public float HealPerComboPoint;
        /// <summary>Heals this fraction of the target's MISSING health on top of the spell-power base. 0 = off.</summary>
        public float HealMissingHealthFraction;

        /// <summary>After the heal, remove the first of these effects the target carries (in priority order).</summary>
        public string[]? ConsumeTargetEffectIds;

        // Chain hops from the previous target (damage: nearest enemy, heal: most wounded ally). The direct hit's
        // riders (shatter, splash, consumption) don't re-run per hop. 0 = off.
        public int ChainJumps;
        /// <summary>Search radius for the next hop, measured from the current link (not the caster).</summary>
        public float ChainRadius = 6f;
        /// <summary>Amplitude multiplier applied per hop (0.7 = each jump lands at 70% of the previous one).</summary>
        public float ChainFalloff = 0.7f;

        // SkillFx.RingWave: expanding ground ring where the impact lands.
        public bool LandFxRing;
        public float LandFxRadius = 1.5f;

        /// <summary>SkillFx.Arc: a jagged bolt from caster to target, so the shot itself is visible.</summary>
        public bool BoltFx;

        // One-shot SkillFx accents fired by ApplyDamage on the struck target.
        /// <summary>SkillFx.Trail: a mote streaks caster to target.</summary>
        public bool TrailFx;
        /// <summary>SkillFx.Pillar: a column strikes down on the target (Sacred Flame).</summary>
        public bool PillarFx;
        /// <summary>SkillFx.Nova: an expanding shell bursts at the target's chest (Earthen Jolt).</summary>
        public bool NovaFx;
        /// <summary>SkillFx.Spark: a sharp burst flies off the target, away from the caster (Mind Shock).</summary>
        public bool SparkFx;
        /// <summary>SkillFx.Rune: a flat ground sigil is branded under the target (Sentence).</summary>
        public bool RuneFx;
        /// <summary>SkillFx.Bloom: a rising fountain of petals at the healed target - fires in ApplyHeal (Swift Heal).</summary>
        public bool BloomFx;

        // StatusEffect (applied through the effectshud system)
        public string? StatusEffectId;
        public float StatusEffectDuration;
        public int StatusEffectAmplifier = 1;
        public int StatusEffectAmplifierCap = 1;
        public StatusApplyMode StatusEffectApplyMode = StatusApplyMode.Set;

        // ToggleAura: resource drain per second; the aura drops when the pool can't pay. 0 = free.
        public float AuraUpkeepPerSecond;

        // ToggleAura: buff the caster only, never party allies - a personal combat mode (warrior Stances).
        public bool AuraSelfOnly;

        // GainResource: flat amount of the target's class primary resource granted on this impact (warrior rage).
        public float ResourceGainAmount;

        // Damage: adds coefficient × SpellPower × the target's missing-health fraction - the mirror of
        // HealMissingHealthFraction. 0 = off.
        public float DamageMissingHealthCoefficient;

        // Atonement: a share of the hit heals party allies in range. Share = caster's DamageSplashHealStat (0-based),
        // so it's inert until a talent sets it. Null stat = off.
        public string? DamageSplashHealStat;
        public float DamageSplashHealRadius = 30f;
        public bool DamageSplashRequiresShield = true;

        // Shatter: multiply the hit if the target has the WA flag or the effect (either matches). 0 or 1 = off.
        public float DamageVsControlledMultiplier;
        public string? DamageVsControlFlag;
        public string? DamageVsControlEffectId;
        // Consume the effect on a shatter so it triggers once per application; WA-flag controls stay.
        public bool ConsumeControlOnHit;

        // Fire this impact only if the caster has the talent - lets a talent bolt a rider onto an existing spell.
        public string? RequiresTalentId;

        // Hit the enemy under the crosshair instead of the resolved target (Charge's stun on a Caster-target spell).
        public bool AffectAimedEnemy;

        // Teleport
        public TeleportMode TeleportMode = TeleportMode.Forward;
        public float TeleportDistance = 1.5f;
        /// <summary>After teleporting, turn the caster to face the target (e.g. shadow_step lands you facing its back).</summary>
        public bool TeleportFaceTarget;

        // CoatWeapon (simplealchemy "simplepoisoned"): potionId = StatusEffectId, tier = StatusEffectAmplifier.
        public int CoatCharges = 6;

        // Dash: leap away from the look direction instead of toward the target (disengage).
        public bool DashReverse;
        // Dash with no aimed target: strength of the view-direction leap and its arc. 0 = the default short hop.
        public float DashForwardStrength;
        public float DashUpward;

        // EmpowerNextMelee: how long the buffed next-strike stays armed before it expires (seconds).
        public float EmpowerWindowSeconds = 10f;

        // StatusEffect (invisibility): whether the bearer attacking ends it. Passed to InvisibilityEffect.
        public bool InvisBreaksOnAttack = true;

        // Shield: absorb pool = SpellPower × this coefficient, lasting ShieldDurationSeconds.
        public float ShieldSpellPowerCoefficient = 1f;
        public float ShieldDurationSeconds = 10f;
        // Dome look for ShieldDomeRenderer: -1 = auto by spell school (frost = ice crystal, else smooth dome);
        // 0 = smooth energy sphere, 1 = thin ice crystal (Ice Ward), 2 = solid opaque ice encasement (Frozen Shell).
        public int ShieldDomeStyle = -1;

        // Cleanse: max negative effects removed (0 = all).
        public int CleanseMax;

        // Aggro/taunt: radius around the caster within which mobs are forced to target the caster.
        public float AggroRange = 8f;

        // Stun: when true the stun is a Blind/disorient that ENDS the moment the target takes damage.
        public bool StunBreaksOnDamage;

        // Stun (players only): also drives the vanilla "psychedelic" distortion, restored to its prior value after.
        public bool StunBlindsVision;
        public float StunVisionIntensity = 1f;

        // DamageZone: damage every ZoneTickSeconds; particles refresh on their own finer cadence so they don't pulse.
        public float ZoneDurationSeconds = 6f;
        public float ZoneTickSeconds = 1f;
        public float ZoneParticleSeconds = 0.4f;
        // Placement: the zone lands on the caster's feet, or under the crosshair when ZoneAtAimPoint is set.
        // ZoneRadius > 0 decouples the radius from Range, which then means the max cast DISTANCE.
        public bool ZoneAtAimPoint;
        public float ZoneRadius;

        // Once-per-cast accents at the zone centre (the per-target Fx flags don't apply to zones).
        public bool ZoneLandRune;
        public bool ZoneLandRing;

        // Status payload for enemies inside: every tick for a ticking zone, once on trigger for a trap.
        public string? ZoneStatusEffectId;
        public int ZoneStatusTier = 1;
        public float ZoneStatusSeconds = 2f;

        // Trap mode: armed until an enemy enters ZoneTriggerRadius, then fires once and is gone.
        public bool ZoneIsTrap;
        public float ZoneTriggerRadius = 1.5f;
        // Optional sound when the trap fires. Missing asset = silent.
        public string? ZoneTriggerSound;

        // EmpowerNextShot: extra vanilla arrows loosed with the armed shot, spawned by BowPatches on release.
        public int ShotExtraArrows;

        // EmpowerNextShot: min draw as a fraction of full draw (>1 = held past full), started after arming.
        // A fraction so it follows the bow's real draw time. 0 = any shot.
        public float ShotRequiresDrawFraction;

        // EmpowerNextShot debuffs landed on the arrow's hit, see HunterShots.ConsumeOnArrowHit.
        public float ShotHealCutPercent;
        public float ShotHealCutSeconds;
        public float ShotResourceDrain;
        public bool ShotIgnite;

        // ResetCooldowns (Regroup): spell ids whose cooldown is cleared on the caster.
        public string[]? ResetCooldownKeys;

        // PetCommand: "aggressive" / "defensive" / "passive" / "attack" (aimed target) / "come".
        public string? PetCommand;

        // PlaceTotem: "searing" / "stream" / "earth". Lifetime = StatusEffectDuration, reach = ZoneRadius.
        public string? TotemKind;

        // OpenPortal: Gate = caster <-> aim point, Waypoint = caster <-> anchor. Tints are 0xRRGGBB, 0 = arcane violet.
        public canrpgclasses.Core.Portals.PortalKind PortalKind;
        public int PortalTintA, PortalTintB;
        // Gate: max height between the two ends. Waypoint: max distance to the anchor (0 = unlimited).
        public float PortalMaxHeightDiff, PortalMaxDistance;

        // Evasion: chance (0..1) to fully dodge an incoming physical attack, lasting StatusEffectDuration seconds.
        public float EvasionChance = 0.5f;

        /// <summary>Particle burst where the impact lands - on the target, or on the caster for a self-buff.</summary>
        public ParticleSpec? Particles;

        /// <summary>Shared invisibility impact; it has no stacking tiers, so amplifier is always 1.</summary>
        public static SpellImpact Invisibility(float duration, bool affectCaster = false) => new SpellImpact
        {
            Action = ImpactAction.StatusEffect,
            AffectCaster = affectCaster,
            StatusEffectId = "invisibility",
            StatusEffectDuration = duration,
            StatusEffectAmplifier = 1,
            StatusEffectAmplifierCap = 1
        };
    }

    /// <summary>Resource/cooldown cost paid on a successful cast.</summary>
    public class SpellCost
    {
        public float Exhaust;
        /// <summary>Primary resource spent on cast, 0 = free. Which resource comes from the caster's class, not
        /// the spell; EBSpellCaster checks and pays it.</summary>
        public float Resource;
        public CooldownConfig Cooldown = new CooldownConfig();
    }

    public class CooldownConfig
    {
        /// <summary>Optional shared cooldown group; null = the spell's own id.</summary>
        public string? Group;
        public float Duration;
    }

    /// <summary>Condition under which a stashed effect or passive fires its impacts.</summary>
    public class SpellTrigger
    {
        public TriggerType Type;
        public TargetSelector TargetOverride = TargetSelector.Caster;
    }
}
