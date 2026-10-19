using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Entities;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;

using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Hunter
{
    /// <summary>Shot abilities arm a bonus on the caster that the next real arrow consumes on hit. Split Shot's
    /// extra arrows are spawned by BowPatches on release.</summary>
    public static class HunterShots
    {
        private static bool registered;

        public static void Init()
        {
            if (registered) return;
            registered = true;
            SpellExecutor.RegisterImpact(ImpactAction.EmpowerNextShot, ApplyArm);
            DamageModifiers.RegisterPersistent(ConsumeOnArrowHit);
        }

        private static void ApplyArm(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var e = ctx.Caster;
            var wa = e.WatchedAttributes;

            // Same shape as FinisherAmount, but banked onto the next arrow instead of dealt now.
            float coeff = imp.DamageSpellPowerCoefficient + imp.DamagePerComboPoint * ctx.ComboPoints;
            wa.SetFloat(AttrKeys.ShotDamage, ctx.SpellPower * coeff);
            wa.SetFloat(AttrKeys.ShotKnockback, imp.Knockback);
            // Builders grant combo only when the arrow hits.
            wa.SetInt(AttrKeys.ShotComboGrant, ctx.Spell.ComboBuilder ? ctx.Spell.ComboPointsGenerated : 0);
            wa.SetString(AttrKeys.ShotStatusId, imp.StatusEffectId ?? "");
            wa.SetFloat(AttrKeys.ShotStatusDur, imp.StatusEffectDuration);
            wa.SetInt(AttrKeys.ShotStatusAmp, imp.StatusEffectAmplifier);
            wa.SetString(AttrKeys.ShotSpell, ctx.Spell.Id);
            int extraArrows = imp.ShotExtraArrows;
            if (extraArrows > 0 && Core.Talents.TalentState.Rank(e, "hunter:bank_shot") > 0)
                extraArrows += (int)BalanceConfig.Talent("hunter:bank_shot").F("extraArrows", 2f);
            wa.SetInt(AttrKeys.ShotExtra, extraArrows);
            wa.SetFloat(AttrKeys.ShotFullDraw, imp.ShotRequiresDrawFraction); // aimed_shot: fresh full-draw required (fraction)
            wa.SetFloat(AttrKeys.ShotHealCut, imp.ShotHealCutPercent);        // wounding_shot: -incoming healing
            wa.SetFloat(AttrKeys.ShotHealCutSecs, imp.ShotHealCutSeconds);
            wa.SetFloat(AttrKeys.ShotDrain, imp.ShotResourceDrain);          // draining_shot: -primary resource
            wa.SetBool(AttrKeys.ShotIgnite, imp.ShotIgnite);                 // incendiary_shot: set on fire
            wa.SetLong(AttrKeys.ShotArmedMs, e.World.ElapsedMilliseconds);
            float window = imp.EmpowerWindowSeconds > 0 ? imp.EmpowerWindowSeconds : 6f;
            wa.SetLong(AttrKeys.ShotUntilMs, e.World.ElapsedMilliseconds + (long)(window * 1000f));

            // Cosmetic HUD buff showing the window.
            SpellExecutor.ApplyEffect(e, EmpowerEffectIds.For(ctx.Spell.LocalId), 1, window);
            imp.Particles?.Emit(e.World, e);
        }

        private static void ConsumeOnArrowHit(Entity victim, Entity? attacker, DamageSource source, ref float damage)
        {
            if (damage <= 0f || attacker == null || victim == attacker) return;
            if (victim?.World?.Side != EnumAppSide.Server) return;
            // Vanilla arrows only, not our spell projectiles.
            if (source.SourceEntity is not EntityProjectile proj || proj is EntitySpellProjectile) return;

            var wa = attacker.WatchedAttributes;
            long until = wa.GetLong(AttrKeys.ShotUntilMs, 0);
            if (until == 0 || attacker.World.ElapsedMilliseconds > until) return;
            if (SpellExecutor.IsAlly(attacker, victim)) return;

            float bonus = wa.GetFloat(AttrKeys.ShotDamage, 0f);
            if (bonus > 0f) damage += bonus;

            string sid = wa.GetString(AttrKeys.ShotStatusId, "");
            if (!string.IsNullOrEmpty(sid))
                SpellExecutor.ApplyEffect(victim, sid, Math.Max(1, wa.GetInt(AttrKeys.ShotStatusAmp, 1)), wa.GetFloat(AttrKeys.ShotStatusDur, 5f));

            int grant = wa.GetInt(AttrKeys.ShotComboGrant, 0);
            if (grant > 0) ResourceState.AddCombo(attacker, grant);

            // WoundedEffect owns the heal cut itself; ShotHealCut only gates it. Ignite is the vanilla burn.
            float healCut = wa.GetFloat(AttrKeys.ShotHealCut, 0f);
            if (healCut > 0f)
                SpellExecutor.ApplyEffect(victim!, Core.Effects.WoundedEffectId.Id, 1, wa.GetFloat(AttrKeys.ShotHealCutSecs, 5f));

            float drain = wa.GetFloat(AttrKeys.ShotDrain, 0f);
            if (drain > 0f)
                ResourceState.Spend(victim, ResourceState.PrimaryPool(victim!), drain);

            if (wa.GetBool(AttrKeys.ShotIgnite, false))
                victim!.IsOnFire = true;

            ClearArmedDamage(attacker);
        }

        public static bool HasArmedShot(Entity e)
        {
            long until = e.WatchedAttributes.GetLong(AttrKeys.ShotUntilMs, 0);
            return until != 0 && e.World.ElapsedMilliseconds <= until;
        }

        // ItemBow's hardcoded time to full draw.
        private const float BowFullDrawSeconds = 0.75f;

        /// <summary>On bow release: a shot that needs a full draw is discarded unless the draw started after the
        /// ability was pressed (no pre-drawing) and was held long enough.</summary>

        public static void ValidateDrawOnRelease(Entity shooter, float secondsUsed)
        {
            if (!HasArmedShot(shooter)) return;
            var wa = shooter.WatchedAttributes;
            float fraction = wa.GetFloat(AttrKeys.ShotFullDraw, 0f);
            if (fraction <= 0f) return;

            float drawSpeed = shooter.Stats.GetBlended(StatKeys.BowDrawSpeed);
            float fullDrawSecs = BowFullDrawSeconds / (drawSpeed > 0.1f ? drawSpeed : 1f);
            float need = fraction * fullDrawSecs;

            long drawStartMs = shooter.World.ElapsedMilliseconds - (long)(secondsUsed * 1000f);
            bool freshDraw = drawStartMs >= wa.GetLong(AttrKeys.ShotArmedMs, 0);
            bool drawnEnough = secondsUsed >= need;
            if (!freshDraw || !drawnEnough) ClearArmedDamage(shooter);
        }

        /// <summary>Takes the armed extra-arrow count; the damage bundle still waits for the hit.</summary>
        public static int TakeExtraArrows(Entity e)
        {
            if (!HasArmedShot(e)) return 0;
            int extra = e.WatchedAttributes.GetInt(AttrKeys.ShotExtra, 0);
            if (extra > 0) e.WatchedAttributes.SetInt(AttrKeys.ShotExtra, 0);
            return extra;
        }

        // Leaves the extra arrows alone; TakeExtraArrows consumes those on fire.
        private static void ClearArmedDamage(Entity e)
        {
            var wa = e.WatchedAttributes;
            string spellId = wa.GetString(AttrKeys.ShotSpell, "");
            wa.SetFloat(AttrKeys.ShotDamage, 0f);
            wa.SetInt(AttrKeys.ShotComboGrant, 0);
            wa.SetString(AttrKeys.ShotStatusId, "");
            wa.SetLong(AttrKeys.ShotUntilMs, 0);

            if (!string.IsNullOrEmpty(spellId))
            {
                int i = spellId.IndexOf(':');
                string local = i >= 0 ? spellId.Substring(i + 1) : spellId;
                if (e.GetBehavior<EBEffects>() is { } eff && eff.TryGetEffect(EmpowerEffectIds.For(local), out var buff))
                    buff.SetExpiryImmediately();
            }
        }
    }
}
