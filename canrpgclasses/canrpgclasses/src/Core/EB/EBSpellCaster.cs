using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Control;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Net;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;

using EBEffects = effectshud.src.EBEffectsAffected;
using EffectsHud = effectshud.src.effectshud;
using EffectBase = effectshud.src.Effect;
using System.Collections.Generic;

namespace canrpgclasses.Core.EB
{
    /// <summary>Server-authoritative cast driver for instant, charged and channelled casts: validate, execute,
    /// broadcast, pay, cooldown.</summary>
    public class EBSpellCaster : EntityBehavior
    {
        public const string Name = "canrpgspellcaster";

        public EBSpellCaster(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        /// <summary>True while a Charge cast or a channel is in progress (server).</summary>
        public bool IsCasting => castSpellId != null;
        public bool IsChanneling => isChanneling;
        public string? CurrentCastSpellId => castSpellId;
        public long CastEndMs => castEndMs;

        /// <summary>Why the last TryCast returned false; None after a cast that went through.</summary>
        public CastFailReason LastFailReason { get; private set; }

        /// <summary>Cancelled-cast counter; AI compares it before/after a timed cast to detect interruption.</summary>
        public int InterruptCount { get; private set; }

        // Runtime-only knobs for AI casters: which events break a Charge cast, and an optional cooldown rewrite.
        public CastInterruptFlags InterruptFlags { get; set; } = CastInterruptFlags.Movement | CastInterruptFlags.Damage;
        public float MinInterruptDamage { get; set; }
        public System.Func<Spell, float, float>? CooldownOverride { get; set; }

        public void Interrupt(bool silent = true) => CancelCast(silent);

        private bool Fail(CastFailReason reason)
        {
            LastFailReason = reason;
            return false;
        }

        // In-flight Charge/Channel cast, server only. Null when not casting.
        private string? castSpellId;
        private AimContext? castAim;
        private long castEndMs;
        // Cost captured at commit, so a cost scaling with a stack the cast itself grants (Mystic Charges) isn't inflated.
        private float castResourceCost;
        // Channel fires at START and keeps its zone alive while it runs; a Charge cast fires on completion and breaks on damage.
        private bool isChanneling;
        // Periodic-fire channel (Mystic Barrage); cost was paid up front.
        private int channelTicksRemaining;
        private float channelTickInterval;
        private long nextTickMs;
        private Spell? channelSpell;

        // Seconds left in the stealth window. Transient field rather than WatchedAttributes, so it's relog-safe
        // without an absolute clock; attacking while > 0 applies the opener's full lockout.
        private float stealthWindow;

        // The spell that opened the stealth window, so NotifyStealthBrokenByAttack rewrites that spell's cooldown.
        private string? stealthSpellKey;
        private float stealthLockoutSeconds;

        // Transient on purpose: ElapsedMilliseconds resets between sessions, so a persisted deadline would go stale.
        private long empowerUntilMs;

        public void ArmEmpowerWindow(float seconds) => empowerUntilMs = entity.World.ElapsedMilliseconds + (long)(seconds * 1000f);

        // Instant-cast window: next matching cast ("*" = any) skips its cast time. Transient for the same reason.
        private string? instantCastSpellId;
        private long instantCastUntilMs;
        private string? instantCastMarkerId; // HUD effect cleared when the window is consumed

        /// <summary>Next cast of <paramref name="spellIdOrStar"/> within <paramref name="windowSeconds"/> ignores its
        /// cast time. A later grant overwrites an earlier one.</summary>
        public void GrantInstantCast(string spellIdOrStar, float windowSeconds, string? markerEffectId = null)
        {
            instantCastSpellId = spellIdOrStar;
            instantCastUntilMs = entity.World.ElapsedMilliseconds + (long)(windowSeconds * 1000f);
            instantCastMarkerId = markerEffectId;
        }

        private bool ConsumeInstantCastIfEligible(Spell spell)
        {
            if (instantCastSpellId == null || entity.World.ElapsedMilliseconds > instantCastUntilMs) return false;
            if (instantCastSpellId != "*" && instantCastSpellId != spell.Id) return false;
            instantCastSpellId = null;
            if (instantCastMarkerId != null) { SpellExecutor.RemoveEffect(entity, instantCastMarkerId); instantCastMarkerId = null; }
            return true;
        }

        public bool TryCast(string spellId, AimContext aim)
        {
            LastFailReason = CastFailReason.None;
            if (entity.Api.Side != EnumAppSide.Server) return Fail(CastFailReason.WrongSide);

            var mod = canrpgclassesModSystem.For(entity.Api);
            if (mod == null || !mod.Spells.TryGet(spellId, out var spell)) return Fail(CastFailReason.UnknownSpell);

            if (ControlState.IsActive(entity, ControlType.Stun))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-stunned"));
                return Fail(CastFailReason.Stunned);
            }
            if (ControlState.IsActive(entity, ControlType.Fear))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-feared"));
                return Fail(CastFailReason.Feared);
            }
            if (ControlState.IsActive(entity, ControlType.Silence))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-silenced"));
                return Fail(CastFailReason.Silenced);
            }
            if (ControlState.IsActive(entity, ControlType.Transmute))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-polymorphed"));
                return Fail(CastFailReason.Transmuted);
            }
            // Travel-form lock, except the spell that toggles the form off.
            if (!spell.BypassesFormLock && FormLockHeld())
            {
                SendMessage(Lang.Get("canrpgclasses:msg-form-locked"));
                return Fail(CastFailReason.FormLocked);
            }

            // In-form abilities set BypassesFormLock + RequiresForm.
            if (!string.IsNullOrEmpty(spell.RequiresForm)
                && entity.WatchedAttributes.GetString(AttrKeys.ActiveAura, "") != spell.RequiresForm)
            {
                SendMessage(Lang.Get("canrpgclasses:msg-wrong-form"));
                return Fail(CastFailReason.WrongForm);
            }

            if (spell.RequiresOutOfCombat && canrpgclasses.Core.HarmonyPatches.StunPatches.IsInCombat(entity))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-in-combat"));
                return Fail(CastFailReason.InCombat);
            }

            if (spell.RequiresBow && !HoldingBow(entity))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-need-bow"));
                return Fail(CastFailReason.NeedBow);
            }

            if (spell.RequiresShield && !HoldingShield(entity))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-need-shield"));
                return Fail(CastFailReason.NeedShield);
            }

            if (!Attributes.AttributeStats.Meets(entity, spell.RequiresAttributes, out string missingAttr))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-need-attribute", missingAttr));
                return Fail(CastFailReason.NeedAttribute);
            }

            // Impact-specific gate (e.g. portal placement).
            if (entity is EntityAgent checker && SpellExecutor.Precheck(checker, spell, aim) is string refusal)
            {
                SendMessage(refusal);
                return Fail(CastFailReason.Blocked);
            }

            var cooldowns = entity.GetBehavior<EBSpellCooldowns>();
            string cdKey = spell.CooldownKey;
            if (cooldowns != null && cooldowns.IsOnCooldown(cdKey))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-on-cooldown", spell.DisplayName, cooldowns.RemainingSeconds(cdKey).ToString("0.0")));
                return Fail(CastFailReason.OnCooldown);
            }

            // GCD fails silently: the hotbar sweeps every slot during it, so a message would spam chat.
            if (spell.TriggersGlobalCooldown && cooldowns != null && cooldowns.IsOnCooldown(Spell.GlobalCooldownKey))
                return Fail(CastFailReason.GlobalCooldown);

            // Fail before spending anything when nothing is aimed at. Ally smart-casts fall back to the caster.
            if (spell.Target.Type == TargetType.Aim && spell.Target.Affinity != TargetAffinity.Ally
                && entity is EntityAgent aimer
                && SpellExecutor.GetAimedEntity(aimer, aim, spell.Range > 0 ? spell.Range : 16f) == null)
            {
                SendMessage(Lang.Get("canrpgclasses:msg-no-target"));
                return Fail(CastFailReason.NoTarget);
            }

            // Target must already carry one of these effects. Resolved like SpellExecutor.ResolveAim so the gate
            // and the impact agree on the target.
            if (spell.RequiresTargetEffectIds != null && entity is EntityAgent effectAimer)
            {
                Entity? gateTarget = entity;
                if (spell.Target.Type == TargetType.Aim)
                {
                    var aimed = SpellExecutor.GetAimedEntity(effectAimer, aim, spell.Range > 0 ? spell.Range : 16f);
                    gateTarget = spell.Target.Affinity == TargetAffinity.Ally
                        ? (aimed != null && SpellExecutor.IsAlly(entity, aimed) ? aimed : entity)
                        : aimed;
                }
                if (!SpellExecutor.HasAnyEffect(gateTarget, spell.RequiresTargetEffectIds))
                {
                    SendMessage(Lang.Get("canrpgclasses:msg-need-target-effect", spell.DisplayName));
                    return Fail(CastFailReason.NeedTargetEffect);
                }
            }

            // Resource gate is silent; the HUD already shows it. The cost read here is carried through to PayCost.
            var pool = ResourceState.PrimaryPool(entity);
            float resourceCost = EffectiveResourceCost(spell);
            if (!ResourceState.Has(entity, pool, resourceCost)) return Fail(CastFailReason.NoResource);
            if (spell.ComboFinisher && ResourceState.Combo(entity) <= 0) return Fail(CastFailReason.NoCombo);
            if (!string.IsNullOrEmpty(spell.SecondaryResourceId)
                && !ResourceState.Has(entity, ResourceState.SecondaryPool(spell.SecondaryResourceId), spell.SecondaryResourceCost))
                return Fail(CastFailReason.NoResource);

            // GCD starts here for all paths, so it runs in parallel with a cast bar rather than after it.
            TriggerGlobalCooldown(spell);

            bool instantCast = spell.CastMode == CastMode.Charge && spell.CastDuration > 0f && ConsumeInstantCastIfEligible(spell);

            // Charge cast: cost/cooldown are paid on completion, so an interrupted cast wastes nothing.
            if (spell.CastMode == CastMode.Charge && spell.CastDuration > 0f && !instantCast && entity is EntityAgent)
            {
                if (castSpellId != null) CancelCast(silent: true);
                castSpellId = spell.Id;
                castAim = aim;
                castResourceCost = resourceCost;
                float castDur = EffectiveCastDuration(spell);
                castEndMs = entity.World.ElapsedMilliseconds + (long)(castDur * 1000f);
                BroadcastCast(spell, castDur);
                return true;
            }

            // Channel: fire now, then run for CastDuration; moving or CC cancels the channel and its zone.
            if (spell.CastMode == CastMode.Channel && spell.CastDuration > 0f && entity is EntityAgent)
            {
                if (castSpellId != null) CancelCast(silent: true);
                BroadcastCast(spell, spell.CastDuration);
                if (spell.ChannelTicks > 0)
                {
                    // Periodic channel: pay once, fire the first bolt now and the rest evenly so the last lands at the
                    // end. ConsumesCasterEffectId is stripped at channel end, after every bolt snapshotted spell power.
                    PayCost(spell, resourceCost);
                    ExecuteOnly(spell, aim);
                    channelSpell = spell;
                    channelTicksRemaining = spell.ChannelTicks - 1;
                    channelTickInterval = spell.ChannelTicks > 1 ? spell.CastDuration / (spell.ChannelTicks - 1) : 0f;
                    nextTickMs = entity.World.ElapsedMilliseconds + (long)(channelTickInterval * 1000f);
                    // Beam channel: publish the ray target for clients (cleared by ClearBeamFx). aim.TargetEntity
                    // is null on a pure crosshair aim.
                    if (spell.BeamFx && entity is EntityAgent beamCaster)
                    {
                        var beamTarget = aim.TargetEntity
                            ?? SpellExecutor.GetAimedEntity(beamCaster, aim, spell.Range > 0 ? spell.Range : 16f);
                        if (beamTarget != null)
                        {
                            entity.WatchedAttributes.SetLong(Visuals.EffectVisuals.BeamTargetAttr, beamTarget.EntityId);
                            entity.WatchedAttributes.SetInt(Visuals.EffectVisuals.BeamSchoolAttr, (int)spell.School);
                        }
                    }
                }
                else ExecuteAndPay(spell, aim, resourceCost); // zone channel: fire once, keep the zone alive
                castSpellId = spell.Id;
                castAim = aim;
                isChanneling = true;
                castEndMs = entity.World.ElapsedMilliseconds + (long)(spell.CastDuration * 1000f);
                return true;
            }

            if (castSpellId != null) CancelCast(silent: true); // an instant cast also breaks an in-progress channel
            // Duration 0 for a proc'd instant cast, or the client shows a phantom cast bar that never clears.
            BroadcastCast(spell, instantCast ? 0f : -1f);
            ExecuteAndPay(spell, aim, resourceCost);
            return true;
        }

        /// <summary><paramref name="resourceCost"/> is the cost captured when the cast was committed.</summary>
        private void ExecuteAndPay(Spell spell, AimContext aim, float resourceCost)
        {
            ExecuteOnly(spell, aim);
            PayCost(spell, resourceCost);
            ConsumeCastEffect(spell);
        }

        private void ExecuteOnly(Spell spell, AimContext aim)
        {
            if (entity is EntityAgent agent) SpellExecutor.Execute(agent, spell, aim);
        }

        /// <summary>Form lock from the synced <c>FormActionLock</c> flag or the persisted <c>ActiveAura</c>; the
        /// aura string survives relog, so deriving from it too prevents a desync.</summary>
        private bool FormLockHeld()
        {
            var wa = entity.WatchedAttributes;
            if (wa.GetBool(CombatFlags.FormActionLock)) return true;
            string aura = wa.GetString(AttrKeys.ActiveAura, "");
            if (string.IsNullOrEmpty(aura)) return false;
            var mod = canrpgclassesModSystem.For(entity.Api);
            return mod != null && mod.Spells.TryGet(aura, out var auraSpell) && auraSpell.AuraLocksActions;
        }

        /// <summary>GCD is deliberately not reduced by cooldownReduction; it's the rotation's floor.</summary>
        private void TriggerGlobalCooldown(Spell spell)
        {
            if (!spell.TriggersGlobalCooldown) return;
            float gcd = BalanceConfig.Global("globalCooldownSeconds", 1.0f);
            if (gcd <= 0f) return;
            entity.GetBehavior<EBSpellCooldowns>()?.SetCooldown(Spell.GlobalCooldownKey, gcd);
        }

        /// <summary>Pays cost, sets cooldown, opens any stealth window. Split from firing so a periodic channel pays once.</summary>
        private void PayCost(Spell spell, float resourceCost)
        {
            var pool = ResourceState.PrimaryPool(entity);
            // Exhaust, item and durability costs are declared on SpellCost but not charged yet.
            ResourceState.Spend(entity, pool, resourceCost);
            if (!string.IsNullOrEmpty(spell.SecondaryResourceId))
                ResourceState.Spend(entity, ResourceState.SecondaryPool(spell.SecondaryResourceId), spell.SecondaryResourceCost);

            float cdr = Math.Clamp(Reduction(StatKeys.CooldownReduction) + Reduction(StatKeys.CooldownReductionFor(spell.LocalId)),
                0f, BalanceConfig.Global("cooldownReductionCap", 0.8f));
            // The server-wide pace knob comes after CDR, so talents still shave the same fraction off it.
            float cd = spell.Cost.Cooldown.Duration * (1f - cdr) * BalanceConfig.Global("spellCooldownMultiplier", 1f);
            if (CooldownOverride != null) cd = Math.Max(0f, CooldownOverride(spell, cd));
            entity.GetBehavior<EBSpellCooldowns>()?.SetCooldown(spell.CooldownKey, cd);

            // The short cd frees while still hidden, allowing re-stealth; attacking in the window applies the full lockout.
            if (spell.OpensStealthWindowSeconds > 0f)
            {
                stealthWindow = spell.OpensStealthWindowSeconds;
                stealthSpellKey = spell.CooldownKey;
                stealthLockoutSeconds = spell.Cost.Cooldown.Duration;
            }
        }

        /// <summary>Attacking inside the stealth window sets the opener's full cooldown. Uses our own countdown, so it
        /// doesn't depend on whether effectshud already removed the invis effect.</summary>
        public void NotifyStealthBrokenByAttack()
        {
            if (entity.Api.Side != EnumAppSide.Server || stealthWindow <= 0f) return;
            stealthWindow = 0f;
            if (stealthSpellKey != null)
                entity.GetBehavior<EBSpellCooldowns>()?.SetCooldown(stealthSpellKey, stealthLockoutSeconds);
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            stealthWindow = 0f;
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);
            stealthWindow = 0f;
            if (castSpellId != null) CancelCast(silent: true);
        }

        // Eating breaks invisibility. Only fires when food is actually consumed.
        public override void OnEntityReceiveSaturation(float saturation, EnumFoodCategory foodCat = EnumFoodCategory.Unknown, float saturationLossDelay = 10f, float nutritionGainMultiplier = 1f)
        {
            base.OnEntityReceiveSaturation(saturation, foodCat, saturationLossDelay, nutritionGainMultiplier);
            if (entity.Api.Side == EnumAppSide.Server) SpellExecutor.BreakCasterInvisibility(entity);
        }

        private float EffectiveCastDuration(Spell spell)
        {
            float cr = Math.Clamp(Reduction(StatKeys.CastReductionFor(spell.LocalId)), 0f,
                BalanceConfig.Global("castReductionCap", 0.8f));
            return spell.CastDuration * (1f - cr);
        }

        private float Reduction(string stat) => entity.ReductionStat(stat);

        /// <summary>Flat <see cref="SpellCost.Resource"/> times the 1.0-based <see cref="Spell.ResourceCostMultiplierStat"/>.</summary>
        private float EffectiveResourceCost(Spell spell)
        {
            float cost = spell.Cost.Resource;
            if (cost > 0f && !string.IsNullOrEmpty(spell.ResourceCostMultiplierStat))
            {
                float mult = entity.Stats.GetBlended(spell.ResourceCostMultiplierStat);
                if (mult > 0.01f) cost *= mult;
            }
            return cost;
        }

        /// <summary>Strips <see cref="Spell.ConsumesCasterEffectId"/> once the cast has fully fired.</summary>
        private void ConsumeCastEffect(Spell spell)
        {
            if (!string.IsNullOrEmpty(spell.ConsumesCasterEffectId))
                SpellExecutor.RemoveEffect(entity, spell.ConsumesCasterEffectId!);
        }

        // ---- Channeled cast progress / interrupts (server) ----
        public override void OnGameTick(float deltaTime)
        {
            if (entity.Api.Side != EnumAppSide.Server) return;

            if (stealthWindow > 0f) stealthWindow -= deltaTime;

            if (castSpellId == null)
            {
                ClearBeamFx(); // a death mid-channel skips the normal end paths
                return;
            }

            // TriesToMove only covers walk directions, so Jump is checked separately.
            var castControls = (entity as EntityAgent)?.Controls;
            if (castControls != null && (InterruptFlags & CastInterruptFlags.Movement) != 0
                && (castControls.TriesToMove || castControls.Jump)) { CancelCast(); return; }
            if (ControlState.PreventsCasting(entity)) { CancelCast(); return; }

            // Fire due bolts before the end check so the final one (at exactly castEndMs) isn't dropped.
            while (isChanneling && channelTicksRemaining > 0 && channelSpell != null && entity.World.ElapsedMilliseconds >= nextTickMs)
            {
                ExecuteOnly(channelSpell, castAim ?? AimContext.None);
                channelTicksRemaining--;
                nextTickMs += (long)(channelTickInterval * 1000f);
            }

            if (entity.World.ElapsedMilliseconds >= castEndMs)
            {
                if (isChanneling)
                {
                    // Periodic channel consumes its effect here; a zone channel has channelSpell null and consumed in ExecuteAndPay.
                    var ended = channelSpell;
                    castSpellId = null; castAim = null; isChanneling = false; channelTicksRemaining = 0; channelSpell = null;
                    ClearBeamFx();
                    if (ended != null) ConsumeCastEffect(ended);
                }
                else FinishCast();
            }
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);
            // Charge casts break on damage; channels don't.
            if (entity.Api.Side == EnumAppSide.Server && castSpellId != null && !isChanneling
                && (InterruptFlags & CastInterruptFlags.Damage) != 0
                && damage > 0f && damage >= MinInterruptDamage && damageSource?.Type != EnumDamageType.Heal)
                CancelCast();
        }

        private void FinishCast()
        {
            var mod = canrpgclassesModSystem.For(entity.Api);
            string? id = castSpellId;
            var aim = castAim ?? AimContext.None;
            castSpellId = null;
            castAim = null;
            if (id == null || mod == null || !mod.Spells.TryGet(id, out var spell)) return;

            // Aura upkeep may have drained the pool during the cast (Spend clamps at 0, so it'd fire on credit).
            if (!ResourceState.Has(entity, ResourceState.PrimaryPool(entity), castResourceCost))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-no-resource"));
                return;
            }
            // The world may have changed during the cast (portal spot built over, anchor moved).
            if (entity is EntityAgent checker && SpellExecutor.Precheck(checker, spell, aim) is string refusal)
            {
                SendMessage(refusal);
                return;
            }

            ExecuteAndPay(spell, aim, castResourceCost);
        }

        private void CancelCast(bool silent = false)
        {
            if (castSpellId == null) return;
            InterruptCount++;
            if (isChanneling) { canrpgclasses.Core.Execution.ZoneManager.CancelChannelZones(entity); isChanneling = false; }
            channelTicksRemaining = 0;
            channelSpell = null;
            castSpellId = null;
            castAim = null;
            ClearBeamFx();
            SendCastCancel();
            if (!silent) SendMessage(Lang.Get("canrpgclasses:msg-cast-interrupted"));
        }

        private void ClearBeamFx()
        {
            if (entity.WatchedAttributes.GetLong(Visuals.EffectVisuals.BeamTargetAttr, 0) != 0)
                entity.WatchedAttributes.SetLong(Visuals.EffectVisuals.BeamTargetAttr, 0);
        }

        private void SendCastCancel()
        {
            var mod = canrpgclassesModSystem.For(entity.Api);
            if (mod?.ServerChannel == null) return;

            // Nearby players too, so onlookers' world cast bars hide; includes the caster.
            var packet = new SpellCastCancelPacket { CasterEntityId = entity.EntityId };
            foreach (var p in entity.World.GetPlayersAround(entity.ServerPos.XYZ, CastBroadcastRadius, CastBroadcastRadius))
            {
                if (p is IServerPlayer sp) mod.ServerChannel.SendPacket(packet, sp);
            }
        }

        private void BroadcastCast(Spell spell, float durationOverride = -1f)
        {
            var mod = canrpgclassesModSystem.For(entity.Api);
            if (mod?.ServerChannel == null) return;

            var packet = new SpellCastSyncPacket
            {
                CasterEntityId = entity.EntityId,
                SpellId = spell.Id,
                CastMode = (int)spell.CastMode,
                Duration = durationOverride >= 0f ? durationOverride : spell.CastDuration
            };

            foreach (var p in entity.World.GetPlayersAround(entity.ServerPos.XYZ, CastBroadcastRadius, CastBroadcastRadius))
            {
                if (p is IServerPlayer sp) mod.ServerChannel.SendPacket(packet, sp);
            }
        }

        /// <summary>Radius in blocks for cast start/cancel packets.</summary>
        private const int CastBroadcastRadius = 64;

        private static bool HoldingBow(Entity e)
        {
            var slot = (e as EntityPlayer)?.Player?.InventoryManager?.ActiveHotbarSlot;
            return slot?.Itemstack?.Collectible?.Tool == EnumTool.Bow;
        }

        /// <summary>Shield in either hand; mirrors ModSystemWearableStats.applyShieldProtection.</summary>
        private static bool HoldingShield(Entity e)
        {
            if (e is not EntityAgent agent) return false;
            return IsShield(agent.LeftHandItemSlot?.Itemstack) || IsShield(agent.RightHandItemSlot?.Itemstack);
        }

        private static bool IsShield(ItemStack? stack) => stack?.ItemAttributes?["shield"]?.Exists == true;

        private void SendMessage(string message)
        {
            var mod = canrpgclassesModSystem.For(entity.Api);
            if (mod?.ServerChannel == null) return;
            if ((entity as EntityPlayer)?.Player is IServerPlayer player)
            {
                mod.ServerChannel.SendPacket(new SpellMessagePacket { Message = message }, player);
            }
        }

        // ---- Triggers (MELEE_IMPACT: consume stash stacks; apply weapon coating) ----
        public override void DidAttack(DamageSource source, EntityAgent targetEntity, ref EnumHandling handled)
        {
            if (entity.Api.Side != EnumAppSide.Server) return;

            var mod = canrpgclassesModSystem.For(entity.Api);
            if (mod == null) return;

            // Consume one stash stack per hit; ids are precomputed at registry scan.
            var effects = entity.GetBehavior<EBEffects>();
            if (effects != null)
            {
                var stashIds = mod.Spells.MeleeStashEffectIds;
                for (int i = 0; i < stashIds.Count; i++) ConsumeStack(effects, stashIds[i]);
            }

            // Empower is not applied here: DidAttack runs after the swing, inside the target's i-frames. See
            // TakeEmpoweredMeleeBonus.

            ApplyOwnWeaponCoating(targetEntity);

            // Talent perks registered via Talent.OnMeleeHit.
            if (targetEntity != null) MeleeHitHooks.Apply(entity, targetEntity);

            NotifyStealthBrokenByAttack();
        }

        /// <summary>Consumes the empower and returns the bonus to add to the triggering swing; a second ReceiveDamage
        /// would land in the target's 500ms i-frames.</summary>
        public float TakeEmpoweredMeleeBonus(Entity targetEntity)
        {
            if (targetEntity == null || entity.Api.Side != EnumAppSide.Server) return 0f;
            var wa = entity.WatchedAttributes;
            float bonus = wa.GetFloat(SpellExecutor.EmpowerDamageKey, 0f);
            // Missing-health part (Death Blow) scaled by this target's missing-HP fraction.
            float missingMax = wa.GetFloat(SpellExecutor.EmpowerMissingDamageKey, 0f);
            if (missingMax > 0f) bonus += missingMax * SpellExecutor.MissingHealthFraction(targetEntity);
            if (bonus <= 0f) return 0f;

            float applied = 0f;
            if (entity.World.ElapsedMilliseconds <= empowerUntilMs)
            {
                // Physical bonus folds into the swing; a magical one lands as its own hit. IgnoreInvFrames pierces
                // i-frames, TicksPerDuration = 2 keeps it from arming new ones (ReceiveDamage arms only when < 2).
                var school = (SpellSchool)wa.GetInt(SpellExecutor.EmpowerSchoolKey, (int)SpellSchool.PhysicalMelee);
                var def = DamageSchools.For(school);

                entity.Api.Logger.VerboseDebug(
                    "[canrpgclasses] TakeEmpoweredMeleeBonus armedBy={0} school={1} physical={2} storedBonus={3:0.###}",
                    wa.GetString(SpellExecutor.EmpowerSpellKey, "?"), school, def.Physical, bonus);

                if (def.Physical)
                {
                    applied = bonus;
                }
                else
                {
                    targetEntity.ReceiveDamage(new CanrpgDamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        CauseEntity = entity,
                        School = school,
                        Type = def.EngineType,
                        IgnoreInvFrames = true,
                        TicksPerDuration = 2
                    }, bonus);
                }

                int combo = wa.GetInt(SpellExecutor.EmpowerComboKey, 0);
                if (combo > 0) ResourceState.AddCombo(entity, combo);

                string armedBy = wa.GetString(SpellExecutor.EmpowerSpellKey, "");
                if (!string.IsNullOrEmpty(armedBy)
                    && canrpgclassesModSystem.For(entity.Api) is { } mod
                    && mod.Spells.TryGet(armedBy, out var armedSpell))
                {
                    foreach (var imp in armedSpell.Impacts)
                        if (imp.Action == ImpactAction.EmpowerNextMelee && imp.Particles != null)
                        { imp.Particles.Emit(entity.World, targetEntity); break; }
                }
            }

            ClearEmpowerHudBuffs();

            wa.RemoveAttribute(SpellExecutor.EmpowerDamageKey);
            wa.RemoveAttribute(SpellExecutor.EmpowerMissingDamageKey);
            wa.RemoveAttribute(SpellExecutor.EmpowerKnockbackKey);
            wa.RemoveAttribute(SpellExecutor.EmpowerSchoolKey);
            wa.RemoveAttribute(SpellExecutor.EmpowerComboKey);
            wa.RemoveAttribute(SpellExecutor.EmpowerUntilKey);
            wa.RemoveAttribute(SpellExecutor.EmpowerSpellKey);
            return applied;
        }

        /// <summary>Only one empower is ever active, so this keeps at most one empower icon showing.</summary>
        public void ClearEmpowerHudBuffs()
        {
            var hudBuffs = entity.GetBehavior<EBEffects>();
            if (hudBuffs?.activeEffects == null) return;
            var toExpire = new List<EffectBase>();
            foreach (var kv in hudBuffs.activeEffects)
                if (canrpgclasses.Core.Effects.EmpowerEffectIds.IsEmpowerEffect(kv.Key) && kv.Value != null) toExpire.Add(kv.Value);
            foreach (var e in toExpire) e.SetExpiryImmediately();
        }

        /// <summary>Applies a negative effectshud effect; false if unavailable. Used by talent hooks.</summary>
        public static bool ApplyEffectshud(Entity target, string id, int tier, int seconds)
        {
            var effMod = target.Api.ModLoader.GetModSystem<EffectsHud>();
            if (effMod?.effects != null && effMod.effects.TryGetValue(id, out var type)
                && Activator.CreateInstance(type) is EffectBase eff)
            {
                eff.Tier = tier;
                eff.SetExpiryInRealSeconds(seconds);
                eff.positive = false;
                return EffectsHud.ApplyEffectOnEntity(target, eff);
            }
            return false;
        }

        /// <summary>One coating effect per hit until charges run out. Own tag, so simplealchemy never touches it.</summary>
        private void ApplyOwnWeaponCoating(Entity targetEntity)
        {
            if (targetEntity == null) return;

            var slot = (entity as EntityPlayer)?.Player?.InventoryManager?.ActiveHotbarSlot;
            var stack = slot?.Itemstack;
            var tree = stack?.Attributes.GetTreeAttribute(SpellExecutor.WeaponCoatKey);
            if (tree == null) return;

            int charges = tree.GetInt("charges");
            if (charges <= 0)
            {
                stack!.Attributes.RemoveAttribute(SpellExecutor.WeaponCoatKey);
                slot!.MarkDirty();
                return;
            }

            ApplyCoatingEffect(targetEntity, tree.GetString("potionId", "poison"), tree.GetInt("tier", 1));
            tree.SetInt("charges", charges - 1);
            slot!.MarkDirty();
        }

        /// <summary>Durations mirror simplealchemy.</summary>
        private static void ApplyCoatingEffect(Entity target, string potionId, int tier)
        {
            var effMod = target.Api.ModLoader.GetModSystem<EffectsHud>();
            if (effMod?.effects != null && effMod.effects.TryGetValue(potionId, out var type)
                && Activator.CreateInstance(type) is EffectBase eff)
            {
                eff.Tier = tier;
                eff.SetExpiryInRealSeconds(potionId == "poison" ? 15 + tier * 10 : 20 + tier * 10);
                eff.positive = false;
                if (EffectsHud.ApplyEffectOnEntity(target, eff)) return;
            }

            // Fallback for mobs without EBEffectsAffected: flat poison damage.
            if (potionId == "poison")
            {
                target.ReceiveDamage(new DamageSource
                {
                    Source = EnumDamageSource.Internal,
                    Type = EnumDamageType.Poison
                }, tier * 2f);
            }
        }

        private static void ConsumeStack(EBEffects effects, string effectId)
        {
            if (!effects.TryGetEffect(effectId, out var eff)) return;
            if (eff.Tier > 1)
            {
                eff.Tier -= 1;
                eff.OnStart(); // re-apply the (now lower) stat bonus
            }
            else
            {
                eff.SetExpiryImmediately();
            }
        }
    }
}
