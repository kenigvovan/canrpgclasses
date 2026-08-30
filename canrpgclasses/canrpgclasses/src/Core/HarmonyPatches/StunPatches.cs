using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Control;

namespace canrpgclasses.Core.HarmonyPatches
{
    /// <summary>
    /// Harmony prefixes enforcing crowd control, gated on <see cref="canrpgclasses.Core.Control.ControlState"/>:
    /// they block movement modules, attacking and item drops. The knockback module still runs - that's how a
    /// feared player is moved.
    /// </summary>
    public static class StunPatches
    {
        // PModule*.Applicable(Entity, EntityPos, EntityControls)
        public static bool Prefix_PModuleApplicable(Entity entity)
            => !ControlState.PreventsMovement(entity);

        // Entity.ReceiveDamage(DamageSource, float). Wrapped so a bug on this hot path can never throw into -
        // and break - vanilla's damage pipeline.
        public static bool Prefix_ReceiveDamage(Entity __instance, DamageSource damageSource, ref float damage)
        {
            try { return ReceiveDamageCore(__instance, damageSource, ref damage); }
            catch (Exception ex)
            {
                __instance?.Api?.Logger?.Warning("[canrpgclasses] ReceiveDamage patch error: {0}", ex);
                return true; // let the original damage proceed unmodified
            }
        }

        private static bool ReceiveDamageCore(Entity __instance, DamageSource damageSource, ref float damage)
        {
            if (damageSource == null) return true;
            // Healing skips the stun/damage-perk pipeline below; the mortal-wound heal cut is handled by
            // WoundedEffect, which hooks onDamaged before ApplyHealing.
            if (damageSource.Type == EnumDamageType.Heal) return true;
            var attacker = damageSource.CauseEntity ?? damageSource.SourceEntity;
            if (ControlState.PreventsActions(attacker)) return false;

            // Travel-form lock: a wolf can't swing a weapon. Only a real player swing is blocked - totems, shields
            // and DoTs come through with Source == Entity, so what you put down before transforming keeps working.
            if (damageSource.Source == EnumDamageSource.Player && IsPhysicalAttack(damageSource.Type)
                && (attacker?.WatchedAttributes?.GetBool(CombatFlags.FormActionLock) ?? false))
                return false;

            // Order from here: talent multipliers, evasion, empower, DR, absorb. Multipliers before evasion is
            // behaviour-neutral - a dodged hit is cancelled outright.
            if (damage > 0f) DamageModifiers.Apply(__instance, attacker, damageSource, ref damage);

            if (damage > 0f && attacker != null && IsPhysicalAttack(damageSource.Type) && RollEvasion(__instance))
                return false;

            // Empowered next strike: folded into this hit rather than dealt separately in DidAttack, which would
            // land inside the target's fresh i-frames and be dropped. Only a player swing consumes it.
            if (attacker != null && damage > 0f && damageSource.Source == EnumDamageSource.Player)
            {
                var caster = attacker.GetBehavior<canrpgclasses.Core.EB.EBSpellCaster>();
                if (caster != null) damage += caster.TakeEmpoweredMeleeBonus(__instance);
            }

            // Flat damage reduction, capped at 50%. Before the shield, so the absorb pool soaks what's left.
            if (damage > 0f)
            {
                float dr = Math.Clamp(__instance.ReductionStat(StatKeys.DamageReduction), 0f,
                    canrpgclasses.Core.Config.BalanceConfig.Global("damageReductionCap", 0.5f));
                if (dr > 0f) damage *= 1f - dr;
            }

            // Magic resistance. Only our spell hits carry a School; physical ones already have worn armor and the
            // DR above, so they skip this. Vanilla armor does nothing against Injury/Fire/Frost - this is the
            // only defence against them.
            if (damage > 0f && damageSource is Core.Spells.CanrpgDamageSource cds)
            {
                var def = Core.Spells.DamageSchools.For(cds.School);
                if (!def.Physical)
                {
                    float res = InnateMagicResist(__instance)
                              + __instance.ReductionStat(def.ResistStat)
                              + __instance.ReductionStat(StatKeys.MagicResist);
                    // Penetration eats into the resist before the cap, toward 0 - never into a damage bonus.
                    if (attacker != null)
                        res -= attacker.ReductionStat(def.PenStat) + attacker.ReductionStat(StatKeys.MagicPen);
                    res = Math.Clamp(res, 0f, canrpgclasses.Core.Config.BalanceConfig.Global("magicResistCap", 0.6f));
                    if (res > 0f) damage *= 1f - res;
                }
            }

            if (damage > 0f) AbsorbDamage(__instance, ref damage);

            // Break-on-damage controls end on a real hit. After absorb, so a fully-soaked hit doesn't break them.
            if (damage > 0f) ControlState.OnDamage(__instance);

            // Combat tag on both sides. Players only - EBCombatState expires it and is only attached to players,
            // so a tagged mob would keep the flag forever.
            if (damage > 0f)
            {
                float tagSeconds = canrpgclasses.Core.Config.BalanceConfig.Global("combatTagSeconds", 6f);
                long tagUntil = __instance.World.ElapsedMilliseconds + (long)(tagSeconds * 1000f);
                Tag(__instance, tagUntil);
                if (attacker != null) Tag(attacker, tagUntil);
            }

            return true;
        }

