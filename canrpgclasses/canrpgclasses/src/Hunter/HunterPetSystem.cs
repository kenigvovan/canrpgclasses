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
    /// <summary>Server-side hunter wolf: summon, assist/defend hook, owner-scaled stats, pet-target impacts.</summary>
    public static class HunterPetSystem
    {
        public const string PetEntityCode = "hunterpet";

        // Owner WA holds the pet's entity id (resummon despawns the old one); pet WA holds the Beast Fury window.
        public const string OwnerPetIdKey = "canrpgPetEntityId";
        public const string PetWrathUntilKey = "canrpgPetWrathUntil";
        public const string PetWrathBonusKey = "canrpgPetWrathBonus";

        public const string PetModeKey = "canrpgPetMode";
        public const string PetAssignUntilKey = "canrpgPetAssignUntil";
        // Held by the AI tasks so a commanded pet doesn't drop its target on the vanilla nearest-target re-scan.
        public const string PetTargetIdKey = "canrpgPetTargetId";
        // How long after an order/assist a Defensive pet may pursue.
        private const long AssignWindowMs = 6000;

        // 1.0-based owner stats from Beast Mastery talents.
        public const string PetHealthStat = "hunterPetHealth";
        public const string PetDamageStat = "hunterPetDamage";

        // The vanilla stayclosetoguardedentity task bails while this WA is set, which pins the pet in place.
        public const string PetStayKey = "commandSit";
        // Stored on the owner, since the pet itself doesn't persist across relog.
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
            // Persistent so it survives talent-registry republishes.
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

            pet.WatchedAttributes.SetString(EBHunterPet.OwnerUidKey, op.PlayerUID);

            // Name goes in before spawning: a post-spawn write races the client building an empty name texture.
            string petName = owner.WatchedAttributes.GetString(PetNameKey, null);
            if (!string.IsNullOrEmpty(petName)) ApplyPetName(pet, petName);

            // Spawn a step behind the owner.
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

        // Direct nametag tree write instead of the behavior's SetName, so it also works pre-spawn.
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

            // Beast Fury: a timed damage window read by PetDamageAndAssistHook.
            if (imp.DamageSpellPowerCoefficient > 0f && imp.StatusEffectDuration > 0f)
            {
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
                    // An explicit order takes a passive pet off passive so it actually engages.
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
                    ClearPetTarget(pet); // otherwise it runs straight back to the persisted target
                    SetPetStay(pet, false); // otherwise a held pet teleports in and just sits
                    imp.Particles?.Emit(world, pet);
                    break;
                case "stay":
                    SetPetStay(pet, !pet.WatchedAttributes.GetBool(PetStayKey, false));
                    imp.Particles?.Emit(owner.World, pet);
                    break;
                case "dismiss":
                    imp.Particles?.Emit(owner.World, pet);
                    DismissPet(owner, pet);
                    break;
            }
        }

        /// <summary>No summon lockout, unlike a death: the owner link is severed first so OnPetDeath finds no owner.</summary>
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

        /// <summary>Stores the pet name on the owner and stamps it onto the wolf that's currently out.</summary>
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

        /// <summary>Whether the gated seek/melee tasks may run: Aggressive always, Passive never, Defensive only
        /// inside the order/assist window.</summary>
        public static bool TaskModeAllows(Entity pet)
        {
            switch (PetModeOf(pet))
            {
                case PetMode.Aggressive: return true;
                case PetMode.Passive: return false;
                default: return pet.World.ElapsedMilliseconds <= pet.WatchedAttributes.GetLong(PetAssignUntilKey, 0);
            }
        }

        // ---- Damage hook: pet damage scaling + assist/defend ----
        private static void PetDamageAndAssistHook(Entity victim, Entity? attacker, DamageSource source, ref float damage)
        {
            if (damage <= 0f || victim?.World?.Side != EnumAppSide.Server) return;

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
                // Landing hits keeps the pursuit window open, so a Defensive pet finishes the fight on its own.
                attacker.WatchedAttributes.SetLong(PetAssignUntilKey, attacker.World.ElapsedMilliseconds + AssignWindowMs);
                return; // a pet's strike isn't an "owner attacked" event
            }

            // Pack Tactics is checked before the assist retargets the pet, so it only rewards hitting what the
            // wolf is already biting.
            if (attacker is EntityPlayer && HasPet(attacker))
            {
                int pack = TalentState.Rank(attacker, "hunter:pack_tactics");
                if (pack > 0 && PetIsTargeting(attacker, victim))
                    damage *= 1f + BalanceConfig.Talent("hunter:pack_tactics").F("perRank", 0.04f) * pack;
                AssignPetTarget((EntityAgent)attacker, victim);
            }

            // Owner was struck: the pet defends.
            if (victim is EntityPlayer && attacker != null && HasPet(victim))
                AssignPetTarget((EntityAgent)victim, attacker);
        }

        /// <summary>Beast Mastery on-hit talents carried by the wolf: Venom, Cowing Roar, Bloodletting.</summary>
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

        private static bool PetIsTargeting(Entity owner, Entity foe)
        {
            var mgr = GetPet(owner)?.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (mgr == null) return false;
            return mgr.GetTask<AiTaskMeleeAttack>()?.targetEntity == foe
                || mgr.GetTask<AiTaskSeekEntity>()?.targetEntity == foe;
        }

        /// <summary>Sets the pet's seek/melee target and pings the revenge hook so it sticks through the task's
        /// re-evaluation (same trick as SpellExecutor.ApplyAggro).</summary>
        public static void AssignPetTarget(EntityAgent owner, Entity foe)
        {
            if (foe == null || foe == owner || IsPet(foe)) return;
            if (SpellExecutor.IsAlly(owner, foe)) return;
            var pet = GetPet(owner);
            if (pet == null || pet == foe) return;
            if (PetModeOf(pet) == PetMode.Passive) return;

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
            // Beast Call's summon_pet CDR applies here, since the lockout is the only cooldown that matters.
            float cdr = System.Math.Clamp(owner.ReductionStat(StatKeys.CooldownReductionFor("summon_pet")), 0f, 0.8f);
            lockout *= 1f - cdr;
            owner.GetBehavior<EBSpellCooldowns>()?.SetCooldown("canrpgclasses:summon_pet", lockout);
        }

        /// <summary>The foe last set by AssignPetTarget, or null if it's gone or dead.</summary>
        public static Entity? ForcedTargetOf(Entity pet)
        {
            long id = pet.WatchedAttributes.GetLong(PetTargetIdKey, 0);
            if (id == 0) return null;
            var e = pet.World.GetEntityById(id);
            return e != null && e.Alive ? e : null;
        }

        /// <summary>Forgets the ordered target and clears the task targets so the pet stops chasing immediately.</summary>
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
