using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Progression;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Hunter
{
    /// <summary>
    /// Server-side driver for the hunter's wolf: the summon impact, the assist/defend hook, owner-scaled
    /// pet stats, and the Tend Beast / Beast Fury (PetTarget) impacts.
    /// </summary>
    public static class HunterPetSystem
    {
        public const string PetEntityCode = "hunterpet";

        // Owner WA: the summoned pet's entity id (so a resummon can despawn the old one, and death can bump the
        // summon cooldown). Pet WA: guardedPlayerUid (owner uid, also drives the follow task) + wrath window.
        public const string OwnerPetIdKey = "canrpgPetEntityId";
        public const string PetWrathUntilKey = "canrpgPetWrathUntil";
        public const string PetWrathBonusKey = "canrpgPetWrathBonus";

        // Command mode on the pet (set by the pet-command spells, read by the gated AI tasks). Aggressive hunts
        // freely; Defensive only engages within an order/assist window; Passive never attacks on its own.
        public const string PetModeKey = "canrpgPetMode";
        public const string PetAssignUntilKey = "canrpgPetAssignUntil";
        // The foe the pet was last ordered/assisted onto - its AI tasks hold this as their target so a commanded pet
        // doesn't drop a player or distant foe on the vanilla nearest-target re-scan after a single bite.
        public const string PetTargetIdKey = "canrpgPetTargetId";
        // How long after an order/assist the pet is allowed to actively pursue in Defensive mode.
        private const long AssignWindowMs = 6000;

        // 1.0-based owner stats set by Beast Mastery talents; read here to scale the pet.
        public const string PetHealthStat = "hunterPetHealth";
        public const string PetDamageStat = "hunterPetDamage";

        // Stay/hold: the vanilla stayclosetoguardedentity follow task already bails when this WA is set, so setting it
        // pins the pet in place instead of trailing the owner. (Combat/seek still runs per command mode.)
        public const string PetStayKey = "commandSit";
        // The pet's chosen name, kept on the owner (the pet itself doesn't persist across relog) and stamped onto each
        // freshly summoned wolf's nametag.
        public const string PetNameKey = "canrpgPetName";

        private static bool registered;

        /// <summary>Wires the pet content into the core (server side). Idempotent.</summary>
        public static void Init()
        {
            if (registered) return;
            registered = true;
            SpellExecutor.RegisterImpact(ImpactAction.Spawn, ApplySummonPet);
            SpellExecutor.RegisterImpact(ImpactAction.PetTarget, ApplyPetTarget);
            SpellExecutor.RegisterImpact(ImpactAction.PetCommand, ApplyPetCommand);
            // Persistent (survives talent-registry republishes): pet damage scaling + owner assist/defend.
            canrpgclasses.Core.DamageModifiers.RegisterPersistent(PetDamageAndAssistHook);
        }

        // ---- Summon (ImpactAction.Spawn) ----
        private static void ApplySummonPet(SpellContext ctx, Entity target, SpellImpact imp)
            => SummonPet(ctx.Caster);

        public static void SummonPet(EntityAgent owner)
        {
            var world = owner.World;
            if (owner is not EntityPlayer op || op.PlayerUID == null) return;

            long oldId = owner.WatchedAttributes.GetLong(OwnerPetIdKey, 0);
            if (oldId != 0) world.GetEntityById(oldId)?.Die(EnumDespawnReason.Removed);

            var type = world.GetEntityType(new AssetLocation(canrpgclassesModSystem.ModId, PetEntityCode));
            if (type == null || world.ClassRegistry.CreateEntity(type) is not EntityAgent pet) return;

            // Owner link: guardedPlayerUid drives the follow/teleport task and EBHunterPet's despawn check.
            pet.WatchedAttributes.SetString(EBHunterPet.OwnerUidKey, op.PlayerUID);

            // Stamp the chosen name into the nametag tree before spawning, so the client entity is born already
            // carrying it (a post-spawn write races the client building an empty name texture on the first frame).
            string petName = owner.WatchedAttributes.GetString(PetNameKey, null);
            if (!string.IsNullOrEmpty(petName)) ApplyPetName(pet, petName);

            // Spawn a step behind/beside the owner. (Pos is authoritative on the server; Pos/SidedPos
            // are obsolete aliases for it in this VS build.)
            Vec3d pos = owner.Pos.XYZ.AheadCopy(1.0, 0, owner.Pos.Yaw + GameMath.PI);
            pet.Pos.SetPos(pos);
            pet.World = world;
            world.SpawnEntity(pet);

            pet.WatchedAttributes.MarkPathDirty(EBHunterPet.OwnerUidKey);
            owner.WatchedAttributes.SetLong(OwnerPetIdKey, pet.EntityId);
            owner.WatchedAttributes.MarkPathDirty(OwnerPetIdKey);

            int level = owner.GetBehavior<EBProgression>()?.Level ?? 1;
            float baseHp = BalanceConfig.Global("hunterPetBaseHp", 20f);
            float perLevel = BalanceConfig.Global("hunterPetHpPerLevel", 1f);
            float healthMul = System.Math.Max(1f, owner.Stats.GetBlended(PetHealthStat));
            float maxHp = (baseHp + perLevel * System.Math.Max(0, level - 1)) * healthMul;
            pet.GetBehavior<EBHunterPet>()?.SetMaxHealth(maxHp);
        }

        // Writes the name straight into the vanilla nametag WatchedAttributes tree (what EntityBehaviorNameTag reads
        // and renders) and syncs it. Direct tree write rather than GetBehavior().SetName so it also works pre-spawn,
        // before the behavior instance exists.
        private static void ApplyPetName(Entity pet, string name)
        {
            var nt = pet.WatchedAttributes.GetTreeAttribute("nametag");
            if (nt == null) pet.WatchedAttributes["nametag"] = nt = new Vintagestory.API.Datastructures.TreeAttribute();
            nt.SetString("name", name ?? "");
            pet.WatchedAttributes.MarkPathDirty("nametag");
        }

        // ---- PetTarget (Tend Beast / Beast Fury) ----
        private static void ApplyPetTarget(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var pet = GetPet(ctx.Caster);
            if (pet == null) return;

            if (imp.HealSpellPowerCoefficient > 0f)
            {
                float heal = ctx.SpellPower * imp.HealSpellPowerCoefficient;
                if (heal > 0f)
                    pet.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, heal);
            }

            // Beast Fury: a timed damage window on the pet (read by PetDamageAndAssistHook) + a visual buff.
            if (imp.DamageSpellPowerCoefficient > 0f && imp.StatusEffectDuration > 0f)
            {
                // Wild Chase augments Beast Fury: longer window + a bigger damage spike.
                float durMul = 1f, bonusAdd = 0f;
                if (TalentState.Rank(ctx.Caster, "hunter:wild_chase") > 0)
                {
                    var wc = BalanceConfig.Talent("hunter:wild_chase");
                    durMul += wc.F("durationBonus", 0.5f);
                    bonusAdd += wc.F("damageBonus", 0.2f);
                }
                long until = pet.World.ElapsedMilliseconds + (long)(imp.StatusEffectDuration * durMul * 1000f);
                pet.WatchedAttributes.SetLong(PetWrathUntilKey, until);
                pet.WatchedAttributes.SetFloat(PetWrathBonusKey, 1f + imp.DamageSpellPowerCoefficient + bonusAdd);
            }
            if (!string.IsNullOrEmpty(imp.StatusEffectId))
                SpellExecutor.ApplyEffect(pet, imp.StatusEffectId!, imp.StatusEffectAmplifier <= 0 ? 1 : imp.StatusEffectAmplifier, imp.StatusEffectDuration);

            imp.Particles?.Emit(ctx.Caster.World, pet);
        }

        // ---- PetCommand (the pet-order spells) ----
        private static void ApplyPetCommand(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var owner = ctx.Caster;
            var pet = GetPet(owner);
            if (pet == null) return; // no wolf out - the order simply no-ops

            switch (imp.PetCommand)
            {
                case "aggressive": SetPetMode(pet, PetMode.Aggressive); break;
                case "defensive": SetPetMode(pet, PetMode.Defensive); break;
                case "passive": SetPetMode(pet, PetMode.Passive); break;
                case "attack":
                    // Send the wolf at the aimed foe. An explicit order takes a passive pet off passive so it
                    // will actually engage, then the assist path drives it onto the target.
                    if (target != null && target != owner && !IsPet(target) && !SpellExecutor.IsAlly(owner, target))
                    {
                        if (PetModeOf(pet) == PetMode.Passive) SetPetMode(pet, PetMode.Defensive);
                        AssignPetTarget(owner, target);
                    }
                    break;
                case "come":
                    var world = owner.World;
                    Vec3d pos = owner.Pos.XYZ.AheadCopy(1.0, 0, owner.Pos.Yaw + GameMath.PI);
                    pet.TeleportTo(pos);
                    ClearPetTarget(pet); // recall = call off the fight, or the pet just runs back to the target we now persist
                    SetPetStay(pet, false); // a recall resumes following - otherwise a held pet teleports in then just sits
                    imp.Particles?.Emit(world, pet);
                    break;
                case "stay":
                    // Toggle: pin the pet where it stands / release it back to following.
                    SetPetStay(pet, !pet.WatchedAttributes.GetBool(PetStayKey, false));
                    imp.Particles?.Emit(owner.World, pet);
                    break;
                case "dismiss":
                    imp.Particles?.Emit(owner.World, pet);
                    DismissPet(owner, pet);
                    break;
            }
        }

        /// <summary>Player-initiated put-away of the wolf. Unlike a battlefield death this carries NO summon
        /// lockout - the hunter can stow the pet and call it back at will. Severs the owner link on both sides
        /// first so OnPetDeath finds no owner (OwnerOf returns null) and skips the lockout entirely.</summary>
        private static void DismissPet(Entity owner, EntityAgent pet)
        {
            pet.WatchedAttributes.RemoveAttribute(EBHunterPet.OwnerUidKey);
            owner.WatchedAttributes.RemoveAttribute(OwnerPetIdKey);
            owner.WatchedAttributes.MarkPathDirty(OwnerPetIdKey);
            pet.Die(EnumDespawnReason.Removed);
        }

        private static void SetPetStay(Entity pet, bool stay)
        {
            pet.WatchedAttributes.SetBool(PetStayKey, stay);
            pet.WatchedAttributes.MarkPathDirty(PetStayKey);
        }

        /// <summary>Stores the owner's chosen pet name (persists across relog, unlike the pet) and stamps it onto the
        /// wolf that's currently out, if any. Called from the pet-name chat command and on each summon.</summary>
        public static void SetPetName(EntityAgent owner, string name)
        {
            name = name?.Trim() ?? "";
            if (name.Length == 0) owner.WatchedAttributes.RemoveAttribute(PetNameKey);
            else owner.WatchedAttributes.SetString(PetNameKey, name);
            owner.WatchedAttributes.MarkPathDirty(PetNameKey);
            var pet = GetPet(owner);
            if (pet != null) ApplyPetName(pet, name);
        }

        public static PetMode PetModeOf(Entity pet) => (PetMode)pet.WatchedAttributes.GetInt(PetModeKey, (int)PetMode.Aggressive);

        private static void SetPetMode(Entity pet, PetMode mode)
        {
            pet.WatchedAttributes.SetInt(PetModeKey, (int)mode);
            pet.WatchedAttributes.MarkPathDirty(PetModeKey);
        }

        /// <summary>Whether the pet's gated AI tasks (seek/melee) may run right now, per its command mode:
        /// Aggressive = always (free hunting); Passive = never; Defensive = only inside the order/assist window.</summary>
        public static bool TaskModeAllows(Entity pet)
        {
            switch (PetModeOf(pet))
            {
                case PetMode.Aggressive: return true;
                case PetMode.Passive: return false;
                default: return pet.World.ElapsedMilliseconds <= pet.WatchedAttributes.GetLong(PetAssignUntilKey, 0);
            }
        }

        // ---- Damage hook: scale pet damage + make the pet assist the owner (and defend them) ----
        private static void PetDamageAndAssistHook(Entity victim, Entity? attacker, DamageSource source, ref float damage)
        {
            if (damage <= 0f || victim?.World?.Side != EnumAppSide.Server) return;

            // The pet's own hit: scale by the owner's Beast stats + any active Beast Fury window.
            if (attacker != null && IsPet(attacker))
            {
                float mul = 1f;
                var pOwner = OwnerOf(attacker);
                if (pOwner != null)
                {
                    float s = pOwner.Stats.GetBlended(PetDamageStat);
                    if (s > 0.01f) mul *= s;
                }
                if (attacker.World.ElapsedMilliseconds <= attacker.WatchedAttributes.GetLong(PetWrathUntilKey, 0))
                    mul *= attacker.WatchedAttributes.GetFloat(PetWrathBonusKey, 1f);
                damage *= mul;
                if (pOwner != null) ApplyBeastOnHit(pOwner, attacker, victim, damage);
                // A pet actively landing hits keeps its own pursuit window open, so a Defensive pet finishes the
                // fight it started instead of timing out after one bite when the owner isn't also attacking.
                attacker.WatchedAttributes.SetLong(PetAssignUntilKey, attacker.World.ElapsedMilliseconds + AssignWindowMs);
                return; // a pet's strike isn't an "owner attacked" event
            }

            // Owner struck a foe → focus-fire bonus if the pet was already on this target (Pack Tactics), then the
            // pet piles on. Pack Tactics is checked before the assist so the first hit on a fresh target (pet not
            // yet retargeted) doesn't self-trigger - it rewards sticking to what the wolf is already biting.
            if (attacker is EntityPlayer && HasPet(attacker))
            {
                int pack = TalentState.Rank(attacker, "hunter:pack_tactics");
                if (pack > 0 && PetIsTargeting(attacker, victim))
                    damage *= 1f + BalanceConfig.Talent("hunter:pack_tactics").F("perRank", 0.04f) * pack;
                AssignPetTarget((EntityAgent)attacker, victim);
            }

            // Owner was struck → the pet defends against the attacker.
            if (victim is EntityPlayer && attacker != null && HasPet(victim))
                AssignPetTarget((EntityAgent)victim, attacker);
        }

        /// <summary>Beast Mastery on-hit effects the wolf carries, gated by the owner's talent ranks: Venom (bite
        /// poisons), Cowing Roar (bite slows), Bloodletting (the pet heals for a fraction of the damage it dealt).</summary>
        private static void ApplyBeastOnHit(EntityAgent owner, Entity pet, Entity victim, float dealt)
        {
            int venom = TalentState.Rank(owner, "hunter:venom");
            if (venom > 0)
                SpellExecutor.ApplyEffect(victim, "poison", venom, BalanceConfig.Talent("hunter:venom").F("duration", 6f));

            int intim = TalentState.Rank(owner, "hunter:cowing_roar");
            if (intim > 0)
                SpellExecutor.ApplyEffect(victim, "walkslow", intim, BalanceConfig.Talent("hunter:cowing_roar").F("duration", 3f));

            int blood = TalentState.Rank(owner, "hunter:bloodletting");
            if (blood > 0 && dealt > 0f)
            {
                float heal = dealt * BalanceConfig.Talent("hunter:bloodletting").F("perRank", 0.06f) * blood;
                if (heal > 0f)
                    pet.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, heal);
            }
        }

        /// <summary>Whether the owner's pet currently has this foe as its seek/melee target (used by Pack Tactics).</summary>
        private static bool PetIsTargeting(Entity owner, Entity foe)
        {
            var mgr = GetPet(owner)?.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (mgr == null) return false;
            return mgr.GetTask<AiTaskMeleeAttack>()?.targetEntity == foe
                || mgr.GetTask<AiTaskSeekEntity>()?.targetEntity == foe;
        }

        /// <summary>Points the owner's pet at a foe: sets its seek/melee target and pings the revenge hook so it
        /// sticks through the task's own re-evaluation (same technique as SpellExecutor.ApplyAggro).</summary>
        public static void AssignPetTarget(EntityAgent owner, Entity foe)
        {
            if (foe == null || foe == owner || IsPet(foe)) return;
            if (SpellExecutor.IsAlly(owner, foe)) return;
            var pet = GetPet(owner);
            if (pet == null || pet == foe) return;
            if (PetModeOf(pet) == PetMode.Passive) return; // a passive pet ignores assist/defend orders

            // Open the pursuit window so a Defensive pet actually engages this target (see TaskModeAllows), and
            // record the foe so the AI tasks hold it as their target through their own re-evaluation.
            pet.WatchedAttributes.SetLong(PetAssignUntilKey, pet.World.ElapsedMilliseconds + AssignWindowMs);
            pet.WatchedAttributes.SetLong(PetTargetIdKey, foe.EntityId);

            var taskAI = pet.GetBehavior<EntityBehaviorTaskAI>();
            var mgr = taskAI?.TaskManager;
            if (taskAI == null || mgr == null) return;

            var seek = mgr.GetTask<AiTaskSeekEntity>();
            if (seek != null) seek.targetEntity = foe;
            var melee = mgr.GetTask<AiTaskMeleeAttack>();
            if (melee != null) melee.targetEntity = foe;

            var ping = new DamageSource { Source = EnumDamageSource.Entity, SourceEntity = foe, CauseEntity = foe, Type = EnumDamageType.BluntAttack };
            float zero = 0f;
            taskAI.OnEntityReceiveDamage(ping, ref zero);
        }

        // ---- Death → summon lockout (called from the mod's OnEntityDeath) ----
        public static void OnPetDeath(Entity pet)
        {
            var owner = OwnerOf(pet);
            if (owner == null) return;
            owner.WatchedAttributes.RemoveAttribute(OwnerPetIdKey);
            float lockout = BalanceConfig.Global("petDeathLockoutSeconds", 60f);
            // Beast Call (summon_pet cooldown reduction) also shortens the death lockout - the only meaningful
            // "summon cooldown" the pet has (its base cast CD is trivial).
            float cdr = System.Math.Clamp(owner.ReductionStat(StatKeys.CooldownReductionFor("summon_pet")), 0f, 0.8f);
            lockout *= 1f - cdr;
            owner.GetBehavior<EBSpellCooldowns>()?.SetCooldown("canrpgclasses:summon_pet", lockout);
        }

        /// <summary>The foe the pet was last ordered/assisted onto (AssignPetTarget), or null if it's gone/dead.
        /// The gated seek/melee tasks hold this so a commanded pet keeps its target through the vanilla re-scan.</summary>
        public static Entity? ForcedTargetOf(Entity pet)
        {
            long id = pet.WatchedAttributes.GetLong(PetTargetIdKey, 0);
            if (id == 0) return null;
            var e = pet.World.GetEntityById(id);
            return e != null && e.Alive ? e : null;
        }

        /// <summary>Fully disengages the pet: forgets its ordered target, closes the pursuit window, and clears the
        /// seek/melee tasks' targets so it stops chasing immediately (used by the "come" recall - otherwise the pet
        /// would just path straight back to the target we now persist).</summary>
        public static void ClearPetTarget(Entity pet)
        {
            pet.WatchedAttributes.RemoveAttribute(PetTargetIdKey);
            pet.WatchedAttributes.SetLong(PetAssignUntilKey, 0);
            var mgr = pet.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (mgr == null) return;
            var seek = mgr.GetTask<AiTaskSeekEntity>();
            if (seek != null) seek.targetEntity = null;
            var melee = mgr.GetTask<AiTaskMeleeAttack>();
            if (melee != null) melee.targetEntity = null;
        }

        public static bool IsPet(Entity e) => e?.Code?.Path == PetEntityCode;

        private static bool HasPet(Entity owner) => GetPet(owner) != null;

        public static EntityAgent? GetPet(Entity owner)
        {
            long id = owner.WatchedAttributes.GetLong(OwnerPetIdKey, 0);
            if (id == 0) return null;
            return owner.World.GetEntityById(id) is EntityAgent pet && pet.Alive ? pet : null;
        }

        private static EntityAgent? OwnerOf(Entity pet)
        {
            string uid = pet.WatchedAttributes.GetString(EBHunterPet.OwnerUidKey, null);
            return uid == null ? null : pet.World.PlayerByUid(uid)?.Entity;
        }
    }
}
