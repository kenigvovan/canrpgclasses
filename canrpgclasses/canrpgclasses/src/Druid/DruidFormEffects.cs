using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Integration;
using canrpgclasses.Core.Spells;
using effectshud.src;

namespace canrpgclasses.Druid
{
    /// <summary>Effect / spell ids for the druid. Form effect ids carry the <see cref="AuraEffectIds.Prefix"/> so the
    /// toggle-aura system (EBAuras) and the relog-safe handling recognise them as auras.</summary>
    public static class DruidEffectIds
    {
        public const string FelineForm = AuraEffectIds.Prefix + "feline_form";
        public const string UrsineForm = AuraEffectIds.Prefix + "ursine_form";

        // The spell ids as stored in ActiveAura (= what a Spell.RequiresForm gate compares against).
        public const string FelineFormSpellId = "canrpgclasses:feline_form";
        public const string UrsineFormSpellId = "canrpgclasses:ursine_form";

        // Feral bleeds / dots.
        public const string ClawSlash = "canrpg_druid_claw_slash"; // Cat bleed (builder)
        public const string DeepRend = "canrpg_druid_deep_rend";   // Cat bleed finisher (combo-scaled)
        public const string Gash = "canrpg_druid_gash";            // Bear bleed
        public const string LunarFlare = "canrpg_druid_lunar_flare";  // Balance dot
        public const string StingingSwarm = "canrpg_druid_insectswarm"; // Balance dot #2

        // Restoration extras.
        public const string LivingBlossom = "canrpg_druid_living_blossom";         // fast strong HoT
        public const string Quickening = "canrpg_druid_quickening"; // cosmetic marker: next cast is instant

        // Defensive cooldown (usable in any form) - flat damage reduction while active.
        public const string BarkHide = "canrpg_druid_bark_hide";

        // Balance proc marker: next cast-time spell is instant (Falling Stars talent).
        public const string FallingStars = "canrpg_druid_falling_stars";

        // Restoration heal-over-time effects.
        public const string VerdantRenewal = "canrpg_druid_rejuv";
        public const string RestorativeBloom = "canrpg_druid_restorative_bloom";
        public const string SpreadingBloom = "canrpg_druid_wildgrowth";

        /// <summary>Every Restoration heal-over-time, in the order Instant Mend consumes them - it eats the FIRST one
        /// present. Ordered by how cheaply the druid can put it back, so cashing one in never silently costs the
        /// expensive cast.</summary>
        public static readonly string[] HealOverTimeIds =
        {
            VerdantRenewal, LivingBlossom, RestorativeBloom, SpreadingBloom
        };
    }

    /// <summary>Base for a druid shapeshift form: a self-only toggled aura that swaps the rendered model (via the
    /// generic <see cref="PlayerModelSwap"/>) and applies a stat profile while active. Re-asserts every tick so the
    /// form survives relog, the same pattern Spirit Wolf uses. Cat and Bear also raise
    /// <see cref="CombatFlags.FormActionLock"/> to block out-of-form spells while shifted; Moonkin, being a caster
    /// form, leaves it clear. Declares <see cref="IModelSwapEffect"/> so a transmute cancels the form first.</summary>
    public abstract class DruidFormEffectBase : AuraEffectBase, IModelSwapEffect
    {
        protected abstract string ModelCode { get; }
        protected virtual bool LocksActions => true;

        public override void OnStart() { base.OnStart(); Assert(); }
        public override void OnTick() { base.OnTick(); Assert(); }
        public override void OnExpire() { base.OnExpire(); Undo(); }
        public override bool OnDeath() { Undo(); return base.OnDeath(); }

        /// <summary>Called when something else claims the model-swap slot: drop the form cleanly.</summary>
        public void CancelSwap() { Undo(); SetExpiryImmediately(); }

        private void Assert()
        {
            if (LocksActions) entity.WatchedAttributes.SetBool(CombatFlags.FormActionLock, true);
            ApplyStats();
            // Relog is handled centrally by PlayerModelSwap.ScheduleReapplyOnJoin; this just keeps asserting.
            PlayerModelSwap.EnsureModel(entity as EntityPlayer, ModelCode);
        }

        private void Undo()
        {
            if (LocksActions) entity?.WatchedAttributes.SetBool(CombatFlags.FormActionLock, false);
            RemoveStats();
            PlayerModelSwap.Restore(entity as EntityPlayer);
            OnUndo();
        }

        /// <summary>Set the form's stat profile (idempotent - re-run every tick).</summary>
        protected abstract void ApplyStats();
        protected abstract void RemoveStats();
        protected virtual void OnUndo() { }
    }

    /// <summary>Gates the Cat combo abilities through their RequiresForm, and locks normal casting.</summary>
    [EffectRegistration(DruidEffectIds.FelineForm)]
    public class FelineFormEffect : DruidFormEffectBase
    {
        protected override string ModelCode => "canrpgclasses:canrpgcat";
        private readonly float speed;
        public FelineFormEffect() { speed = BalanceConfig.Spell("feline_form").F("walkSpeed", 0.20f); }

        protected override void ApplyStats() => entity.Stats.Set(StatKeys.WalkSpeed, DruidEffectIds.FelineForm, speed);
        protected override void RemoveStats() => entity?.Stats.Remove(StatKeys.WalkSpeed, DruidEffectIds.FelineForm);

        /// <summary>Leaving Feline Form drops Stalk's stealth: the invis window is caster-level (not form-bound), so
        /// without this you'd keep sneaking as a visible seraph / caster - break it on unshift.</summary>
        protected override void OnUndo()
        {
            if (entity?.Api?.Side == EnumAppSide.Server) SpellExecutor.BreakCasterInvisibility(entity);
        }
    }

    /// <summary>Rage is generated only while this form is up (see <see cref="DruidRage"/>). Locks normal casting.</summary>
    [EffectRegistration(DruidEffectIds.UrsineForm)]
    public class UrsineFormEffect : DruidFormEffectBase
    {
        protected override string ModelCode => "canrpgclasses:canrpgbear";
        private readonly float hp, dr;
        public UrsineFormEffect()
        {
            var b = BalanceConfig.Spell("ursine_form");
            hp = b.F("bonusHealth", 8f);
            dr = b.F("damageReduction", 0.15f);
        }

        protected override void ApplyStats()
        {
            entity.Stats.Set(StatKeys.MaxHealthExtraPoints, DruidEffectIds.UrsineForm, hp);
            // Tough Hide is amplified in Ursine Form: the talent grants its DR everywhere (its own ApplyStats), and this
            // adds an equal bonus while shifted - so the tank's Tough Hide is effectively doubled in bear.
            int thRank = canrpgclasses.Core.Talents.TalentState.Rank(entity, "druid:tough_hide");
            float thBonus = thRank * BalanceConfig.Talent("druid:tough_hide").F("perRank", 0.03f);
            entity.Stats.Set(StatKeys.DamageReduction, DruidEffectIds.UrsineForm, dr + thBonus);
        }
        protected override void RemoveStats()
        {
            entity?.Stats.Remove(StatKeys.MaxHealthExtraPoints, DruidEffectIds.UrsineForm);
            entity?.Stats.Remove(StatKeys.DamageReduction, DruidEffectIds.UrsineForm);
        }
    }
}
