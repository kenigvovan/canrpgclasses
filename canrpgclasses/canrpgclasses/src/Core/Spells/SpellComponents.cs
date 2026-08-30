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

        /// <summary>Ground visual for an Area spell: a particle carpet over the affected disc, emitted once per
        /// cast no matter how many targets are hit. Null = none.</summary>
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

        /// <summary>After the heal lands, remove the first of these effects the target carries (Instant Mend eats
        /// its own heal-over-time). Order is the consumption priority; no-op if the target carries none.</summary>
        public string[]? ConsumeTargetEffectIds;

        // Chain: the effect jumps on from the PREVIOUS target, losing amplitude each hop - damage to the nearest
        // enemy, heals to the most wounded party ally. The direct hit's riders (shatter, splash, consumption) do
        // not re-run per jump: a chain must not multiply what it paid for once. 0 = off.
        public int ChainJumps;
        /// <summary>Search radius for the next hop, measured from the current link (not the caster).</summary>
        public float ChainRadius = 6f;
        /// <summary>Amplitude multiplier applied per hop (0.7 = each jump lands at 70% of the previous one).</summary>
        public float ChainFalloff = 0.7f;

        // SkillFx.RingWave: an expanding ground ring where the impact lands. Save it for the big moments, or it
        // stops reading as an accent.
        public bool LandFxRing;
        public float LandFxRadius = 1.5f;

        /// <summary>SkillFx.Arc: a jagged bolt from caster to target, so the shot itself is visible.</summary>
        public bool BoltFx;

        // More one-shot SkillFx accents on a Damage impact, all fired by ApplyDamage on the struck target:
        /// <summary>SkillFx.Trail: a mote streaks caster to target - a projectile-less nuke visibly travels.</summary>
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

        // ToggleAura: primary-resource drain per second. EBAuras pays it each tick and toggles the aura off when
        // the pool can't cover it. 0 = free.
        public float AuraUpkeepPerSecond;

        // ToggleAura: buff the caster only, never party allies - a personal combat mode (warrior Stances).
        public bool AuraSelfOnly;

        // GainResource: flat amount of the target's class primary resource granted on this impact (warrior rage).
        public float ResourceGainAmount;

        // Damage: adds coefficient × SpellPower × the target's missing-health fraction - the mirror of
        // HealMissingHealthFraction. 0 = off.
        public float DamageMissingHealthCoefficient;

        // Atonement splash: a share of this hit also heals the caster's party allies in range. The share comes
        // from the caster's DamageSplashHealStat (0-based), so the rider sits inert until a talent sets it.
        // DamageSplashRequiresShield limits it to allies carrying an absorb shield. Null stat = off.
        public string? DamageSplashHealStat;
        public float DamageSplashHealRadius = 30f;
        public bool DamageSplashRequiresShield = true;

        // Shatter bonus: multiply this hit when the target is in a control state the spell itself nominates -
        // a WA flag (DamageVsControlFlag) or a carried effect (DamageVsControlEffectId). Both set = either
        // matches. 1 or 0 = no bonus.
        public float DamageVsControlledMultiplier;
        public string? DamageVsControlFlag;
        public string? DamageVsControlEffectId;
        // A hit that triggered the bonus consumes DamageVsControlEffectId, so a target shatters once per
        // application. A WA-flag control is left intact.
        public bool ConsumeControlOnHit;

        // Fire this impact only if the caster has the talent - lets a talent bolt a rider onto an existing spell.
        public string? RequiresTalentId;

        // Apply to the enemy under the crosshair instead of the resolved target: for a Caster-target spell that
        // still wants to hit what it aims at (Charge's stun).
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

        // Stun (players only): also drives the game's "psychedelic" screen distortion - the mushroom one -
        // restoring whatever value was there before once the stun ends.
        public bool StunBlindsVision;
        public float StunVisionIntensity = 1f;

        // DamageZone: a lingering ground zone. Damage re-applies every ZoneTickSeconds; the particle carpet
        // refreshes on its own finer cadence, so it looks continuous instead of pulsing once per damage tick.
        public float ZoneDurationSeconds = 6f;
        public float ZoneTickSeconds = 1f;
        public float ZoneParticleSeconds = 0.4f;
        // Placement: the zone lands on the caster's feet, or under the crosshair when ZoneAtAimPoint is set.
        // ZoneRadius > 0 decouples the radius from Range, which then means the max cast DISTANCE.
        public bool ZoneAtAimPoint;
        public float ZoneRadius;

        // A once-per-cast accent at the zone centre - a zone has no single struck target, so the per-target flags
        // above don't apply. Rune brands a ground sigil, Ring sends out an expanding ring the size of the zone.
        public bool ZoneLandRune;
        public bool ZoneLandRing;

        // Status payload for enemies inside: every tick for a ticking zone, once on trigger for a trap.
        public string? ZoneStatusEffectId;
        public int ZoneStatusTier = 1;
        public float ZoneStatusSeconds = 2f;

        // Trap mode: instead of ticking, the zone sits armed until an enemy steps within ZoneTriggerRadius, then
        // fires once over the blast disc and is gone. Allies and the caster don't trip it.
        public bool ZoneIsTrap;
        public float ZoneTriggerRadius = 1.5f;
        // Optional sound played at the trap when it fires (e.g. an explosion boom). Missing asset = silent, no crash.
        public string? ZoneTriggerSound;

        // EmpowerNextShot (hunter Split Shot): extra vanilla arrows the bow looses alongside the main one when
        // the armed shot fires (0 = a single empowered arrow). Spawned by BowPatches on bow release.
        public int ShotExtraArrows;

        // EmpowerNextShot: require the arrow to be drawn to at least this fraction of a full draw (>1 = held past
        // full), the draw having started after the ability was pressed. A fraction, not seconds, so it follows
        // the bow's real draw time. 0 = any shot in the window.
        public float ShotRequiresDrawFraction;

        // EmpowerNextShot debuffs landed on the arrow's hit, see HunterShots.ConsumeOnArrowHit.
        public float ShotHealCutPercent;
        public float ShotHealCutSeconds;
        public float ShotResourceDrain;
        public bool ShotIgnite;

        // ResetCooldowns (Regroup): spell ids whose cooldown is cleared on the caster.
        public string[]? ResetCooldownKeys;

        // PetCommand (hunter pet orders): which order to issue - "aggressive" / "defensive" / "passive" (mode
        // switches), "attack" (send the pet at the aimed target), "come" (recall the pet to the owner).
        public string? PetCommand;

        // PlaceTotem (shaman): which totem to plant - "searing" (fire turret), "stream" (group heal pulse),
        // "earth" (melee-damage buff pulse). Lifetime rides StatusEffectDuration, reach rides ZoneRadius.
        public string? TotemKind;

        // Evasion: chance (0..1) to fully dodge an incoming physical attack, lasting StatusEffectDuration seconds.
        public float EvasionChance = 0.5f;

        /// <summary>Particle burst where the impact lands - on the target, or on the caster for a self-buff.</summary>
        public ParticleSpec? Particles;

        /// <summary>The shape every stealth spell builds. Invisibility has no stacking tiers, so it is always
        /// amplifier 1; only the duration and the target vary.</summary>
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