        /// <summary>True while <paramref name="e"/> is tagged "in combat" (a real hit dealt or taken recently).</summary>
        public static bool IsInCombat(Entity? e) => e?.WatchedAttributes?.GetBool(CombatFlags.InCombat) ?? false;

        private static void Tag(Entity e, long until)
        {
            if (e is not EntityPlayer) return;
            var wa = e.WatchedAttributes;
            if (until > wa.GetLong(CombatFlags.InCombatUntil, 0)) wa.SetLong(CombatFlags.InCombatUntil, until);
            wa.SetBool(CombatFlags.InCombat, true);
        }

        /// <summary>Consumes the target's absorb shield (set by SpellExecutor.ApplyShield) before damage lands.</summary>
        private static void AbsorbDamage(Entity victim, ref float damage)
        {
            var wa = victim?.WatchedAttributes;
            if (wa == null) return;
            float shield = wa.GetFloat(CombatFlags.Absorb, 0f);
            if (shield <= 0f) return;
            if (victim!.World.ElapsedMilliseconds > wa.GetLong(CombatFlags.AbsorbUntil, 0)) { wa.RemoveAttribute(CombatFlags.Absorb); return; }

            float soak = Math.Min(shield, damage);
            damage -= soak;
            shield -= soak;
            if (shield > 0f) wa.SetFloat(CombatFlags.Absorb, shield);
            else { wa.RemoveAttribute(CombatFlags.Absorb); wa.RemoveAttribute(CombatFlags.AbsorbUntil); }
        }

        /// <summary>Innate magic resistance every character carries, scaling linearly with level between
        /// baseMagicResistMin and baseMagicResistMax. Stacks with the worn and talent resists under the same cap.
        /// Mobs have no progression, so they read level 1. Public so the character sheet shows the same number.</summary>
        public static float InnateMagicResist(Entity e)
        {
            float min = canrpgclasses.Core.Config.BalanceConfig.Global("baseMagicResistMin", 0.01f);
            float max = canrpgclasses.Core.Config.BalanceConfig.Global("baseMagicResistMax", 0.15f);
            int level = e.GetBehavior<Core.Progression.EBProgression>()?.Level ?? 1;
            int cap = Core.Progression.EBProgression.MaxLevel;
            float t = cap > 1 ? Math.Clamp((level - 1f) / (cap - 1f), 0f, 1f) : 0f;
            return min + (max - min) * t;
        }

        /// <summary>Physical melee/ranged attack types - the ones Evasion can dodge (not fire/poison/fall/etc.).</summary>
        private static bool IsPhysicalAttack(EnumDamageType type)
            => type == EnumDamageType.BluntAttack || type == EnumDamageType.SlashingAttack || type == EnumDamageType.PiercingAttack;

        /// <summary>Rolls the victim's Evasion (set by SpellExecutor.ApplyEvasion): true = the hit is dodged.
        /// EBCombatState owns expiry; the deadline check here is just a cheap guard against acting on a flag that
        /// happens to be a tick stale.</summary>
        private static bool RollEvasion(Entity victim)
        {
            var wa = victim?.WatchedAttributes;
            if (wa == null) return false;
            float chance = wa.GetFloat(CombatFlags.Evasion, 0f);
            if (chance <= 0f) return false;
            if (victim!.World.ElapsedMilliseconds > wa.GetLong(CombatFlags.EvasionUntil, 0))
            {
                wa.RemoveAttribute(CombatFlags.Evasion);
                wa.RemoveAttribute(CombatFlags.EvasionUntil);
                return false;
            }
            return victim.World.Rand.NextDouble() < chance;
        }

        // ServerPlayerInventoryManager.DropItem(ItemSlot, bool) - '___player' injects the base 'player' field.
        public static bool Prefix_DropItem(IPlayer ___player) => !ControlState.PreventsActions(___player?.Entity);
    }
}
