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
    /// <summary>
    /// Hunter "shot" abilities arm a bonus on the caster instead of spawning a projectile; the next real
    /// arrow that lands consumes it. Split Shot's extra arrows are spawned by BowPatches on bow release.
    /// </summary>
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

            // Bonus damage snapshot: coeff + per-combo-point (finisher combo captured before delivery) × ranged
            // spell power. Same shape as FinisherAmount, but banked onto the next arrow instead of dealt now.
            float coeff = imp.DamageSpellPowerCoefficient + imp.DamagePerComboPoint * ctx.ComboPoints;
            wa.SetFloat(AttrKeys.ShotDamage, ctx.SpellPower * coeff);
            wa.SetFloat(AttrKeys.ShotKnockback, imp.Knockback);
            // Builders grant their combo when the arrow HITS (a miss builds nothing); finishers already spent it.
            wa.SetInt(AttrKeys.ShotComboGrant, ctx.Spell.ComboBuilder ? ctx.Spell.ComboPointsGenerated : 0);
            wa.SetString(AttrKeys.ShotStatusId, imp.StatusEffectId ?? "");
            wa.SetFloat(AttrKeys.ShotStatusDur, imp.StatusEffectDuration);
            wa.SetInt(AttrKeys.ShotStatusAmp, imp.StatusEffectAmplifier);
            wa.SetString(AttrKeys.ShotSpell, ctx.Spell.Id);
            // Bank Shot augments a shot that already fires a fan (Split Shot) with extra arrows.
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

            // Cosmetic HUD buff with a countdown of the window (per-spell icon), + a puff so it reads as "armed".
            SpellExecutor.ApplyEffect(e, EmpowerEffectIds.For(ctx.Spell.LocalId), 1, window);
            imp.Particles?.Emit(e.World, e);
        }

        private static void ConsumeOnArrowHit(Entity victim, Entity? attacker, DamageSource source, ref float damage)
        {
            if (damage <= 0f || attacker == null || victim == attacker) return;
            if (victim?.World?.Side != EnumAppSide.Server) return;
            // Only a real vanilla arrow the attacker fired (an EntityProjectile that isn't one of our spell shots).
            if (source.SourceEntity is not EntityProjectile proj || proj is EntitySpellProjectile) return;

            var wa = attacker.WatchedAttributes;
            long until = wa.GetLong(AttrKeys.ShotUntilMs, 0);
            if (until == 0 || attacker.World.ElapsedMilliseconds > until) return; // nothing armed / window lapsed
            if (SpellExecutor.IsAlly(attacker, victim)) return;

            float bonus = wa.GetFloat(AttrKeys.ShotDamage, 0f);
            if (bonus > 0f) damage += bonus;

            string sid = wa.GetString(AttrKeys.ShotStatusId, "");
            if (!string.IsNullOrEmpty(sid))
                SpellExecutor.ApplyEffect(victim, sid, Math.Max(1, wa.GetInt(AttrKeys.ShotStatusAmp, 1)), wa.GetFloat(AttrKeys.ShotStatusDur, 5f));

            int grant = wa.GetInt(AttrKeys.ShotComboGrant, 0);
            if (grant > 0) ResourceState.AddCombo(attacker, grant);

            // WoundedEffect owns the heal-cut and its HUD icon; ShotHealCut only gates it and ShotHealCutSecs
            // drives the duration. Ignite is the vanilla burn (~0.5 dmg/s until it goes out).
            float healCut = wa.GetFloat(AttrKeys.ShotHealCut, 0f);
            if (healCut > 0f)
                SpellExecutor.ApplyEffect(victim!, Core.Effects.WoundedEffectId.Id, 1, wa.GetFloat(AttrKeys.ShotHealCutSecs, 5f));

            float drain = wa.GetFloat(AttrKeys.ShotDrain, 0f);
            if (drain > 0f)
                ResourceState.Spend(victim, ResourceState.PrimaryPool(victim!), drain);

            if (wa.GetBool(AttrKeys.ShotIgnite, false))
                victim!.IsOnFire = true;

            ClearArmedDamage(attacker); // the empowered shot is spent (extra arrows are cleared separately on fire)
        }

        public static bool HasArmedShot(Entity e)
        {
            long until = e.WatchedAttributes.GetLong(AttrKeys.ShotUntilMs, 0);
            return until != 0 && e.World.ElapsedMilliseconds <= until;
        }

        /// <summary>Called on bow release (BowPatches): if the armed shot demands a fresh full draw (aimed_shot),
        /// discard the empowered bundle unless this shot qualifies - held long enough and its draw STARTED after
        /// the ability was pressed (so you can't pre-draw the bow and then press aimed_shot for a free big hit).
        /// A discarded aimed shot fires as a plain arrow.</summary>
        // Vanilla bow: renderVariant reaches full (max) by ~this many seconds of draw (ItemBow's hardcoded
        // timing). A future "faster draw" talent would set StatKeys.BowDrawSpeed (1.0-based) to shorten it.
        private const float BowFullDrawSeconds = 0.75f;

        public static void ValidateDrawOnRelease(Entity shooter, float secondsUsed)
        {
            if (!HasArmedShot(shooter)) return;
            var wa = shooter.WatchedAttributes;
            float fraction = wa.GetFloat(AttrKeys.ShotFullDraw, 0f);
            if (fraction <= 0f) return; // this shot has no draw requirement (steady/kill/serpent/concussive)

            // Required hold = the demanded fraction of a full draw, scaled by the bow's draw speed (so a faster
            // bow reaches the same fraction sooner). Expressed as a fraction, not a fixed time.
            float drawSpeed = shooter.Stats.GetBlended(StatKeys.BowDrawSpeed);
            float fullDrawSecs = BowFullDrawSeconds / (drawSpeed > 0.1f ? drawSpeed : 1f);
            float need = fraction * fullDrawSecs;

            long drawStartMs = shooter.World.ElapsedMilliseconds - (long)(secondsUsed * 1000f);
            bool freshDraw = drawStartMs >= wa.GetLong(AttrKeys.ShotArmedMs, 0);
            bool drawnEnough = secondsUsed >= need;
            if (!freshDraw || !drawnEnough) ClearArmedDamage(shooter);
        }

        /// <summary>Extra arrows the current armed shot fires (Split Shot), or 0. Consuming it clears only the
        /// extra-arrow flag - the damage/status bundle still lands on the arrow's hit.</summary>
        public static int TakeExtraArrows(Entity e)
        {
            if (!HasArmedShot(e)) return 0;
            int extra = e.WatchedAttributes.GetInt(AttrKeys.ShotExtra, 0);
            if (extra > 0) e.WatchedAttributes.SetInt(AttrKeys.ShotExtra, 0);
            return extra;
        }

        // Clears the damage/status/combo bundle and closes the window + strips the HUD buff. Leaves the extra
        // arrows to their own consumption on fire.
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
