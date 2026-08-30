namespace canrpgclasses.Core.Spells
{
    public enum SpellType
    {
        Active,
        Passive,
        Modifier
    }

    /// <summary>
    /// Magic schools - the mod's own damage classification; resists, talents and immunities key off it.
    /// <see cref="DamageSchools"/> maps it to an engine damage type only for armor and built-in mechanics.
    /// </summary>
    public enum SpellSchool
    {
        PhysicalMelee,
        PhysicalRanged,
        Fire,
        Frost,
        Arcane,
        Holy,
        Nature,
        Shadow,
        Fel
    }

    public enum CastMode
    {
        Instant,
        Charge,
        Channel
    }

    public enum TargetType
    {
        None,
        Caster,
        Aim,
        Area,
        Beam,
        FromTrigger
    }

    /// <summary>Which side a targeted spell may affect. Ally smart-casts: the allied player under the crosshair,
    /// else the caster. "Ally" is the crude heuristic - any other player; mobs are enemies.</summary>
    public enum TargetAffinity
    {
        Enemy,
        Ally,
        Any
    }

    public enum DeliveryType
    {
        Direct,
        Projectile,
        Cloud,
        Meteor,
        Melee,
        StashEffect,
        ShootArrow,
        AffectArrow
    }

    public enum ImpactAction
    {
        Damage,
        Heal,
        StatusEffect,
        Teleport,
        Spawn,
        Cooldown,
        Aggro,
        /// <summary>Coat the caster's held weapon (simplealchemy "simplepoisoned": applied on melee hit).</summary>
        CoatWeapon,
        /// <summary>Launch the caster toward the aimed target (or view direction) with a knockback-style impulse.</summary>
        Dash,
        /// <summary>Freeze the target: lock movement and block AI tasks for a duration (real stun, no effectshud).</summary>
        Stun,
        /// <summary>Buff the caster's next melee hit with bonus damage (+ combo) instead of dealing damage now (sinister_strike).</summary>
        EmpowerNextMelee,
        /// <summary>Disarm: knock a player's active weapon into another hotbar slot (no ground loot); mobs get a weakmelee debuff.</summary>
        Disarm,
        /// <summary>Absorb shield: stores a damage-absorbing pool on the target (canrpgAbsorb), soaked by the damage patch.</summary>
        Shield,
        /// <summary>Cleanse: remove negative effectshud effects from the target.</summary>
        Cleanse,
        /// <summary>Lingering ground zone: re-applies AoE damage and its particle carpet every tick.</summary>
        DamageZone,
        /// <summary>Clears the caster's cooldowns for the spells named in <see cref="SpellImpact.ResetCooldownKeys"/>.</summary>
        ResetCooldowns,
        /// <summary>A chance to fully dodge incoming physical attacks, rolled in the damage patch.</summary>
        Evasion,
        /// <summary>Toggle a persistent aura on or off. <see cref="canrpgclasses.Core.EB.EBAuras"/> keeps its effect
        /// refreshed on nearby party allies; the spell's Range is the radius.</summary>
        ToggleAura,
        /// <summary>Redirect this impact onto the caster's companion instead of the resolved target. Handled by
        /// the Hunter module, not the executor.</summary>
        PetTarget,
        /// <summary>Arm the caster's next real bow shot: the arrow they loose carries this impact. Consumed by
        /// <c>HunterShots</c>, not the executor.</summary>
        EmpowerNextShot,
        /// <summary>End break-on-attack invisibility on every enemy in range - flushes out stealthed foes.</summary>
        Reveal,
        /// <summary>Order the caster's companion around; the order rides <see cref="SpellImpact.PetCommand"/>.</summary>
        PetCommand,
        /// <summary>Grant the target a flat amount of its class primary resource. Handled by the Warrior module,
        /// so it is a no-op for a class without it.</summary>
        GainResource,
        /// <summary>Make the target flee: mobs run their flee task, a player loses control and wanders. Goes
        /// through <see cref="canrpgclasses.Core.Control.ControlState"/>, sharing DR with Stun.</summary>
        Fear,
        /// <summary>Block the target from casting. Control layer, own DR bracket.</summary>
        Silence,
        /// <summary>The target can't move, but can still cast and attack. Control layer, own DR bracket.</summary>
        Root,
        /// <summary>Full incapacitate that ends on the first damage taken; players also turn into a sheep.</summary>
        Transmute,
        /// <summary>Let the caster's next Charge cast ignore its cast time. StatusEffectId, if set, is the HUD
        /// marker for the window.</summary>
        GrantInstantCast,
        /// <summary>Plant a shaman totem: a stationary killable summon pulsing an effect around itself. Separate
        /// from <see cref="Spawn"/>, whose one handler already belongs to the hunter's pet.</summary>
        PlaceTotem
    }

    public enum TriggerType
    {
        SpellCast,
        MeleeImpact,
        DamageTaken,
        ArrowShot,
        ArrowImpact,
        SpellImpact
    }

    public enum StatusApplyMode
    {
        /// <summary>Refresh to the given amplifier/duration.</summary>
        Set,
        /// <summary>Add one stack up to a cap.</summary>
        Add
    }

    public enum TeleportMode
    {
        Forward,
        BehindTarget
    }

    public enum TargetSelector
    {
        Caster,
        Target
    }
}
