using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Control;

namespace canrpgclasses.Core.HarmonyPatches
{
    /// <summary>CC prefixes gated on <see cref="canrpgclasses.Core.Control.ControlState"/>, plus the damage pipeline.
    /// The knockback module still runs - that's how a feared player is moved.</summary>
    public static class StunPatches
    {
        // PModule*.Applicable(Entity, EntityPos, EntityControls)
        public static bool Prefix_PModuleApplicable(Entity entity)
            => !ControlState.PreventsMovement(entity);

        // Wrapped so a bug here can never break vanilla's damage pipeline.
        public static bool Prefix_ReceiveDamage(Entity __instance, DamageSource damageSource, ref float damage)
        {
            try { return ReceiveDamageCore(__instance, damageSource, ref damage); }
            catch (Exception ex)
            {
                __instance?.Api?.Logger?.Warning("[canrpgclasses] ReceiveDamage patch error: {0}", ex);
                return true;
            }
        }

        private static bool ReceiveDamageCore(Entity __instance, DamageSource damageSource, ref float damage)
        {
            if (damageSource == null) return true;
            // Heal cut is handled by WoundedEffect.
            if (damageSource.Type == EnumDamageType.Heal) return true;
            var attacker = damageSource.CauseEntity ?? damageSource.SourceEntity;
            if (ControlState.PreventsActions(attacker)) return false;

            // Travel-form lock blocks only real player swings; totems and DoTs come through with Source == Entity.
            if (damageSource.Source == EnumDamageSource.Player && IsPhysicalAttack(damageSource.Type)
                && (attacker?.WatchedAttributes?.GetBool(CombatFlags.FormActionLock) ?? false))
                return false;

            // Order: talent multipliers, evasion, empower, DR, magic resist, absorb.
            if (damage > 0f) DamageModifiers.Apply(__instance, attacker, damageSource, ref damage);

            if (damage > 0f && attacker != null && IsPhysicalAttack(damageSource.Type) && RollEvasion(__instance))
                return false;

            // Empower is folded into this hit: a separate hit from DidAttack would land in the target's i-frames.
            // Only melee swings consume it, not spells or DoTs.
            if (attacker != null && damage > 0f && (damageSource.Source == EnumDamageSource.Player || IsNpcMeleeHit(attacker, damageSource)))
            {
                var caster = attacker.GetBehavior<canrpgclasses.Core.EB.EBSpellCaster>();
                if (caster != null) damage += caster.TakeEmpoweredMeleeBonus(__instance);
            }

            // Flat DR before the shield, so absorb soaks what's left.
            if (damage > 0f)
            {
                float dr = Math.Clamp(__instance.ReductionStat(StatKeys.DamageReduction), 0f,
                    canrpgclasses.Core.Config.BalanceConfig.Global("damageReductionCap", 0.5f));
                if (dr > 0f) damage *= 1f - dr;
            }

            // Magic resist for our non-physical spell hits; vanilla armor does nothing against Injury/Fire/Frost.
            if (damage > 0f && damageSource is Core.Spells.CanrpgDamageSource cds)
            {
                var def = Core.Spells.DamageSchools.For(cds.School);
                if (!def.Physical)
                {
                    float res = InnateMagicResist(__instance)
                              + __instance.ReductionStat(def.ResistStat)
                              + __instance.ReductionStat(StatKeys.MagicResist);
                    // Penetration floors at 0 resist, never a damage bonus.
                    if (attacker != null)
                        res -= attacker.ReductionStat(def.PenStat) + attacker.ReductionStat(StatKeys.MagicPen);
                    res = Math.Clamp(res, 0f, canrpgclasses.Core.Config.BalanceConfig.Global("magicResistCap", 0.6f));
                    if (res > 0f) damage *= 1f - res;
                }
            }

            if (damage > 0f) AbsorbDamage(__instance, ref damage);

            // After absorb, so a fully-soaked hit doesn't break CC.
            if (damage > 0f) ControlState.OnDamage(__instance);

            // Players only: EBCombatState expires the tag and only players have it.
            if (damage > 0f)
            {
                float tagSeconds = canrpgclasses.Core.Config.BalanceConfig.Global("combatTagSeconds", 6f);
                long tagUntil = __instance.World.ElapsedMilliseconds + (long)(tagSeconds * 1000f);
                Tag(__instance, tagUntil);
                if (attacker != null) Tag(attacker, tagUntil);
            }

            return true;
        }

        // AI melee: Source Entity, dealt by the attacker itself, not one of our spell hits.
        private static bool IsNpcMeleeHit(Entity attacker, DamageSource source)
            => attacker is not EntityPlayer && source.Source == EnumDamageSource.Entity && source.SourceEntity == attacker
               && source is not Core.Spells.CanrpgDamageSource;

        public static bool IsInCombat(Entity? e) => e?.WatchedAttributes?.GetBool(CombatFlags.InCombat) ?? false;

        private static void Tag(Entity e, long until)
        {
            if (e is not EntityPlayer) return;
            var wa = e.WatchedAttributes;
            if (until > wa.GetLong(CombatFlags.InCombatUntil, 0)) wa.SetLong(CombatFlags.InCombatUntil, until);
            wa.SetBool(CombatFlags.InCombat, true);
        }

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

        /// <summary>Innate resist, linear in level from baseMagicResistMin to baseMagicResistMax; mobs read level 1.
        /// Stacks with other resists under the same cap.</summary>
        public static float InnateMagicResist(Entity e)
        {
            float min = canrpgclasses.Core.Config.BalanceConfig.Global("baseMagicResistMin", 0.01f);
            float max = canrpgclasses.Core.Config.BalanceConfig.Global("baseMagicResistMax", 0.15f);
            int level = e.GetBehavior<Core.Progression.EBProgression>()?.Level ?? 1;
            int cap = Core.Progression.EBProgression.MaxLevel;
            float t = cap > 1 ? Math.Clamp((level - 1f) / (cap - 1f), 0f, 1f) : 0f;
            return min + (max - min) * t;
        }

        /// <summary>Attack types Evasion can dodge.</summary>
        private static bool IsPhysicalAttack(EnumDamageType type)
            => type == EnumDamageType.BluntAttack || type == EnumDamageType.SlashingAttack || type == EnumDamageType.PiercingAttack;

        /// <summary>True = dodged. The deadline check guards against a flag that's a tick stale; EBCombatState owns expiry.</summary>
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

        // ServerPlayerInventoryManager.DropItem; '___player' injects the base 'player' field.
        public static bool Prefix_DropItem(IPlayer ___player) => !ControlState.PreventsActions(___player?.Entity);
    }
}
