using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using effectshud.src;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Integration;
using canrpgclasses.Core.Spells;

using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Shaman
{
    /// <summary>Effect ids for the shaman. Aura-prefixed ids are driven by <see cref="Core.EB.EBAuras"/> (toggles);
    /// the rest are ordinary timed effectshud effects applied by the spell pipeline.</summary>
    public static class ShamanEffectIds
    {
        public const string SpiritWolf = AuraEffectIds.Prefix + "spirit_wolf"; // toggled travel form (model swap + speed)

        public const string StaticShield = "canrpg_static_shield"; // reactive charges: shocks melee attackers

        public const string EmberShock = "canrpg_ember_shock";             // DoT (Fire); Magma Burst hits it harder
        public const string ElementalSurge = "canrpg_elemental_surge"; // timed +nature/+fire spell power
        public const string TotemEarth = "canrpg_totem_earth";             // Stone Strength Totem's pulse buff

        public const string ThunderCleaveArmed = "canrpg_stormstrike_armed"; // brief: stamps the marker on the next hit
        public const string ThunderCleave = "canrpg_thunder_cleave";            // victim marker: your lightning hits it harder
        public const string Maelstrom = "canrpg_maelstrom";                // Enhancement ramp: stacks cut cast times
        public const string WarChant = "canrpg_war_chant";                // party burst: +melee damage, +speed
    }

    /// <summary>Doubles as the marker Magma Burst looks for through its DamageVsControlEffectId.</summary>
    [EffectRegistration(ShamanEffectIds.EmberShock, positive: false)]
    public class EmberShockEffect : SchoolDamageDotEffect
    {
        private readonly float coeff;
        public EmberShockEffect() { coeff = BalanceConfig.Spell("ember_shock").F("coeffPerTick", 0.25f); }
        protected override SpellSchool School => SpellSchool.Fire;
        protected override float CoeffPerTick => coeff;
    }

    [EffectRegistration(ShamanEffectIds.ElementalSurge)]
    public class ElementalSurgeEffect : Effect
    {
        private readonly float sp;
        public ElementalSurgeEffect() { sp = BalanceConfig.Spell("elemental_surge").F("spellPower", 0.30f); }
        public override void OnStart()
        {
            entity.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Nature), ShamanEffectIds.ElementalSurge, sp);
            entity.Stats.Set(StatKeys.SpellpowerFor(SpellSchool.Fire), ShamanEffectIds.ElementalSurge, sp);
        }
        public override void OnExpire()
        {
            entity.Stats.Remove(StatKeys.SpellpowerFor(SpellSchool.Nature), ShamanEffectIds.ElementalSurge);
            entity.Stats.Remove(StatKeys.SpellpowerFor(SpellSchool.Fire), ShamanEffectIds.ElementalSurge);
        }
    }

    /// <summary>Re-applied every pulse by <see cref="EBShamanTotem"/>. Its duration deliberately outlives one pulse
    /// but not many, so walking out of range drops it by itself.</summary>
    [EffectRegistration(ShamanEffectIds.TotemEarth)]
    public class TotemEarthEffect : Effect
    {
        private readonly float value;
        public TotemEarthEffect() { value = BalanceConfig.Global("totemEarthMeleeBonus", 0.10f); }
        public override float DisplayMagnitude() => value;
        public override void OnStart() => entity.Stats.Set(StatKeys.MeleeWeaponsDamage, ShamanEffectIds.TotemEarth, value);
        public override void OnExpire() => entity.Stats.Remove(StatKeys.MeleeWeaponsDamage, ShamanEffectIds.TotemEarth);
    }

    /// <summary>
    /// Static Shield: each melee hit taken shocks the attacker and burns a charge (charges = effect tier,
    /// shown as HUD stacks). Never ticks - the snapshot only bakes the per-proc damage.
    /// </summary>
    [EffectRegistration(ShamanEffectIds.StaticShield)]
    public class StaticShieldEffect : SnapshotEffect
    {
        /// <summary>Baked damage per proc. Public so it survives relog (OnStart doesn't re-run on deserialize).</summary>
        public float perProc;

        private readonly float coeff;
        public StaticShieldEffect() { coeff = BalanceConfig.Spell("static_shield").F("coeff", 0.3f); }

        // Improved Shields (Restoration talent) scales the proc; read from the caster (self for Static Shield,
        // the healer for Stone Ward) at apply time, like every other snapshot.
        protected override void Recompute()
            => perProc = casterSpellPower * coeff * (1f + (Caster ?? entity).ReductionStat(ShamanStatKeys.ShieldPower));

        public override float DisplayMagnitude() => perProc;

        // Only real incoming melee swings charge the shield: never a heal, never our own spell/proc damage
        // (CanrpgDamageSource - so two shielded shamans can't ping-pong, and DoT ticks don't strip charges).
        public override void OnShouldEntityReceiveDamage(ref float damage, DamageSource dmgSource)
        {
            if (perProc <= 0f || damage <= 0f || dmgSource == null || dmgSource.Type == EnumDamageType.Heal) return;
            if (dmgSource is CanrpgDamageSource) return;
            if (dmgSource.Source != EnumDamageSource.Player && dmgSource.Source != EnumDamageSource.Entity) return;
            var attacker = dmgSource.CauseEntity ?? dmgSource.SourceEntity;
            if (attacker == null || attacker == entity || !attacker.Alive) return;

            Core.Visuals.SkillFx.Arc(entity.World, entity, attacker, SpellSchool.Nature);
            attacker.ReceiveDamage(new CanrpgDamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = entity,
                CauseEntity = entity,
                School = SpellSchool.Nature,
                Type = DamageSchools.For(SpellSchool.Nature).EngineType
            }, perProc);

            Tier--;
            var beh = entity?.GetBehavior<EBEffects>();
            if (beh != null) beh.needUpdate = true; // push the new charge count to the HUD
            if (Tier <= 0) SetExpiryImmediately();
        }
    }

    /// <summary>
    /// Spirit Wolf: travel-form toggle aura (speed + wolf model). Declares <see cref="IModelSwapEffect"/>
    /// so an incoming transmute cancels the form instead of silently failing.
    /// </summary>
    [EffectRegistration(ShamanEffectIds.SpiritWolf)]
    public class SpiritWolfAuraEffect : AuraEffectBase, IModelSwapEffect
    {
        /// <summary>Registered by the PlayerModelLib content asset config/customplayermodels/canrpgwolf.json.
        /// Domain-prefixed for the same reason as the sheep (see PlayerModelSwap.TransmuteModelCode).</summary>
        public const string ModelCode = "canrpgclasses:canrpgwolf";

        public float value;
        public SpiritWolfAuraEffect() { value = BalanceConfig.Spell("spirit_wolf").F("walkSpeed", 0.30f); }

        public override void OnStart()
        {
            base.OnStart();
            Assert();
        }

        // Re-assert every tick: OnStart doesn't run on relog or on a colliding re-apply (that lands as OnStack), and
        // PlayerModelLib can reset skinModel to the base model on join - any of which would silently drop the form.
        // Every step here is idempotent.
        public override void OnTick()
        {
            base.OnTick();
            Assert();
        }

        private void Assert()
        {
            entity.Stats.Set(StatKeys.WalkSpeed, ShamanEffectIds.SpiritWolf, value);
            entity.WatchedAttributes.SetBool(CombatFlags.FormActionLock, true); // a wolf can't swing or cast
            PlayerModelSwap.EnsureModel(entity as EntityPlayer, ModelCode);
        }

        public override void OnExpire()
        {
            base.OnExpire();
            Undo();
        }

        public override bool OnDeath()
        {
            Undo(); // never leave a corpse (or the respawned player) stuck as a wolf
            return base.OnDeath();
        }

        public void CancelSwap()
        {
            Undo();
            SetExpiryImmediately(); // the aura toggle is cleared by the caller; this drops the buff itself
        }

        private void Undo()
        {
            entity?.Stats.Remove(StatKeys.WalkSpeed, ShamanEffectIds.SpiritWolf);
            entity?.WatchedAttributes.SetBool(CombatFlags.FormActionLock, false);
            PlayerModelSwap.Restore(entity as EntityPlayer);
        }
    }
}
