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
    /// <summary>
    /// Drives the cast process for an entity, server authoritative: validate, run the SpellExecutor,
    /// broadcast, pay the cost, start the cooldown. Instant, charged and channelled casts all go through
    /// here.
    /// </summary>
    public class EBSpellCaster : EntityBehavior
    {
        public const string Name = "canrpgspellcaster";

        public EBSpellCaster(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        // Active channeled (CastMode.Charge) cast - server-only. Null when not casting.
        private string? castSpellId;
        private AimContext? castAim;
        private long castEndMs;
        // Resource cost captured for the in-flight cast at the moment it was committed (TryCast) - so a cost that
        // scales with a stack the cast itself grants (Mystic Charges: Mystic Blast's mana rises per charge) reflects
        // the charges the caster had before this cast, not after. Read by PayCost / FinishCast for a Charge cast.
        private float castResourceCost;
        // Channel (Ice Storm): the spell fired at cast START and its effect (a ground zone) is kept alive only while
        // this channel runs; moving or being CC'd ends the channel early and cancels the zone. Distinguishes a
        // channel from a Charge cast (which fires on COMPLETION and breaks on damage).
        private bool isChanneling;
        // Periodic-fire channel (Mystic Barrage): bolts still to fire this channel, the interval between them, and the
        // server-time deadline for the next one. Cost/cooldown were already paid up front at the channel start.
        private int channelTicksRemaining;
        private float channelTickInterval;
        private long nextTickMs;
        private Spell? channelSpell; // the spell whose bolts the periodic channel fires (avoids a per-tick registry lookup)

        // Seconds left in the current stealth invisibility window (0 = not stealthed). A plain decrementing field
        // (not WatchedAttributes), like dummyplayer's pvp combat tag: transient, ticks down in OnGameTick, reset on
        // load/death - so it's restart/relog-safe without any absolute-clock comparison. While > 0, attacking
        // rewrites the opening spell's cooldown to the full lockout (see NotifyStealthBrokenByAttack).
        private float stealthWindow;

        // Which spell opened the current stealth window: its cooldown key + full lockout seconds, remembered at
        // cast so NotifyStealthBrokenByAttack rewrites THAT spell's cooldown (the core doesn't know spell ids -
        // any spell with OpensStealthWindowSeconds > 0 works). Transient, like stealthWindow.
        private string? stealthSpellKey;
        private float stealthLockoutSeconds;

        // Server-time deadline (ms) to land the "empowered next strike" (Zealot's Strike / Vicious Strike). A plain
        // TRANSIENT field, not the old persisted WatchedAttribute: World.ElapsedMilliseconds resets between sessions
        // while WA persists, so a saved deadline went stale and every hit read as expired (now > until) → no bonus.
        // Transient → defaults to 0 (expired) on every load; set fresh on arm. Armed from SpellExecutor.
        private long empowerUntilMs;

        /// <summary>Opens the empowered-next-strike window for <paramref name="seconds"/> (called by SpellExecutor's
        /// EmpowerNextMelee impact). Server-time deadline kept off WatchedAttributes so it can't persist stale.</summary>
        public void ArmEmpowerWindow(float seconds) => empowerUntilMs = entity.World.ElapsedMilliseconds + (long)(seconds * 1000f);

        // Instant-cast window: the next matching cast skips its cast time. Set by Clear Mind ("*" = any spell) or
        // by a proc for a specific spell (Volatile Ember → "canrpgclasses:fire_bolt"). Transient (World-time deadline), so
        // it never persists stale across a relog. Consumed the moment an eligible spell is cast.
        private string? instantCastSpellId;
        private long instantCastUntilMs;
        private string? instantCastMarkerId; // cosmetic HUD effect to clear when the window is consumed

        /// <summary>Grants an "instant next cast" window: the next cast of <paramref name="spellIdOrStar"/> (or any spell
        /// when it's "*") within <paramref name="windowSeconds"/> ignores its cast time. A later grant overwrites an
        /// earlier one. <paramref name="markerEffectId"/> is a cosmetic HUD effect that gets cleared when the window is
        /// used (so the icon disappears the moment you fire), if the caller applied one.</summary>
        public void GrantInstantCast(string spellIdOrStar, float windowSeconds, string? markerEffectId = null)
        {
            instantCastSpellId = spellIdOrStar;
            instantCastUntilMs = entity.World.ElapsedMilliseconds + (long)(windowSeconds * 1000f);
            instantCastMarkerId = markerEffectId;
        }

        /// <summary>If an instant-cast window is active and covers this spell, consumes it (clearing its HUD marker) and
        /// returns true (the caller then fires the spell instantly instead of starting its cast).</summary>
        private bool ConsumeInstantCastIfEligible(Spell spell)
        {
            if (instantCastSpellId == null || entity.World.ElapsedMilliseconds > instantCastUntilMs) return false;
            if (instantCastSpellId != "*" && instantCastSpellId != spell.Id) return false;
            instantCastSpellId = null;
            if (instantCastMarkerId != null) { SpellExecutor.RemoveEffect(entity, instantCastMarkerId); instantCastMarkerId = null; }
            return true;
        }

        /// <summary>Server-side authoritative cast entry point.</summary>
        public bool TryCast(string spellId, AimContext aim)
        {
            if (entity.Api.Side != EnumAppSide.Server) return false;

            var mod = canrpgclassesModSystem.For(entity.Api);
            if (mod == null || !mod.Spells.TryGet(spellId, out var spell)) return false;

            // Can't cast while under a control that prevents casting (stun / fear / silence) - unified layer,
            // per-type message so the player knows why.
            if (ControlState.IsActive(entity, ControlType.Stun))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-stunned"));
                return false;
            }
            if (ControlState.IsActive(entity, ControlType.Fear))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-feared"));
                return false;
            }
            if (ControlState.IsActive(entity, ControlType.Silence))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-silenced"));
                return false;
            }
            if (ControlState.IsActive(entity, ControlType.Transmute))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-polymorphed"));
                return false;
            }
            // Travel-form action lock (Spirit Wolf / druid Cat/Bear): no casting - except the spell that toggles the
            // form back off, or you'd be stuck shapeshifted.
            if (!spell.BypassesFormLock && FormLockHeld())
            {
                SendMessage(Lang.Get("canrpgclasses:msg-form-locked"));
                return false;
            }

            // Form gate (druid shapeshift): a form-locked ability is castable only while its form is active. Composes
            // with the negative form-lock above: an in-form ability sets BypassesFormLock + RequiresForm.
            if (!string.IsNullOrEmpty(spell.RequiresForm)
                && entity.WatchedAttributes.GetString(AttrKeys.ActiveAura, "") != spell.RequiresForm)
            {
                SendMessage(Lang.Get("canrpgclasses:msg-wrong-form"));
                return false;
            }

            // Out-of-combat gate (Stealth): the spell declares it; Slip Away (no gate) is the mid-fight escape.
            if (spell.RequiresOutOfCombat && canrpgclasses.Core.HarmonyPatches.StunPatches.IsInCombat(entity))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-in-combat"));
                return false;
            }

            // Bow gate (hunter shots): these empower the next real bow shot, so a bow must be in hand.
            if (spell.RequiresBow && !HoldingBow(entity))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-need-bow"));
                return false;
            }

            if (spell.RequiresShield && !HoldingShield(entity))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-need-shield"));
                return false;
            }

            // Attribute gate: a spell can ask for e.g. 15 intelligence. Failing costs neither cooldown nor resource.
            if (!Attributes.AttributeStats.Meets(entity, spell.RequiresAttributes, out string missingAttr))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-need-attribute", missingAttr));
                return false;
            }

            var cooldowns = entity.GetBehavior<EBSpellCooldowns>();
            string cdKey = spell.CooldownKey;
            if (cooldowns != null && cooldowns.IsOnCooldown(cdKey))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-on-cooldown", spell.DisplayName, cooldowns.RemainingSeconds(cdKey).ToString("0.0")));
                return false;
            }

            // Global cooldown: a single shared entry that every active ability both respects and refreshes, so the
            // rotation can't be mashed faster than globalCooldownSeconds no matter how cheap the abilities are.
            // Silent by design - the hotbar sweeps every slot during the GCD, and a 1s lock would spam chat on key mash.
            if (spell.TriggersGlobalCooldown && cooldowns != null && cooldowns.IsOnCooldown(Spell.GlobalCooldownKey))
                return false;

            // Aim spells need a target. If none is under the crosshair, fail early WITHOUT spending the
            // cooldown/energy - otherwise the cast is silently wasted (e.g. shadow_step goes on cooldown but
            // does nothing). This way the player can immediately retry while aiming at the enemy.
            // Exception: an Ally smart-cast (heal/buff) falls back to the caster, so "no target" is valid.
            if (spell.Target.Type == TargetType.Aim && spell.Target.Affinity != TargetAffinity.Ally
                && entity is EntityAgent aimer
                && SpellExecutor.GetAimedEntity(aimer, aim, spell.Range > 0 ? spell.Range : 16f) == null)
            {
                SendMessage(Lang.Get("canrpgclasses:msg-no-target"));
                return false;
            }

            // Target-effect gate (druid Instant Mend): the target must ALREADY carry one of the effects this spell feeds
            // on. Resolved the same way SpellExecutor.ResolveAim will (an allied smart-cast falls back to the caster),
            // so the gate and the impact can never disagree about who was checked. Failing here costs nothing.
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
                    return false;
                }
            }

            // Resource gate (silent - the HUD shows energy/combo, so no chat spam on a key mash):
            // not enough of the class primary resource, or a finisher with no combo points built. The cost is the
            // spell's flat resource × any ResourceCostMultiplierStat (Mystic Charges), read NOW so it reflects the
            // caster's current stacks; the same figure is carried through to PayCost.
            var pool = ResourceState.PrimaryPool(entity);
            float resourceCost = EffectiveResourceCost(spell);
            if (!ResourceState.Has(entity, pool, resourceCost)) return false;
            if (spell.ComboFinisher && ResourceState.Combo(entity) <= 0) return false;
            if (!string.IsNullOrEmpty(spell.SecondaryResourceId)
                && !ResourceState.Has(entity, ResourceState.SecondaryPool(spell.SecondaryResourceId), spell.SecondaryResourceCost))
                return false;

            // Every gate has passed - the cast is happening on one of the three paths below, so start the GCD here
            // (once, for all of them). A cast-time spell runs its GCD in PARALLEL with the cast bar rather
            // than after it, so a 2s Nature Bolt is followed by no extra lockout.
            TriggerGlobalCooldown(spell);

            // Instant-cast proc (Clear Mind / Volatile Ember): if an active window covers this Charge cast, consume
            // it and fall through to the instant path below instead of starting the cast bar.
            bool instantCast = spell.CastMode == CastMode.Charge && spell.CastDuration > 0f && ConsumeInstantCastIfEligible(spell);

            // Charge cast: start a timed cast instead of firing now. Cost/cooldown are paid on completion,
            // so an interrupted cast wastes nothing. A new cast cancels any in-progress one.
            if (spell.CastMode == CastMode.Charge && spell.CastDuration > 0f && !instantCast && entity is EntityAgent)
            {
                if (castSpellId != null) CancelCast(silent: true);
                castSpellId = spell.Id;
                castAim = aim;
                castResourceCost = resourceCost; // committed cost (pre-cast charges) - paid on completion
                float castDur = EffectiveCastDuration(spell);
                castEndMs = entity.World.ElapsedMilliseconds + (long)(castDur * 1000f);
                BroadcastCast(spell, castDur); // client cast bar matches the (possibly shortened) duration
                return true;
            }

            // Channeled cast (Ice Storm): fire NOW (spawns the zone + pays), then run a channel for CastDuration -
            // the zone lives exactly that long, and moving / being CC'd cancels the channel and the zone early.
            if (spell.CastMode == CastMode.Channel && spell.CastDuration > 0f && entity is EntityAgent)
            {
                if (castSpellId != null) CancelCast(silent: true);
                BroadcastCast(spell, spell.CastDuration);
                if (spell.ChannelTicks > 0)
                {
                    // Periodic-fire channel (Mystic Barrage): pay once up front, fire the first bolt now, then fire the
                    // rest spread evenly across CastDuration (interval = duration / (ticks - 1), so the last lands at end).
                    // ConsumesCasterEffectId (Mystic Charges) is stripped at channel end (OnGameTick), after every bolt
                    // has launched and snapshotted the caster's charged spell power.
                    PayCost(spell, resourceCost);
                    ExecuteOnly(spell, aim);
                    channelSpell = spell;
                    channelTicksRemaining = spell.ChannelTicks - 1;
                    channelTickInterval = spell.ChannelTicks > 1 ? spell.CastDuration / (spell.ChannelTicks - 1) : 0f;
                    nextTickMs = entity.World.ElapsedMilliseconds + (long)(channelTickInterval * 1000f);
                    // Beam channel (Mind Rend): publish the ray's endpoints for every client. Cleared on every
                    // channel-end path (natural end / cancel) - see ClearBeamFx. Resolve the target the same way
                    // the impacts will (aim.TargetEntity is null on a pure crosshair aim).
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
                else ExecuteAndPay(spell, aim, resourceCost); // zone channel (Ice Storm): fire once + keep the zone alive
                castSpellId = spell.Id;
                castAim = aim;
                isChanneling = true;
                castEndMs = entity.World.ElapsedMilliseconds + (long)(spell.CastDuration * 1000f);
                return true;
            }

            if (castSpellId != null) CancelCast(silent: true); // an instant cast also breaks an in-progress channel
            // Force duration 0 for a proc'd instant cast (Clear Mind / Volatile Ember) so a Charge spell fired
            // instantly doesn't flash a phantom cast bar for its normal cast time (nothing tracks it → it'd never clear).
            BroadcastCast(spell, instantCast ? 0f : -1f);
            ExecuteAndPay(spell, aim, resourceCost);
            return true;
        }

        /// <summary>Runs the spell, pays its cost + cooldown, then consumes any per-cast spender effect. Shared by
        /// instant casts and channeled completion. <paramref name="resourceCost"/> is the cost captured when the cast
        /// was committed (so a cost that scales with a stack this cast grants isn't inflated by its own new stack).</summary>
        private void ExecuteAndPay(Spell spell, AimContext aim, float resourceCost)
        {
            ExecuteOnly(spell, aim);
            PayCost(spell, resourceCost);
            ConsumeCastEffect(spell);
        }

        /// <summary>Fires the spell's impacts without paying - used per-tick by a periodic-fire channel, which pays
        /// its single cost up front (see <see cref="PayCost"/>).</summary>
        private void ExecuteOnly(Spell spell, AimContext aim)
        {
            if (entity is EntityAgent agent) SpellExecutor.Execute(agent, spell, aim);
        }

        /// <summary>The negative form-lock, from both sources: the synced <c>FormActionLock</c> bool (asserted every
        /// tick by the live form effect) OR the persisted <c>ActiveAura</c> string resolving to a form-toggle spell
        /// with <see cref="Spell.AuraLocksActions"/>. The string is what actually survives a relog - deriving the
        /// lock from it too means the two can never desync (a relogged cat can't suddenly cast heals).</summary>
        private bool FormLockHeld()
        {
            var wa = entity.WatchedAttributes;
            if (wa.GetBool(CombatFlags.FormActionLock)) return true;
            string aura = wa.GetString(AttrKeys.ActiveAura, "");
            if (string.IsNullOrEmpty(aura)) return false;
            var mod = canrpgclassesModSystem.For(entity.Api);
            return mod != null && mod.Spells.TryGet(aura, out var auraSpell) && auraSpell.AuraLocksActions;
        }

        /// <summary>Starts this caster's global cooldown, unless the spell opts out (<see cref="Spell.TriggersGlobalCooldown"/>).
        /// Duration is the <c>globalCooldownSeconds</c> global - deliberately not reduced by cooldownReduction, since
        /// the GCD is the rotation's floor rather than a per-spell cost.</summary>
        private void TriggerGlobalCooldown(Spell spell)
        {
            if (!spell.TriggersGlobalCooldown) return;
            float gcd = BalanceConfig.Global("globalCooldownSeconds", 1.0f);
            if (gcd <= 0f) return;
            entity.GetBehavior<EBSpellCooldowns>()?.SetCooldown(Spell.GlobalCooldownKey, gcd);
        }

        /// <summary>Pays a spell's resource cost + sets its cooldown + opens any stealth window. Split from firing so a
        /// periodic-fire channel can fire many times for one payment. <paramref name="resourceCost"/> is the effective
        /// cost captured when the cast was committed (<see cref="EffectiveResourceCost"/>).</summary>
        private void PayCost(Spell spell, float resourceCost)
        {
            var pool = ResourceState.PrimaryPool(entity);
            // Exhaust, item and durability costs are declared on SpellCost but not charged yet.
            ResourceState.Spend(entity, pool, resourceCost);
            if (!string.IsNullOrEmpty(spell.SecondaryResourceId))
                ResourceState.Spend(entity, ResourceState.SecondaryPool(spell.SecondaryResourceId), spell.SecondaryResourceCost);

            // Cooldown reduction, read from this caster's own stats: the global one plus a per-spell talent.
            float cdr = Math.Clamp(Reduction(StatKeys.CooldownReduction) + Reduction(StatKeys.CooldownReductionFor(spell.LocalId)),
                0f, BalanceConfig.Global("cooldownReductionCap", 0.8f));
            // The server-wide pace knob comes after CDR, so talents still shave the same fraction off it.
            float cd = spell.Cost.Cooldown.Duration * (1f - cdr) * BalanceConfig.Global("spellCooldownMultiplier", 1f);
            entity.GetBehavior<EBSpellCooldowns>()?.SetCooldown(spell.CooldownKey, cd);

            // Stealth-window spells (Stealth): open the invis window. The spell's cd (< invis duration) frees
            // while still hidden → seamless out-of-combat re-stealth; attacking inside the window rewrites the
            // cd to the full lockout (NotifyStealthBrokenByAttack - we remember the opener here).
            if (spell.OpensStealthWindowSeconds > 0f)
            {
                stealthWindow = spell.OpensStealthWindowSeconds;
                stealthSpellKey = spell.CooldownKey;
                stealthLockoutSeconds = spell.Cost.Cooldown.Duration;
            }
        }

        /// <summary>Stealth combat gate: when the player attacks while still inside the stealth invis window, rewrite
        /// the opening spell's cooldown to its full duration (the "you revealed yourself, wait the full lockout" rule)
        /// and close the window. Order-independent - it relies only on our own countdown, not on whether the invis
        /// effect has already been removed by effectshud. Called from the melee (DidAttack) and spell-damage break paths.</summary>
        public void NotifyStealthBrokenByAttack()
        {
            if (entity.Api.Side != EnumAppSide.Server || stealthWindow <= 0f) return;
            stealthWindow = 0f;
            if (stealthSpellKey != null)
                entity.GetBehavior<EBSpellCooldowns>()?.SetCooldown(stealthSpellKey, stealthLockoutSeconds); // sync=true → HUD updates
        }

        // The Stealth window is transient combat state - start every fresh session / life with it closed.
        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            stealthWindow = 0f;
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);
            stealthWindow = 0f;
            if (castSpellId != null) CancelCast(silent: true); // drop any in-progress cast/channel (and its zone)
        }

        // Eating is a revealing action - drop break-on-attack invisibility (Stealth/Slip Away/…) and trip the Stealth
        // lockout, just like attacking or breaking a block. ReceiveSaturation dispatches this to every behavior;
        // it only fires when food is actually consumed, so no extra gating is needed.
        public override void OnEntityReceiveSaturation(float saturation, EnumFoodCategory foodCat = EnumFoodCategory.Unknown, float saturationLossDelay = 10f, float nutritionGainMultiplier = 1f)
        {
            base.OnEntityReceiveSaturation(saturation, foodCat, saturationLossDelay, nutritionGainMultiplier);
            if (entity.Api.Side == EnumAppSide.Server) SpellExecutor.BreakCasterInvisibility(entity);
        }

        /// <summary>This caster's effective cast time: the spell's CastDuration minus its per-spell castReduction
        /// stat, set by a talent on this player alone.</summary>
        private float EffectiveCastDuration(Spell spell)
        {
            float cr = Math.Clamp(Reduction(StatKeys.CastReductionFor(spell.LocalId)), 0f,
                BalanceConfig.Global("castReductionCap", 0.8f));
            return spell.CastDuration * (1f - cr);
        }

        /// <summary>This caster's "reduction" stat (cooldownReduction / castReduction_*) as a 0-based fraction.
        /// See <see cref="canrpgclasses.Core.StatExtensions.ReductionStat"/> for the 1.0-based convention.</summary>
        private float Reduction(string stat) => entity.ReductionStat(stat);

        /// <summary>This caster's effective resource cost for a spell: its flat <see cref="SpellCost.Resource"/> ×
        /// the spell's declared 1.0-based <see cref="Spell.ResourceCostMultiplierStat"/> (Mystic Charges make Arcane
        /// Blast cost more per charge). Unset stat / free spell = the flat cost.</summary>
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

        /// <summary>Strips the spell's <see cref="Spell.ConsumesCasterEffectId"/> from the caster once the cast has
        /// fully fired (Mystic Barrage / Explosion spending all Mystic Charges). No-op if the spell consumes nothing
        /// or the caster doesn't carry the effect.</summary>
        private void ConsumeCastEffect(Spell spell)
        {
            if (!string.IsNullOrEmpty(spell.ConsumesCasterEffectId))
                SpellExecutor.RemoveEffect(entity, spell.ConsumesCasterEffectId!);
        }

        // ---- Channeled cast progress / interrupts (server) ----
        public override void OnGameTick(float deltaTime)
        {
            if (entity.Api.Side != EnumAppSide.Server) return;

            // Tick down the Stealth window (closes on natural invis expiry; attacking closes it early via the gate).
            if (stealthWindow > 0f) stealthWindow -= deltaTime;

            if (castSpellId == null)
            {
                ClearBeamFx(); // self-heal: a death mid-channel skips the normal end paths but must not strand a ray
                return;
            }

            // Interrupt on movement, or any control that prevents casting (stun / fear / silence).
            // Jump is checked separately: EntityControls.TriesToMove only covers the four walk directions, so a
            // standing jump would otherwise let you channel straight through it.
            var castControls = (entity as EntityAgent)?.Controls;
            if (castControls != null && (castControls.TriesToMove || castControls.Jump)) { CancelCast(); return; }
            if (ControlState.PreventsCasting(entity)) { CancelCast(); return; }

            // Periodic-fire channel (Mystic Barrage): fire each due bolt before the end-of-channel check so the final
            // bolt (scheduled at exactly castEndMs) isn't dropped by the clear below.
            while (isChanneling && channelTicksRemaining > 0 && channelSpell != null && entity.World.ElapsedMilliseconds >= nextTickMs)
            {
                ExecuteOnly(channelSpell, castAim ?? AimContext.None);
                channelTicksRemaining--;
                nextTickMs += (long)(channelTickInterval * 1000f);
            }

            if (entity.World.ElapsedMilliseconds >= castEndMs)
            {
                // A channel already fired at its start and just ran its course - clear it (the zone expires on its
                // own timer). A Charge cast fires now, on completion.
                if (isChanneling)
                {
                    // A periodic-fire channel (Mystic Barrage) tracks its spell; strip its ConsumesCasterEffectId
                    // (Mystic Charges) HERE, after every bolt has launched and snapshotted the charged spell power.
                    // The zone channel (Ice Storm) leaves channelSpell null and already consumed in ExecuteAndPay.
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
            // Charge casts break on damage; a channel (Ice Storm) does not - only moving / CC ends it.
            if (entity.Api.Side == EnumAppSide.Server && castSpellId != null && !isChanneling
                && damage > 0f && damageSource?.Type != EnumDamageType.Heal)
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

            // Re-check the resource at completion: it was only gated in TryCast, and aura upkeep during the
            // cast may have drained it below cost (Spend clamps at 0 - the spell would fire "on credit").
            // A message (unlike TryCast's silent gate) because the player already sat through the cast bar.
            // Use the cost captured at cast start (castResourceCost) so a per-charge cost isn't re-read after any
            // charge shift during the cast.
            if (!ResourceState.Has(entity, ResourceState.PrimaryPool(entity), castResourceCost))
            {
                SendMessage(Lang.Get("canrpgclasses:msg-no-resource"));
                return;
            }

            ExecuteAndPay(spell, aim, castResourceCost); // client's bar already elapsed; no cancel packet needed
        }

        private void CancelCast(bool silent = false)
        {
            if (castSpellId == null) return;
            // A channel's effect is a live zone - kill it early when the channel is broken.
            if (isChanneling) { canrpgclasses.Core.Execution.ZoneManager.CancelChannelZones(entity); isChanneling = false; }
            channelTicksRemaining = 0;
            channelSpell = null;
            castSpellId = null;
            castAim = null;
            ClearBeamFx();
            SendCastCancel();
            if (!silent) SendMessage(Lang.Get("canrpgclasses:msg-cast-interrupted"));
        }

        /// <summary>Drops the beam-channel ray state, if any. Cheap no-op for the common case (attribute absent);
        /// SetLong only marks dirty when a beam was actually published.</summary>
        private void ClearBeamFx()
        {
            if (entity.WatchedAttributes.GetLong(Visuals.EffectVisuals.BeamTargetAttr, 0) != 0)
                entity.WatchedAttributes.SetLong(Visuals.EffectVisuals.BeamTargetAttr, 0);
        }

        private void SendCastCancel()
        {
            var mod = canrpgclassesModSystem.For(entity.Api);
            if (mod?.ServerChannel == null) return;

            // Broadcast to nearby players (same radius as BroadcastCast) so onlookers' world cast bars hide too -
            // not just the owner. GetPlayersAround includes the caster, so their own hotbar cast bar is cleared as well.
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

        /// <summary>Radius (blocks) within which start/cancel cast packets are sent, so nearby players can
        /// show/hide a world cast bar over the caster (and interrupt it). Keeps us from broadcasting to everyone.</summary>
        private const int CastBroadcastRadius = 64;

        private static bool HoldingBow(Entity e)
        {
            var slot = (e as EntityPlayer)?.Player?.InventoryManager?.ActiveHotbarSlot;
            return slot?.Itemstack?.Collectible?.Tool == EnumTool.Bow;
        }

        /// <summary>True when the entity has a shield equipped in either hand. VS shields are items carrying a
        /// "shield" attribute block, worn in the off-hand (left) slot and occasionally the main hand - mirrors the
        /// vanilla check in ModSystemWearableStats.applyShieldProtection.</summary>
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

            // slice_and_dice style: consume one stack of the caster's stash effect on hit.
            // The effect-id list is precomputed at registry scan - no per-swing scan over all spells.
            var effects = entity.GetBehavior<EBEffects>();
            if (effects != null)
            {
                var stashIds = mod.Spells.MeleeStashEffectIds;
                for (int i = 0; i < stashIds.Count; i++) ConsumeStack(effects, stashIds[i]);
            }

            // sinister_strike / crusader_strike (EmpowerNextMelee): the bonus is folded INTO this swing's
            // damage by StunPatches.Prefix_ReceiveDamage (see TakeEmpoweredMeleeBonus) - not applied here.
            // DidAttack runs *after* the swing landed, when the target is already in 500ms invulnerability
            // frames, so a second ReceiveDamage would be rejected outright and deal zero extra damage.

            // envenom / crippling_oil / seal_of_light (CoatWeapon): we apply our own coating on each hit,
            // deterministically - works the same with or without simplealchemy (own tag, see ApplyCoatWeapon).
            ApplyOwnWeaponCoating(targetEntity);

            // Talent on-melee-hit perks (Jagged Blades' bleed, …) - registered at talent scan via
            // Talent.OnMeleeHit, so this core trigger doesn't reference class content by id.
            if (targetEntity != null) MeleeHitHooks.Apply(entity, targetEntity);

            NotifyStealthBrokenByAttack();
        }

        /// <summary>Consumes the "empowered next strike" buff and returns the bonus to ADD to the triggering
        /// melee swing - folded into that single hit rather than a second ReceiveDamage, which would land inside
        /// the target's fresh 500ms invulnerability frames and be dropped. Server only.</summary>
        public float TakeEmpoweredMeleeBonus(Entity targetEntity)
        {
            if (targetEntity == null || entity.Api.Side != EnumAppSide.Server) return 0f;
            var wa = entity.WatchedAttributes;
            float bonus = wa.GetFloat(SpellExecutor.EmpowerDamageKey, 0f);
            // Death Blow-style: the arming spell stored a MAX missing-health part; fold it in now, scaled by this struck
            // target's actual missing-HP fraction (0 for normal empowers, whose missing part is unset).
            float missingMax = wa.GetFloat(SpellExecutor.EmpowerMissingDamageKey, 0f);
            if (missingMax > 0f) bonus += missingMax * SpellExecutor.MissingHealthFraction(targetEntity);
            if (bonus <= 0f) return 0f;

            float applied = 0f;
            if (entity.World.ElapsedMilliseconds <= empowerUntilMs)
            {
                // A physical-school bonus folds into the triggering swing (mitigated once, like backstab). A magical
                // school (e.g. crusader_strike / templars_verdict - Holy) lands as its own hit so a paladin's weapon
                // does physical and the bonus does holy. IgnoreInvFrames lets it pierce the swing's i-frames; TicksPerDuration
                // = 2 stops it from SETTING new i-frames (Entity.ReceiveDamage only arms invulnerability when <2), so it
                // can't block the physical swing that's mid-processing right now.
                var school = (SpellSchool)wa.GetInt(SpellExecutor.EmpowerSchoolKey, (int)SpellSchool.PhysicalMelee);
                var def = DamageSchools.For(school);

                // Balance troubleshooting (verbose-debug log level): the stored bonus at the moment it's actually
                // consumed by a swing, to compare against FinisherAmount's log at arm-time.
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

        /// <summary>Expires every "empowered next strike" HUD buff on this caster. Called when an empowered hit is
        /// consumed and when a new empower is armed - since only one empower is ever active (the WA bundle is
        /// overwritten, never stacked), this keeps exactly one icon showing so Brutal Strike and Death Blow can't
        /// appear armed on the same swing at once.</summary>
        public void ClearEmpowerHudBuffs()
        {
            var hudBuffs = entity.GetBehavior<EBEffects>();
            if (hudBuffs?.activeEffects == null) return;
            var toExpire = new List<EffectBase>();
            foreach (var kv in hudBuffs.activeEffects)
                if (canrpgclasses.Core.Effects.EmpowerEffectIds.IsEmpowerEffect(kv.Key) && kv.Value != null) toExpire.Add(kv.Value);
            foreach (var e in toExpire) e.SetExpiryImmediately();
        }

        /// <summary>Applies an effectshud effect of the given id/tier/duration to the target. Returns false if
        /// unavailable. Public so talent hooks (e.g. Jagged Blades' bleed via <c>Talent.OnMeleeHit</c>) can
        /// apply their effects without the core knowing them.</summary>
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

        /// <summary>Applies our own weapon coating (canrpgWeaponCoat) on a melee hit: one effect per hit until
        /// charges run out. Deterministic and self-contained - works the same whether or not simplealchemy is present
        /// (simplealchemy only reacts to its own "simplepoisoned" tag, so it never touches this).</summary>
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

        /// <summary>Applies a coating effect (poison/walkslow/weakmelee) through effectshud, mirroring simplealchemy.</summary>
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
                eff.SetExpiryImmediately(); // removed (and client-synced) on the next effects tick
            }
        }
    }
}
