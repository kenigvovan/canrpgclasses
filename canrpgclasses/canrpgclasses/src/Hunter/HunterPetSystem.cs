using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Progression;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;

using EBEffects = effectshud.src.EBEffectsAffected;

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
        // Kill Command: flat damage the next bite adds, and until when it holds.
        public const string PetBiteBonusKey = "canrpgPetBiteBonus";
        public const string PetBiteUntilKey = "canrpgPetBiteUntil";
        // Species specials on the pet: a rider for the next bite (rend/cripple), or a timed guard/evade window.
        private const string PetBiteKindKey = "canrpgPetBiteKind";
        private const string PetBiteKindUntilKey = "canrpgPetBiteKindUntil";
        private const string PetGuardUntilKey = "canrpgPetGuardUntil";
        private const string PetEvadeUntilKey = "canrpgPetEvadeUntil";

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
        // Owner WA: until when the death lockout runs; Revive Pet is castable only inside it.
        public const string PetDeadUntilKey = "canrpgPetDeadUntil";
        // Owner WA across a relog: the wolf was out at logout, and with how much health.
        private const string PetWasOutKey = "canrpgPetWasOut";
        private const string PetHpFracKey = "canrpgPetHpFrac";
        private const string SummonSpellId = "canrpgclasses:summon_pet";

        private static bool registered;

        /// <summary>Wires the pet content into the core (server side). Idempotent.</summary>
        public static void Init()
        {
            if (registered) return;
            registered = true;
            SpellExecutor.RegisterImpact(ImpactAction.Spawn, ApplySummonPet);
            SpellExecutor.RegisterPrecheck(ImpactAction.Spawn, PrecheckSummon);
            SpellExecutor.RegisterImpact(ImpactAction.PetTarget, ApplyPetTarget);
            SpellExecutor.RegisterImpact(ImpactAction.PetCommand, ApplyPetCommand);
            PetTaming.Init();
            // Persistent so it survives talent-registry republishes.
            canrpgclasses.Core.DamageModifiers.RegisterPersistent(PetDamageAndAssistHook);
        }

        // ---- Summon (ImpactAction.Spawn) ----
        private static void ApplySummonPet(SpellContext ctx, Entity target, SpellImpact imp)
        {
            if (imp.PetCommand != "revive") { SummonPet(ctx.Caster); return; }
            if (PrecheckSummon(ctx.Caster, ctx.Spell, imp, null!) != null) return;
            SummonPet(ctx.Caster, BalanceConfig.Spell("revive_pet").F("healthFraction", 0.5f));
            ctx.Caster.GetBehavior<EBSpellCooldowns>()?.ClearCooldown(SummonSpellId);
        }

        /// <summary>Revive only works on a dead wolf, inside its death lockout; a plain summon is never refused here.</summary>
        private static string? PrecheckSummon(EntityAgent caster, Spell spell, SpellImpact imp, AimContext aim)
        {
            if (imp.PetCommand != "revive") return null;
            if (GetPet(caster) != null) return Lang.Get("canrpgclasses:msg-pet-revive-alive");
            if (caster.World.ElapsedMilliseconds >= caster.WatchedAttributes.GetLong(PetDeadUntilKey, 0))
                return Lang.Get("canrpgclasses:msg-pet-revive-nodead");
            return null;
        }

        public static void SummonPet(EntityAgent owner, float healthFraction = 1f, EntityPos? at = null)
        {
            var world = owner.World;
            if (owner is not EntityPlayer op || op.PlayerUID == null) return;

            long oldId = owner.WatchedAttributes.GetLong(OwnerPetIdKey, 0);
            if (oldId != 0) world.GetEntityById(oldId)?.Die(EnumDespawnReason.Removed);

            var type = PetSpecies.TypeFor(world, PetStable.ActiveSpecies(owner)) ?? PetSpecies.TypeFor(world, PetSpecies.Default);
            if (type == null || world.ClassRegistry.CreateEntity(type) is not EntityAgent pet) return;

            pet.WatchedAttributes.SetString(EBHunterPet.OwnerUidKey, op.PlayerUID);

            // Name goes in before spawning: a post-spawn write races the client building an empty name texture.
            // Colour too: without it the entity picks a random alternate on spawn.
            pet.WatchedAttributes.SetInt("textureIndex", PetStable.ActiveColor(owner));
            if (PetStable.ActiveCosmetics(owner) is { } worn) pet.WatchedAttributes[EBPetCosmetics.Key] = worn.Clone();
            string petName = PetStable.ActiveName(owner);
            if (!string.IsNullOrEmpty(petName)) ApplyPetName(pet, petName);

            // Spawn a step behind the owner, or in place of the creature just tamed.
            if (at != null) { pet.Pos.SetPos(at.XYZ); pet.Pos.Yaw = at.Yaw; }
            else pet.Pos.SetPos(owner.Pos.XYZ.AheadCopy(1.0, 0, owner.Pos.Yaw + GameMath.PI));
            pet.World = world;
            world.SpawnEntity(pet);

            pet.WatchedAttributes.MarkPathDirty(EBHunterPet.OwnerUidKey);
            owner.WatchedAttributes.SetLong(OwnerPetIdKey, pet.EntityId);
            owner.WatchedAttributes.MarkPathDirty(OwnerPetIdKey);
            owner.WatchedAttributes.RemoveAttribute(PetDeadUntilKey);

            var bpet = pet.GetBehavior<EBHunterPet>();
            bpet?.SetMaxHealth(PetMaxHealth(owner, pet), keepFraction: false);
            if (healthFraction < 1f) bpet?.SetHealthFraction(healthFraction);
        }

        /// <summary>The wolf's health fraction and name for HUDs, or (-1, "") when the owner has none out. Works on
        /// either side, as long as the wolf is loaded there.</summary>
        public static (float Fraction, string Name) PetStatus(Entity owner)
        {
            long id = owner.WatchedAttributes.GetLong(OwnerPetIdKey, 0);
            if (id == 0 || owner.World.GetEntityById(id) is not { Alive: true } pet) return (-1f, "");
            var health = pet.WatchedAttributes.GetTreeAttribute("health");
            float max = health?.GetFloat("maxhealth", 0f) ?? 0f;
            float frac = max > 0f ? (health!.GetFloat("currenthealth", 0f) / max) : 0f;
            string name = pet.WatchedAttributes.GetTreeAttribute("nametag")?.GetString("name", "") ?? "";
            return (frac, name);
        }

        private static int OwnerLevel(Entity owner) => owner.GetBehavior<EBProgression>()?.Level ?? 1;

        /// <summary>Wolf max health from the owner's level and Beast Mastery talents; re-read live by EBHunterPet.</summary>
        public static float PetMaxHealth(Entity owner, Entity pet)
        {
            float baseHp = BalanceConfig.Global("hunterPetBaseHp", 20f);
            float perLevel = BalanceConfig.Global("hunterPetHpPerLevel", 1f);
            float healthMul = System.Math.Max(1f, owner.Stats.GetBlended(PetHealthStat));
            return (baseHp + perLevel * System.Math.Max(0, OwnerLevel(owner) - 1)) * healthMul * PetSpecies.HpMul(pet);
        }

        /// <summary>Bite damage multiplier from the owner's level, so the wolf keeps pace with spell power.</summary>
        public static float PetLevelDamageMultiplier(Entity owner)
            => 1f + BalanceConfig.Global("hunterPetDamagePerLevel", 0.05f) * System.Math.Max(0, OwnerLevel(owner) - 1);

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
                case "growl":
                    Growl(owner, pet, imp.AggroRange);
                    imp.Particles?.Emit(owner.World, pet);
                    break;
                case "kill":
                    if (target == null || target == owner || IsPet(target) || SpellExecutor.IsAlly(owner, target)) break;
                    if (PetModeOf(pet) == PetMode.Passive) SetPetMode(pet, PetMode.Defensive);
                    SetPetStay(pet, false);
                    AssignPetTarget(owner, target);
                    ArmBite(ctx, pet, imp);
                    imp.Particles?.Emit(owner.World, pet);
                    break;
                case "special":
                    UseSpecial(owner, pet);
                    imp.Particles?.Emit(owner.World, pet);
                    break;
            }
        }

        private static void ArmBite(SpellContext ctx, EntityAgent pet, SpellImpact imp)
        {
            float window = imp.EmpowerWindowSeconds > 0 ? imp.EmpowerWindowSeconds : 6f;
            pet.WatchedAttributes.SetFloat(PetBiteBonusKey, ctx.SpellPower * imp.DamageSpellPowerCoefficient);
            pet.WatchedAttributes.SetLong(PetBiteUntilKey, pet.World.ElapsedMilliseconds + (long)(window * 1000f));
            // Cosmetic HUD countdown on the owner, cleared when the bite lands.
            SpellExecutor.ApplyEffect(ctx.Caster, EmpowerEffectIds.For(ctx.Spell.LocalId), 1, window);
        }

        /// <summary>The armed Kill Command damage, consumed by this bite; 0 if none or expired.</summary>
        private static float TakeBiteBonus(Entity pet, Entity? owner)
        {
            var wa = pet.WatchedAttributes;
            long until = wa.GetLong(PetBiteUntilKey, 0);
            if (until == 0) return 0f;
            float bonus = pet.World.ElapsedMilliseconds <= until ? wa.GetFloat(PetBiteBonusKey, 0f) : 0f;
            wa.SetLong(PetBiteUntilKey, 0);
            wa.SetFloat(PetBiteBonusKey, 0f);
            if (owner?.GetBehavior<EBEffects>() is { } eff && eff.TryGetEffect(EmpowerEffectIds.For("kill_command"), out var buff))
                buff.SetExpiryImmediately();
            return bonus;
        }

        // ---- Species specials (pet_special) ----
        private static void UseSpecial(EntityAgent owner, EntityAgent pet)
        {
            var b = BalanceConfig.Spell("pet_special");
            long now = pet.World.ElapsedMilliseconds;
            var wa = pet.WatchedAttributes;
            switch (PetSpecies.Special(pet))
            {
                case "rend":
                case "cripple":
                    wa.SetString(PetBiteKindKey, PetSpecies.Special(pet));
                    wa.SetLong(PetBiteKindUntilKey, now + (long)(b.F("biteWindow", 8f) * 1000f));
                    break;
                case "guard":
                    Growl(owner, pet, b.F("guardRadius", 8f));
                    wa.SetLong(PetGuardUntilKey, now + (long)(b.F("guardDuration", 6f) * 1000f));
                    break;
                case "evade":
                    wa.SetLong(PetEvadeUntilKey, now + (long)(b.F("evadeDuration", 5f) * 1000f));
                    break;
            }
        }

        /// <summary>Applies and clears an armed rend/cripple rider when the bite lands.</summary>
        private static void ApplyBiteRider(Entity pet, Entity victim)
        {
            var wa = pet.WatchedAttributes;
            string kind = wa.GetString(PetBiteKindKey, "");
            if (kind.Length == 0) return;
            bool live = pet.World.ElapsedMilliseconds <= wa.GetLong(PetBiteKindUntilKey, 0);
            wa.SetString(PetBiteKindKey, "");
            if (!live) return;

            var b = BalanceConfig.Spell("pet_special");
            if (kind == "rend")
                SpellExecutor.ApplyEffect(victim, "jagged_blades", b.I("rendAmp", 2), b.F("rendDuration", 8f));
            else if (kind == "cripple")
            {
                SpellExecutor.ApplyEffect(victim, WoundedEffectId.Id, 1, b.F("crippleDuration", 6f));
                SpellExecutor.ApplyEffect(victim, "walkslow", b.I("crippleSlowAmp", 2), b.F("crippleDuration", 6f));
            }
        }

        /// <summary>Guard cuts damage taken; evade has a chance to dodge it entirely.</summary>
        private static void DefendPet(Entity pet, ref float damage)
        {
            long now = pet.World.ElapsedMilliseconds;
            var wa = pet.WatchedAttributes;
            var b = BalanceConfig.Spell("pet_special");
            if (now <= wa.GetLong(PetGuardUntilKey, 0))
                damage *= 1f - System.Math.Clamp(b.F("guardDamageCut", 0.4f), 0f, 0.9f);
            if (now <= wa.GetLong(PetEvadeUntilKey, 0) && pet.World.Rand.NextDouble() < b.F("evadeChance", 0.5f))
                damage = 0f;
        }

        /// <summary>Turns every hostile creature around the wolf onto it, like SpellExecutor.ApplyAggro centred on the
        /// pet. Allies and players are left alone.</summary>
        private static void Growl(EntityAgent owner, EntityAgent pet, float range)
        {
            if (range <= 0f) range = 8f;
            var fakeHurt = new DamageSource
            {
                Source = EnumDamageSource.Entity, SourceEntity = pet, CauseEntity = pet, Type = EnumDamageType.Injury
            };

            foreach (var e in pet.World.GetEntitiesAround(pet.Pos.XYZ, range, range,
                         e => e != pet && e.Alive && e is EntityAgent && e is not EntityPlayer))
            {
                if (SpellExecutor.IsAlly(owner, e)) continue;
                var taskAI = e.GetBehavior<EntityBehaviorTaskAI>();
                var mgr = taskAI?.TaskManager;
                if (taskAI == null || mgr == null) continue;

                var seek = mgr.GetTask<AiTaskSeekEntity>();
                if (seek != null) seek.targetEntity = pet;
                var melee = mgr.GetTask<AiTaskMeleeAttack>();
                if (melee != null) melee.targetEntity = pet;

                float zero = 0f;
                taskAI.OnEntityReceiveDamage(fakeHurt, ref zero);
                e.GetBehavior<EntityBehaviorEmotionStates>()?.TryTriggerState("aggressiveondamage", pet.EntityId);
            }
        }

        /// <summary>Kindred Spirit: the wolf takes <paramref name="share"/> of a hit on its owner, if it is close by.</summary>
        public static void ShareDamageWithPet(Entity owner, ref float damage, float share)
        {
            var pet = GetPet(owner);
            if (pet == null || share <= 0f || damage <= 0f) return;
            float range = BalanceConfig.Talent("hunter:kindred_spirit").F("range", 12f);
            if (pet.Pos.SquareDistanceTo(owner.Pos) > range * range) return;

            float moved = damage * System.Math.Min(share, 0.9f);
            damage -= moved;
            // Internal, with no attacker: it must not count as the owner being struck, or the pet re-assigns itself.
            pet.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Injury }, moved);
        }

        // ---- Relog: the wolf itself doesn't persist, so it is remembered on the owner and re-summoned ----
        public static void OnOwnerLeave(EntityAgent? owner)
        {
            if (owner == null) return;
            var pet = GetPet(owner);
            owner.WatchedAttributes.SetBool(PetWasOutKey, pet != null);
            if (pet == null) return;
            var (frac, _) = PetStatus(owner);
            owner.WatchedAttributes.SetFloat(PetHpFracKey, frac > 0f ? frac : 1f);
            DismissPet(owner, pet);
        }

        public static void ScheduleRestoreOnJoin(Vintagestory.API.Server.ICoreServerAPI sapi, Vintagestory.API.Server.IServerPlayer player)
        {
            long delayMs = (long)BalanceConfig.Global("petRestoreOnJoinDelayMs", 3000f);
            sapi.Event.RegisterCallback(_ =>
            {
                if (player?.Entity is not EntityPlayer owner || !owner.Alive) return;
                var wa = owner.WatchedAttributes;
                // Elapsed time restarts with the server, so a lockout written before a restart would never end.
                long lockoutMs = (long)(BalanceConfig.Global("petDeathLockoutSeconds", 60f) * 1000f);
                if (wa.GetLong(PetDeadUntilKey, 0) > owner.World.ElapsedMilliseconds + lockoutMs) wa.RemoveAttribute(PetDeadUntilKey);

                bool wasOut = wa.GetBool(PetWasOutKey, false);
                wa.RemoveAttribute(PetWasOutKey);
                if (!wasOut || GetPet(owner) != null || TalentState.CurrentClass(owner) != "hunter") return;
                SummonPet(owner, wa.GetFloat(PetHpFracKey, 1f));
                wa.RemoveAttribute(PetHpFracKey);
            }, (int)delayMs);
        }

        // ---- Stable (PetStablePacket) ----
        public static void StableAction(EntityAgent owner, string action, int slot)
        {
            if (TalentState.CurrentClass(owner) != "hunter") return;
            if (action.StartsWith("trick:")) { Trick(owner, action.Substring("trick:".Length)); return; }
            if (slot < 0 || slot >= PetStable.Count(owner)) return;

            if (action == "select")
            {
                if (slot == PetStable.ActiveIndex(owner)) return;
                if (canrpgclasses.Core.HarmonyPatches.StunPatches.IsInCombat(owner)) { Tell(owner, "pet-stable-combat"); return; }
                // Swapping in another pet must not skip the death lockout.
                if (owner.World.ElapsedMilliseconds < owner.WatchedAttributes.GetLong(PetDeadUntilKey, 0)) { Tell(owner, "pet-stable-dead"); return; }

                bool wasOut = GetPet(owner) != null;
                PetStable.SetActive(owner, slot);
                if (wasOut) { DismissActive(owner); SummonPet(owner); }
            }
            else if (action == "color")
            {
                bool isOut = slot == PetStable.ActiveIndex(owner) && GetPet(owner) != null;
                if (isOut && canrpgclasses.Core.HarmonyPatches.StunPatches.IsInCombat(owner)) { Tell(owner, "pet-stable-combat"); return; }
                PetStable.CycleColor(owner, slot);
                // The texture is fixed at spawn, so the pet out is swapped for a recoloured one at the same health.
                if (isOut)
                {
                    float frac = PetStatus(owner).Fraction;
                    DismissActive(owner);
                    SummonPet(owner, frac > 0f ? frac : 1f);
                }
            }
            else if (action == "release")
            {
                if (PetStable.Count(owner) <= 1) { Tell(owner, "pet-stable-last"); return; }
                if (slot == PetStable.ActiveIndex(owner)) DismissActive(owner);
                ReturnCosmetics(owner, PetStable.Slot(owner, slot)?.GetTreeAttribute("cosmetics"));
                PetStable.Release(owner, slot);
            }
        }

        // ---- Tricks (visual only) ----

        /// <summary>Tricks of the pet's family whose clip its model has; the pet's look decides, e.g. a fox kit can't lie.</summary>
        public static List<string> Tricks(Entity pet)
        {
            var list = new List<string>();
            foreach (string t in PetSpecies.Family(pet)?["tricks"].AsArray<string>() ?? System.Array.Empty<string>())
                if (HasClip(pet, t)) list.Add(t);
            return list;
        }

        private static bool HasClip(Entity pet, string clip)
        {
            var anims = pet.Properties.Client?.LoadedShapeForEntity?.Animations;
            if (anims == null) return true; // shape not at hand on this side: trust the family list
            foreach (var a in anims) if (a.Code == clip) return true;
            return false;
        }

        public static bool CanTrick(Entity owner, Entity pet, string trick)
            => PetJournal.TrickUnlocked(owner, trick) && Tricks(pet).Contains(trick);

        /// <summary>Plays a trick until the pet moves, fights or its owner walks off (EBHunterPet).</summary>
        public static void Trick(EntityAgent owner, string trick)
        {
            if (GetPet(owner) is not { } pet || !CanTrick(owner, pet, trick)) return;
            if (canrpgclasses.Core.HarmonyPatches.StunPatches.IsInCombat(owner) || ForcedTargetOf(pet) != null)
            { Tell(owner, "pet-trick-busy"); return; }
            pet.GetBehavior<EBHunterPet>()?.StartTrick(trick);
        }

        /// <summary>A released pet's worn items go back to the owner (or drop at their feet), so nothing is lost.</summary>
        private static void ReturnCosmetics(EntityAgent owner, Vintagestory.API.Datastructures.ITreeAttribute? worn)
        {
            if (worn == null) return;
            foreach (var entry in worn)
            {
                string code = (entry.Value as Vintagestory.API.Datastructures.StringAttribute)?.value ?? "";
                var item = owner.World.GetItem(new AssetLocation(code));
                if (item == null) continue;
                var stack = new ItemStack(item);
                if ((owner as EntityPlayer)?.Player?.InventoryManager?.TryGiveItemstack(stack, true) != true)
                    owner.World.SpawnItemEntity(stack, owner.Pos.XYZ);
            }
        }

        private static void Tell(EntityAgent owner, string key)
            => canrpgclasses.Core.Portals.PortalSpells.Tell(owner, Lang.Get("canrpgclasses:msg-" + key));

        /// <summary>Sends the current pet away without a death lockout, if one is out.</summary>
        public static void DismissActive(EntityAgent owner)
        {
            var pet = GetPet(owner);
            if (pet != null) DismissPet(owner, pet);
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
            PetStable.SetActiveName(owner, name); // also mirrors PetNameKey
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

            // Heals arrive as damage of type Heal, and Kindred Spirit's share as Internal; neither may be dodged, or the
            // shared part would simply vanish.
            if (IsPet(victim) && source.Type != EnumDamageType.Heal && source.Source != EnumDamageSource.Internal)
                DefendPet(victim, ref damage);
            if (damage <= 0f) return;

            if (attacker != null && IsPet(attacker))
            {
                float mul = 1f;
                var pOwner = OwnerOf(attacker);
                if (pOwner != null)
                {
                    float s = pOwner.Stats.GetBlended(PetDamageStat);
                    if (s > 0.01f) mul *= s;
                    mul *= PetLevelDamageMultiplier(pOwner);
                }
                mul *= PetSpecies.DmgMul(attacker);
                if (attacker.World.ElapsedMilliseconds <= attacker.WatchedAttributes.GetLong(PetWrathUntilKey, 0))
                    mul *= attacker.WatchedAttributes.GetFloat(PetWrathBonusKey, 1f);
                damage *= mul;
                // Added after the multipliers: Kill Command already scaled with spell power when it was cast.
                damage += TakeBiteBonus(attacker, pOwner);
                ApplyBiteRider(attacker, victim);
                if (pOwner != null) ApplyBeastOnHit(pOwner, attacker, victim, damage);
                // Landing hits keeps the pursuit window open, so a Defensive pet finishes the fight on its own.
                attacker.WatchedAttributes.SetLong(PetAssignUntilKey, attacker.World.ElapsedMilliseconds + AssignWindowMs);
                return; // a pet's strike isn't an "owner attacked" event
            }

            // Pack Tactics is checked before the assist retargets the pet, so it only rewards hitting what the
            // wolf is already biting.
            if (attacker is EntityPlayer && GetPet(attacker) is { } ownPet)
            {
                int pack = TalentState.Rank(attacker, "hunter:pack_tactics");
                if (pack > 0 && PetIsTargeting(attacker, victim))
                    damage *= 1f + BalanceConfig.Talent("hunter:pack_tactics").F("perRank", 0.04f) * pack;
                // Defensive only answers attacks on the owner; Aggressive also joins the owner's own fights.
                if (PetModeOf(ownPet) == PetMode.Aggressive) AssignPetTarget((EntityAgent)attacker, victim);
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
            owner.GetBehavior<EBSpellCooldowns>()?.SetCooldown(SummonSpellId, lockout);
            owner.WatchedAttributes.SetLong(PetDeadUntilKey, owner.World.ElapsedMilliseconds + (long)(lockout * 1000f));
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

        public static bool IsPet(Entity e) => PetSpecies.Of(e) != null;

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
