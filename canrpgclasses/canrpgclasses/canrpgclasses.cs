using HarmonyLib;
using canrpgclasses.Client;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.HarmonyPatches;
using canrpgclasses.Core.Progression;
using canrpgclasses.Core.Entities;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Items;
using canrpgclasses.Core.Net;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace canrpgclasses
{
    /// <summary>
    /// Entry point of the mod: the spell and active-skill framework plus the class content. Holds the sided API
    /// references, the registries and the network channel. Partial class - the network handlers, chat commands
    /// and client GUI management live in canrpgclasses.Network.cs, .Commands.cs and .ClientGui.cs.
    /// </summary>
    public partial class canrpgclassesModSystem : ModSystem
    {
        public const string ModId = "canrpgclasses";
        public const string ChannelName = "canrpgclasses";

        // Sided singletons: in singleplayer both sides run in this process, each with its own ModSystem
        // instance, so a single static Instance was overwritten by whichever side started last - the server
        // could end up using the client's instance (whose ServerChannel is null). Resolve via For(api).
        public static canrpgclassesModSystem? ClientInstance { get; private set; }
        public static canrpgclassesModSystem? ServerInstance { get; private set; }
        public static ICoreClientAPI? ClientApi { get; private set; }
        public static ICoreServerAPI? ServerApi { get; private set; }

        /// <summary>The mod system owned by the given API's side. O(1) - unlike ModLoader.GetModSystem,
        /// which linearly scans all mod systems per call - so hot paths (per-hit Harmony patches, per-tick
        /// resource reads, per-swing triggers) resolve through this.</summary>
        public static canrpgclassesModSystem? For(ICoreAPI? api)
            => api == null ? null : api.Side == EnumAppSide.Server ? ServerInstance : ClientInstance;

        public ICoreAPI Api { get; private set; } = null!;

        /// <summary>Every spell: discovered by <see cref="SpellRegistrationAttribute"/> across the loaded
        /// assemblies, then whatever the JSON content files add on top.</summary>
        public SpellRegistry Spells { get; } = new SpellRegistry();

        /// <summary>Playable classes and their talent definitions, from the same two sources.</summary>
        public RpgClassRegistry Classes { get; } = new RpgClassRegistry();
        public TalentRegistry Talents { get; } = new TalentRegistry();

        /// <summary>Problems found the last time the JSON content files were read. Surfaced by the reload
        /// command so a content author sees them in chat, not only in the log.</summary>
        public Core.Content.ContentReport? LastContentReport { get; private set; }

        internal IServerNetworkChannel? ServerChannel;
        internal IClientNetworkChannel? ClientChannel;

        // Id of the PushPartyResources tick listener, so Dispose can unregister it.
        private long partyResourceTickId;

        // Harmony for the stun enforcement patches. Static + guarded so the process is patched once
        // (in singleplayer Start runs on both the client and server side in the same process).
        private const string HarmonyId = "canrpgclasses";
        private static Harmony? harmony;

        // Called on server and client.
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            if (api.Side == EnumAppSide.Server) ServerInstance = this; else ClientInstance = this;
            Api = api;

            api.RegisterEntityBehaviorClass(EBSpellCaster.Name, typeof(EBSpellCaster));
            api.RegisterEntityBehaviorClass(EBSpellCooldowns.Name, typeof(EBSpellCooldowns));
            api.RegisterEntityBehaviorClass(EBRpgStats.Name, typeof(EBRpgStats));
            api.RegisterEntityBehaviorClass(EBStun.Name, typeof(EBStun));
            api.RegisterEntityBehaviorClass(EBCombatState.Name, typeof(EBCombatState));
            api.RegisterEntityBehaviorClass(EBAuras.Name, typeof(EBAuras));
            api.RegisterEntityBehaviorClass(EBProgression.Name, typeof(EBProgression));
            api.RegisterEntityBehaviorClass(EBTalents.Name, typeof(EBTalents));
            api.RegisterEntityBehaviorClass(EBResources.Name, typeof(EBResources));
            api.RegisterEntityBehaviorClass(EBGearAffinity.Name, typeof(EBGearAffinity));
            api.RegisterEntityBehaviorClass(Hunter.EBHunterPet.Name, typeof(Hunter.EBHunterPet));
            api.RegisterEntityBehaviorClass(Shaman.EBShamanTotem.Name, typeof(Shaman.EBShamanTotem));
            api.RegisterEntityBehaviorClass(Shaman.EBShamanTotemVisual.Name, typeof(Shaman.EBShamanTotemVisual));
            // Command-mode-gated variants of the wolf's seek/melee tasks (respect its aggressive/defensive/passive order).
            Vintagestory.GameContent.AiTaskRegistry.Register<Hunter.AiTaskPetSeek>("petseek");
            Vintagestory.GameContent.AiTaskRegistry.Register<Hunter.AiTaskPetMelee>("petmelee");
            api.RegisterEntity("EntitySpellProjectile", typeof(EntitySpellProjectile));
            api.RegisterCollectibleBehaviorClass(BehaviorSpellContainer.Name, typeof(BehaviorSpellContainer));

            PatchStun();

            // Custom aura effects in effectshud (own type id + own stat key → no clobber; applied infinite → "∞").
            RegisterAuraEffects();

            Mod.Logger.Notification("[canrpgclasses] loaded ({0})", api.Side);
        }

        // Registers the paladin aura effects into effectshud (both sides). Icon falls back to a placeholder until
        // PNG art exists at canrpgclasses:textures/effects/<id>.png.
        private static void RegisterAuraEffects()
        {
            void Reg(string id, System.Type type, string icon) =>
                effectshud.src.effectshud.RegisterEffect(id, type, positive: true, shouldBeRendered: true,
                    icon: new AssetLocation(icon)); // effectshud HUD now rasterizes .svg, so we reuse our own SVG icons

            // Crowd-control HUD markers (cosmetic, negative) so a controlled player sees a countdown. Applied/cleared
            // by ControlState; the control logic itself is separate.
            void RegCc(string id, System.Type type, string icon) =>
                effectshud.src.effectshud.RegisterEffect(id, type, positive: false, shouldBeRendered: true,
                    icon: new AssetLocation(icon));
            RegCc(Core.Effects.ControlEffectIds.Stunned, typeof(Core.Effects.CcStunnedEffect), "canrpgclasses:textures/icons/knockout.svg");
            RegCc(Core.Effects.ControlEffectIds.Feared, typeof(Core.Effects.CcFearedEffect), "canrpgclasses:textures/icons/screaming.svg");
            RegCc(Core.Effects.ControlEffectIds.Silenced, typeof(Core.Effects.CcSilencedEffect), "canrpgclasses:textures/icons/mute.svg");
            RegCc(Core.Effects.ControlEffectIds.Rooted, typeof(Core.Effects.CcRootedEffect), "canrpgclasses:textures/icons/leg-armor.svg");
            RegCc(Core.Effects.ControlEffectIds.Polymorphed, typeof(Core.Effects.CcPolymorphedEffect), "canrpgclasses:textures/icons/sheep.svg");

            Reg(Core.Effects.AuraEffectIds.Might, typeof(Core.Effects.AuraMightEffect), "canrpgclasses:textures/icons/enrage.svg");
            Reg(Core.Effects.AuraEffectIds.Haste, typeof(Core.Effects.AuraHasteEffect), "canrpgclasses:textures/icons/cloaked-figure-on-horseback.svg");
            Reg(Core.Effects.AuraEffectIds.Regen, typeof(Core.Effects.AuraRegenEffect), "canrpgclasses:textures/icons/aura.svg");

            // Cosmetic "empowered next strike" buffs: a HUD countdown of the empower window, one per arming spell
            // so each shows its own icon. No stats - the bonus itself lives in WatchedAttributes.
            Reg(Core.Effects.EmpowerEffectIds.ZealotsStrike, typeof(Core.Effects.ZealotsStrikeEmpowerEffect), "canrpgclasses:textures/icons/shining-sword.svg");
            Reg(Core.Effects.EmpowerEffectIds.ViciousStrike, typeof(Core.Effects.ViciousStrikeEmpowerEffect), "canrpgclasses:textures/icons/scalpel-strike.svg");
            Reg(Core.Effects.EmpowerEffectIds.Prefix + "brutal_strike", typeof(Core.Effects.BrutalStrikeEmpowerEffect), "canrpgclasses:textures/icons/blade-fall.svg");
            Reg(Core.Effects.EmpowerEffectIds.Prefix + "death_blow", typeof(Core.Effects.DeathBlowEmpowerEffect), "canrpgclasses:textures/icons/chopped-skull.svg");

            // Quickblades stacks: dedicated id/stat-key so per-hit ConsumeStack never drains shared strengthmelee buffs.
            Reg(Core.Effects.QuickbladesEffectIds.Quickblades, typeof(Core.Effects.QuickbladesEffect), "canrpgclasses:textures/icons/sacrificial-dagger.svg");

            // Hunter "next bow shot empowered" HUD buffs (cosmetic countdown; bonus lives in WatchedAttributes).
            Reg(Core.Effects.EmpowerEffectIds.Prefix + "measured_shot", typeof(Core.Effects.MeasuredShotEmpowerEffect), "canrpgclasses:textures/icons/air-zigzag.svg");
            Reg(Core.Effects.EmpowerEffectIds.Prefix + "finishing_shot", typeof(Core.Effects.FinishingShotEmpowerEffect), "canrpgclasses:textures/icons/pierced-body.svg");
            Reg(Core.Effects.EmpowerEffectIds.Prefix + "careful_shot", typeof(Core.Effects.CarefulShotEmpowerEffect), "canrpgclasses:textures/icons/deadly-strike.svg");
            Reg(Core.Effects.EmpowerEffectIds.Prefix + "split_shot", typeof(Core.Effects.SplitShotEmpowerEffect), "canrpgclasses:textures/icons/sword-array.svg");
            Reg(Core.Effects.EmpowerEffectIds.Prefix + "venom_sting", typeof(Core.Effects.VenomStingEmpowerEffect), "canrpgclasses:textures/icons/burning-dot.svg");
            Reg(Core.Effects.EmpowerEffectIds.Prefix + "jarring_shot", typeof(Core.Effects.JarringShotEmpowerEffect), "canrpgclasses:textures/icons/knockout.svg");

            // Cosmetic HUD buff for Evasion (icon + countdown); the dodge roll itself stays a WatchedAttributes flag.
            Reg(Core.Effects.EvasionEffectId.Id, typeof(Core.Effects.EvasionEffect), "canrpgclasses:textures/icons/jump-across.svg");

            // Hunter Concealment: concealment (lowered animalSeekingRange), not invisibility.
            Reg(Hunter.ConcealmentEffectId.Id, typeof(Hunter.ConcealmentEffect), "canrpgclasses:textures/icons/hood.svg");

            // Warrior Stances: self-only toggled combat modes (applied infinite by EBAuras, one active at a time).
            Reg(Warrior.StanceEffectIds.Battle, typeof(Warrior.OffensiveStanceEffect), "canrpgclasses:textures/icons/battle-gear.svg");
            Reg(Warrior.StanceEffectIds.Defense, typeof(Warrior.GuardedStanceEffect), "canrpgclasses:textures/icons/cross-shield.svg");
            Reg(Warrior.StanceEffectIds.Berserk, typeof(Warrior.RecklessStanceEffect), "canrpgclasses:textures/icons/enrage.svg");

            // Warrior Enrage: a short melee-damage + haste proc buff (Fury's Enrage talent).
            Reg(Warrior.EnrageEffectId.Id, typeof(Warrior.EnrageEffect), "canrpgclasses:textures/icons/enrage.svg");
            // Warrior Full Guard: a timed heavy damage-reduction self-buff.
            Reg(Warrior.FullGuardEffectId.Id, typeof(Warrior.FullGuardEffect), "canrpgclasses:textures/icons/diamond-hard.svg");
            // Warrior Shield Guard: the short-cooldown active-mitigation damage-reduction buff.
            Reg(Warrior.ShieldGuardEffectId.Id, typeof(Warrior.ShieldGuardEffect), "canrpgclasses:textures/icons/cross-shield.svg");

            // Mortal wound (wounding_shot): a debuff (positive:false), so registered directly - Reg hardcodes positive:true.
            // Carries the heal-cut logic itself (WoundedEffect.OnShouldEntityReceiveDamage); shows an icon on the victim.
            effectshud.src.effectshud.RegisterEffect(Core.Effects.WoundedEffectId.Id, typeof(Core.Effects.WoundedEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/ragged-wound.svg"));

            // Warrior Mortal Wound (mortal_strike): a heal-cut debuff (positive:false), same as WoundedEffect but its
            // own id/config so the two anti-heal debuffs balance independently.
            effectshud.src.effectshud.RegisterEffect(Warrior.MortalWoundEffectId.Id, typeof(Warrior.MortalWoundEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/swordwoman.svg"));

            // Warrior Armor Break: a stacking vulnerability debuff (positive:false) that raises the bearer's incoming damage.
            effectshud.src.effectshud.RegisterEffect(Warrior.ArmorBreakEffectId.Id, typeof(Warrior.ArmorBreakEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/shoulder-armor.svg"));

            // Priest Soothing Prayer: a spell-power-scaled heal-over-time (snapshot at cast). Positive HoT buff.
            Reg(Priest.PriestEffectIds.SoothingPrayer, typeof(Priest.SoothingPrayerEffect), "canrpgclasses:textures/icons/bandaged.svg");
            // Priest Sheltering Spirit: a timed +incoming-healing buff on an ally.
            Reg(Priest.PriestEffectIds.ShelteringSpirit, typeof(Priest.ShelteringSpiritEffect), "canrpgclasses:textures/icons/angel-wings.svg");

            // Priest Sacred Flame: a spell-power-scaled Holy DoT (snapshot at cast) - a debuff on the victim.
            effectshud.src.effectshud.RegisterEffect(Priest.PriestEffectIds.SacredFlame, typeof(Priest.SacredFlameDotEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/tarot-19-the-sun.svg"));

            // Priest Inner Flame / Suppress Pain: timed damage-reduction buffs (self / on an ally).
            Reg(Priest.PriestEffectIds.InnerFlame, typeof(Priest.InnerFlameEffect), "canrpgclasses:textures/icons/heraldic-sun.svg");
            Reg(Priest.PriestEffectIds.SuppressPain, typeof(Priest.SuppressPainEffect), "canrpgclasses:textures/icons/shield-opposition.svg");
            Reg(Priest.PriestEffectIds.SteadyWill, typeof(Priest.SteadyWillEffect), "canrpgclasses:textures/icons/cross-shield.svg");
            // Priest Dissipate: a timed strong self damage-reduction (paired with a mana refund on the spell).
            Reg(Priest.PriestEffectIds.Dissipate, typeof(Priest.DissipateEffect), "canrpgclasses:textures/icons/swirl-string.svg");
            // Priest Shadow Guise: a self-only toggled aura (like a warrior Stance).
            Reg(Priest.PriestEffectIds.ShadowGuise, typeof(Priest.ShadowGuiseEffect), "canrpgclasses:textures/icons/shadow-follower.svg");
            // Priest Shadow Orbs: the Shadow ramp buff. shouldBeRendered:false → no effectshud icon; the priest sees
            // it as combo-style pips in the spell HUD instead (HudSpellHotbarRenderer.DrawComboPoints). Still synced to
            // the client (sync ignores shouldBeRendered), so the pip count reads from onlyClientsActiveEffects.
            effectshud.src.effectshud.RegisterEffect(Priest.PriestEffectIds.ShadowOrbs, typeof(Priest.ShadowOrbsEffect),
                positive: true, shouldBeRendered: false, icon: new AssetLocation("canrpgclasses:textures/icons/orbital.svg"));

            // Priest Shadow DoTs: spell-power-scaled Shadow DEBUFFs (snapshot at cast). Consuming Plague also leeches.
            effectshud.src.effectshud.RegisterEffect(Priest.PriestEffectIds.ShadowBrand, typeof(Priest.ShadowBrandEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/despair.svg"));
            effectshud.src.effectshud.RegisterEffect(Priest.PriestEffectIds.ConsumingPlague, typeof(Priest.ConsumingPlagueEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/carrion.svg"));

            // Mage Armors: self-only toggled postures (one active at a time, like warrior Stances).
            Reg(Mage.MageEffectIds.MysticGuard, typeof(Mage.MysticGuardEffect), "canrpgclasses:textures/icons/book-aura.svg");
            Reg(Mage.MageEffectIds.FrostGuard, typeof(Mage.FrostGuardEffect), "canrpgclasses:textures/icons/icebergs.svg");
            Reg(Mage.MageEffectIds.MoltenGuard, typeof(Mage.MoltenGuardEffect), "canrpgclasses:textures/icons/lava.svg");
            // Mage timed spell-power cooldowns (positive self-buffs).
            Reg(Mage.MageEffectIds.Conflagration, typeof(Mage.ConflagrationEffect), "canrpgclasses:textures/icons/heptagram.svg");
            Reg(Mage.MageEffectIds.IcyVeins, typeof(Mage.IcyVeinsEffect), "canrpgclasses:textures/icons/eclipse.svg");
            Reg(Mage.MageEffectIds.MysticSurge, typeof(Mage.MysticSurgeEffect), "canrpgclasses:textures/icons/embrassed-energy.svg");
            Reg(Mage.MageEffectIds.HotStreak, typeof(Mage.HotStreakEffect), "canrpgclasses:textures/icons/heraldic-sun.svg");
            Reg(Mage.MageEffectIds.FieryHaste, typeof(Mage.FieryHasteEffect), "canrpgclasses:textures/icons/fire-dash.svg");
            Reg(Mage.MageEffectIds.ClearMind, typeof(Mage.ClearMindEffect), "canrpgclasses:textures/icons/sundial.svg");
            // Mage Mystic Charges: the stacking Arcane ramp buff (Mystic Blast builds it, the spenders consume it).
            Reg(Mage.MageEffectIds.MysticCharges, typeof(Mage.MysticChargesEffect), "canrpgclasses:textures/icons/power-ring.svg");

            // Mage frost chill: slows + marks the target as "frozen" for Ice Shard / Icebreaker.
            effectshud.src.effectshud.RegisterEffect(Mage.MageEffectIds.Chilled, typeof(Mage.ChilledEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/wave-crest.svg"));

            // Shaman Spirit Wolf: the self-only toggled travel form (model swap + speed), driven by EBAuras.
            Reg(Shaman.ShamanEffectIds.SpiritWolf, typeof(Shaman.SpiritWolfAuraEffect), "canrpgclasses:textures/icons/lion.svg");
            // Shaman Static Shield: the reactive charge buff (its tier is the remaining charge count).
            Reg(Shaman.ShamanEffectIds.StaticShield, typeof(Shaman.StaticShieldEffect), "canrpgclasses:textures/icons/shield-echoes.svg");
            // Shaman Elemental Surge: timed +nature/+fire spell power. Totem earth: the earth totem's pulse buff.
            Reg(Shaman.ShamanEffectIds.ElementalSurge, typeof(Shaman.ElementalSurgeEffect), "canrpgclasses:textures/icons/heraldic-sun.svg");
            Reg(Shaman.ShamanEffectIds.TotemEarth, typeof(Shaman.TotemEarthEffect), "canrpgclasses:textures/icons/mountains.svg");
            // Shaman Ember Shock: the Elemental fire DoT (snapshot) and Magma Burst's marker.
            effectshud.src.effectshud.RegisterEffect(Shaman.ShamanEffectIds.EmberShock, typeof(Shaman.EmberShockEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/burning-dot.svg"));
            // Shaman weapon imbues: mutually exclusive long self-buffs whose rider runs on every landed swing
            // (effectshud's own Effect.DidAttack hook - no core melee plumbing needed).
            Reg(Shaman.ShamanImbueIds.FlameEdge, typeof(Shaman.FlameEdgeImbueEffect), "canrpgclasses:textures/icons/match-head.svg");
            Reg(Shaman.ShamanImbueIds.FrostEdge, typeof(Shaman.FrostEdgeImbueEffect), "canrpgclasses:textures/icons/icicles-aura.svg");
            Reg(Shaman.ShamanImbueIds.GaleEdge, typeof(Shaman.GaleEdgeImbueEffect), "canrpgclasses:textures/icons/wind-hole.svg");
            // Shaman Enhancement: the Thunder Cleave arming window, the Maelstrom ramp, and the War Chant party burst.
            Reg(Shaman.ShamanEffectIds.ThunderCleaveArmed, typeof(Shaman.ThunderCleaveArmedEffect), "canrpgclasses:textures/icons/swords-power.svg");
            Reg(Shaman.ShamanEffectIds.Maelstrom, typeof(Shaman.MaelstromEffect), "canrpgclasses:textures/icons/air-zigzag.svg");
            Reg(Shaman.ShamanEffectIds.WarChant, typeof(Shaman.WarChantEffect), "canrpgclasses:textures/icons/enrage.svg");
            // Shaman Restoration: the Tide Surge HoT, the reactive Stone Ward (tier = charges) and the Rising Tide window.
            Reg(Shaman.ShamanRestoIds.TideSurge, typeof(Shaman.TideSurgeEffect), "canrpgclasses:textures/icons/splash.svg");
            Reg(Shaman.ShamanRestoIds.StoneWard, typeof(Shaman.StoneWardEffect), "canrpgclasses:textures/icons/surrounded-shield.svg");
            Reg(Shaman.ShamanRestoIds.RisingTide, typeof(Shaman.RisingTideEffect), "canrpgclasses:textures/icons/wave-crest.svg");
            // Shaman Thunder Cleave marker on the victim (a debuff: your lightning hits it harder).
            effectshud.src.effectshud.RegisterEffect(Shaman.ShamanEffectIds.ThunderCleave, typeof(Shaman.ThunderCleaveDebuffEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/hypersonic-bolt.svg"));

            // Druid forms (positive auras: model swap + form profile) and Feral/Balance bleeds/DoTs (negative).
            Reg(Druid.DruidEffectIds.FelineForm, typeof(Druid.FelineFormEffect), "canrpgclasses:textures/icons/hyena-head.svg");
            Reg(Druid.DruidEffectIds.UrsineForm, typeof(Druid.UrsineFormEffect), "canrpgclasses:textures/icons/lion.svg");
            void RegDot(string id, System.Type type, string icon) =>
                effectshud.src.effectshud.RegisterEffect(id, type, positive: false, shouldBeRendered: true, icon: new AssetLocation(icon));
            RegDot(Druid.DruidEffectIds.ClawSlash, typeof(Druid.ClawSlashBleedEffect), "canrpgclasses:textures/icons/blood.svg");
            RegDot(Druid.DruidEffectIds.DeepRend, typeof(Druid.DeepRendBleedEffect), "canrpgclasses:textures/icons/neck-bite.svg");
            RegDot(Druid.DruidEffectIds.Gash, typeof(Druid.GashBleedEffect), "canrpgclasses:textures/icons/ragged-wound.svg");
            RegDot(Druid.DruidEffectIds.LunarFlare, typeof(Druid.LunarFlareDotEffect), "canrpgclasses:textures/icons/falling-star.svg");
            RegDot(Druid.DruidEffectIds.StingingSwarm, typeof(Druid.StingingSwarmDotEffect), "canrpgclasses:textures/icons/tree-beehive.svg");
            Reg(Druid.DruidEffectIds.BarkHide, typeof(Druid.BarkHideEffect), "canrpgclasses:textures/icons/barbed-wire.svg");
            Reg(Druid.DruidEffectIds.FallingStars, typeof(Druid.FallingStarsEffect), "canrpgclasses:textures/icons/falling-star.svg");
            Reg(Druid.DruidEffectIds.VerdantRenewal, typeof(Druid.VerdantRenewalEffect), "canrpgclasses:textures/icons/pine-tree.svg");
            Reg(Druid.DruidEffectIds.RestorativeBloom, typeof(Druid.RestorativeBloomHotEffect), "canrpgclasses:textures/icons/pine-tree.svg");
            Reg(Druid.DruidEffectIds.SpreadingBloom, typeof(Druid.SpreadingBloomEffect), "canrpgclasses:textures/icons/pine-tree.svg");
            Reg(Druid.DruidEffectIds.LivingBlossom, typeof(Druid.LivingBlossomEffect), "canrpgclasses:textures/icons/pine-tree.svg");
            Reg(Druid.DruidEffectIds.Quickening, typeof(Druid.QuickeningEffect), "canrpgclasses:textures/icons/sundial.svg");

            // Display names for the effect list (C screen / HUD). Without these, effectshud humanizes the raw type id
            // ("canrpgaura_might" -> "Canrpgaura might", "canrpg_empower_death_blow" -> "Canrpg empower execute"). The Func
            // is resolved lazily on the client, so Lang picks the viewer's language. Reuses the existing ui-eff-* keys
            // where they already exist (priest/mage DoTs & buffs); the rest are added to the lang files.
            void Name(string id, string key)
                => effectshud.src.effectshud.RegisterEffectDisplayName(id, () => Vintagestory.API.Config.Lang.Get(key));

            Name(Core.Effects.ControlEffectIds.Stunned,     "canrpgclasses:ui-eff-stunned");
            Name(Core.Effects.ControlEffectIds.Feared,      "canrpgclasses:ui-eff-feared");
            Name(Core.Effects.ControlEffectIds.Silenced,    "canrpgclasses:ui-eff-silenced");
            Name(Core.Effects.ControlEffectIds.Rooted,      "canrpgclasses:ui-eff-rooted");
            Name(Core.Effects.ControlEffectIds.Polymorphed, "canrpgclasses:ui-eff-polymorphed");

            Name(Core.Effects.AuraEffectIds.Might, "canrpgclasses:ui-eff-aura_might");
            Name(Core.Effects.AuraEffectIds.Haste, "canrpgclasses:ui-eff-aura_haste");
            Name(Core.Effects.AuraEffectIds.Regen, "canrpgclasses:ui-eff-aura_regen");

            Name(Core.Effects.EmpowerEffectIds.ZealotsStrike,               "canrpgclasses:ui-eff-zealots_strike");
            Name(Core.Effects.EmpowerEffectIds.ViciousStrike,               "canrpgclasses:ui-eff-vicious_strike");
            Name(Core.Effects.EmpowerEffectIds.Prefix + "brutal_strike",    "canrpgclasses:ui-eff-brutal_strike");
            Name(Core.Effects.EmpowerEffectIds.Prefix + "death_blow",          "canrpgclasses:ui-eff-death_blow");
            Name(Core.Effects.EmpowerEffectIds.Prefix + "measured_shot",      "canrpgclasses:ui-eff-measured_shot");
            Name(Core.Effects.EmpowerEffectIds.Prefix + "finishing_shot",        "canrpgclasses:ui-eff-finishing_shot");
            Name(Core.Effects.EmpowerEffectIds.Prefix + "careful_shot",       "canrpgclasses:ui-eff-careful_shot");
            Name(Core.Effects.EmpowerEffectIds.Prefix + "split_shot",       "canrpgclasses:ui-eff-split_shot");
            Name(Core.Effects.EmpowerEffectIds.Prefix + "venom_sting",    "canrpgclasses:ui-eff-venom_sting");
            Name(Core.Effects.EmpowerEffectIds.Prefix + "jarring_shot",  "canrpgclasses:ui-eff-jarring_shot");

            Name(Core.Effects.QuickbladesEffectIds.Quickblades, "canrpgclasses:ui-eff-quickblades");
            Name(Core.Effects.EvasionEffectId.Id,          "canrpgclasses:ui-eff-evasion");
            Name(Hunter.ConcealmentEffectId.Id,            "canrpgclasses:ui-eff-concealment");

            Name(Warrior.StanceEffectIds.Battle,  "canrpgclasses:ui-eff-stance_battle");
            Name(Warrior.StanceEffectIds.Defense, "canrpgclasses:ui-eff-stance_defense");
            Name(Warrior.StanceEffectIds.Berserk, "canrpgclasses:ui-eff-stance_berserk");
            Name(Warrior.EnrageEffectId.Id,       "canrpgclasses:ui-eff-enrage");
            Name(Warrior.FullGuardEffectId.Id,    "canrpgclasses:ui-eff-full_guard");
            Name(Warrior.ShieldGuardEffectId.Id,  "canrpgclasses:ui-eff-shield_guard");
            Name(Warrior.MortalWoundEffectId.Id,  "canrpgclasses:ui-eff-mortal_wound");
            Name(Warrior.ArmorBreakEffectId.Id,   "canrpgclasses:ui-eff-sunder");
            Name(Core.Effects.WoundedEffectId.Id, "canrpgclasses:ui-eff-wounded");

            Name(Priest.PriestEffectIds.SoothingPrayer,   "canrpgclasses:ui-eff-renew");
            Name(Priest.PriestEffectIds.ShelteringSpirit, "canrpgclasses:ui-eff-sheltering_spirit");
            Name(Priest.PriestEffectIds.SacredFlame,      "canrpgclasses:ui-eff-sacred_flame");
            Name(Priest.PriestEffectIds.InnerFlame,       "canrpgclasses:ui-eff-inner_flame");
            Name(Priest.PriestEffectIds.SuppressPain,     "canrpgclasses:ui-eff-suppress_pain");
            Name(Priest.PriestEffectIds.SteadyWill,       "canrpgclasses:ui-eff-steady_will");
            Name(Priest.PriestEffectIds.Dissipate,        "canrpgclasses:ui-eff-dissipate");
            Name(Priest.PriestEffectIds.ShadowGuise,      "canrpgclasses:ui-eff-shadow_guise");
            Name(Priest.PriestEffectIds.ShadowOrbs,      "canrpgclasses:ui-eff-shadow_orbs");
            Name(Priest.PriestEffectIds.ShadowBrand,      "canrpgclasses:ui-eff-shadow_brand");
            Name(Priest.PriestEffectIds.ConsumingPlague,  "canrpgclasses:ui-eff-consuming_plague");

            Name(Mage.MageEffectIds.MysticGuard,    "canrpgclasses:ui-eff-mystic_guard");
            Name(Mage.MageEffectIds.FrostGuard,     "canrpgclasses:ui-eff-frost_guard");
            Name(Mage.MageEffectIds.MoltenGuard,    "canrpgclasses:ui-eff-molten_guard");
            Name(Mage.MageEffectIds.Conflagration,     "canrpgclasses:ui-eff-conflagration");
            Name(Mage.MageEffectIds.IcyVeins,       "canrpgclasses:ui-eff-icy_veins");
            Name(Mage.MageEffectIds.MysticSurge,    "canrpgclasses:ui-eff-mystic_surge");
            Name(Mage.MageEffectIds.HotStreak,      "canrpgclasses:ui-eff-hot_streak");
            Name(Mage.MageEffectIds.FieryHaste,     "canrpgclasses:ui-eff-fiery_haste");
            Name(Mage.MageEffectIds.ClearMind,      "canrpgclasses:ui-eff-clear_mind");
            Name(Mage.MageEffectIds.MysticCharges,  "canrpgclasses:ui-eff-arcane_charges");
            Name(Mage.MageEffectIds.Chilled,        "canrpgclasses:ui-eff-chilled");

            Name(Shaman.ShamanEffectIds.SpiritWolf,      "canrpgclasses:ui-eff-spirit_wolf");
            Name(Shaman.ShamanEffectIds.StaticShield,    "canrpgclasses:ui-eff-static_shield");
            Name(Shaman.ShamanEffectIds.EmberShock,      "canrpgclasses:ui-eff-ember_shock");
            Name(Shaman.ShamanEffectIds.ElementalSurge,  "canrpgclasses:ui-eff-elemental_surge");
            Name(Shaman.ShamanEffectIds.TotemEarth,      "canrpgclasses:ui-eff-totem_earth");
            Name(Shaman.ShamanImbueIds.FlameEdge,        "canrpgclasses:ui-eff-imbue_flametongue");
            Name(Shaman.ShamanImbueIds.FrostEdge,        "canrpgclasses:ui-eff-imbue_frostbrand");
            Name(Shaman.ShamanImbueIds.GaleEdge,         "canrpgclasses:ui-eff-imbue_windfury");
            Name(Shaman.ShamanEffectIds.ThunderCleaveArmed, "canrpgclasses:ui-eff-stormstrike_armed");
            Name(Shaman.ShamanEffectIds.ThunderCleave,   "canrpgclasses:ui-eff-thunder_cleave");
            Name(Shaman.ShamanEffectIds.Maelstrom,       "canrpgclasses:ui-eff-maelstrom");
            Name(Shaman.ShamanEffectIds.WarChant,        "canrpgclasses:ui-eff-war_chant");
            Name(Shaman.ShamanRestoIds.TideSurge,        "canrpgclasses:ui-eff-tide_surge");
            Name(Shaman.ShamanRestoIds.StoneWard,        "canrpgclasses:ui-eff-stone_ward");
            Name(Shaman.ShamanRestoIds.RisingTide,       "canrpgclasses:ui-eff-rising_tide");
            Name(Druid.DruidEffectIds.FelineForm,        "canrpgclasses:ui-eff-feline_form");
            Name(Druid.DruidEffectIds.UrsineForm,        "canrpgclasses:ui-eff-ursine_form");
            Name(Druid.DruidEffectIds.ClawSlash,         "canrpgclasses:ui-eff-druid_claw_slash");
            Name(Druid.DruidEffectIds.DeepRend,          "canrpgclasses:ui-eff-druid_deep_rend");
            Name(Druid.DruidEffectIds.Gash,              "canrpgclasses:ui-eff-druid_gash");
            Name(Druid.DruidEffectIds.LunarFlare,        "canrpgclasses:ui-eff-druid_lunar_flare");
            Name(Druid.DruidEffectIds.StingingSwarm,     "canrpgclasses:ui-eff-druid_insectswarm");
            Name(Druid.DruidEffectIds.BarkHide,          "canrpgclasses:ui-eff-druid_bark_hide");
            Name(Druid.DruidEffectIds.FallingStars,      "canrpgclasses:ui-eff-druid_falling_stars");
            Name(Druid.DruidEffectIds.VerdantRenewal,    "canrpgclasses:ui-eff-druid_rejuv");
            Name(Druid.DruidEffectIds.RestorativeBloom,  "canrpgclasses:ui-eff-druid_restorative_bloom");
            Name(Druid.DruidEffectIds.SpreadingBloom,    "canrpgclasses:ui-eff-druid_wildgrowth");
            Name(Druid.DruidEffectIds.LivingBlossom,     "canrpgclasses:ui-eff-druid_living_blossom");
            Name(Druid.DruidEffectIds.Quickening,        "canrpgclasses:ui-eff-druid_quickening");

            // Mage Fire DoTs (snapshot, negative debuffs). Volatile Ember detonates in OnExpire.
            effectshud.src.effectshud.RegisterEffect(Mage.MageEffectIds.Ignite, typeof(Mage.IgniteEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/burning-dot.svg"));
            effectshud.src.effectshud.RegisterEffect(Mage.MageEffectIds.VolatileEmber, typeof(Mage.VolatileEmberEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/burning-dot.svg"));
        }

        // Assets (incl. our config/*.json balance files) are guaranteed loaded here, but not yet in Start().
        // Load the balance config FIRST, then scan the registries - spell/talent constructors read the config.
        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);

            BalanceConfig.Load(api);
            Core.Attributes.RpgAttributes.Load(api); // before the registries: content spells/talents gate on attributes

            RebuildRegistries(Mod.Logger);
            SpellExecutor.RegisterDefaults();

            Mod.Logger.Notification("[canrpgclasses] {0} spell(s) registered", Spells.Count);
        }

        /// <summary>Rescans spells/classes/talents from this assembly. Constructors read BalanceConfig once, at
        /// construction - so this must re-run after every config change (asset load, server overrides layered,
        /// admin live-edit, client adopting the server's numbers) to bake the current numbers in.</summary>
        private void RebuildRegistries(ILogger? logger)
        {
            // Ours plus every loaded mod that references us - that's the extension point: an add-on declares
            // [SpellRegistration]/[TalentRegistration]/[RpgClassRegistration] types and they land in these
            // registries with no change here. Registration is by id, so a later assembly can also replace one
            // of ours deliberately. See Core.ModExtensions.
            foreach (var asm in Core.ModExtensions.ScanTargets(Api, logger))
            {
                Spells.ScanAssembly(asm, logger);
                Classes.ScanAssembly(asm, logger);
                Talents.ScanAssembly(asm, logger);
            }

            // JSON content last, so a data file can deliberately replace a compiled entry of the same id. The
            // derived lookups are rebuilt afterwards, or they'd miss everything registered here.
            LastContentReport = Core.Content.ContentLoader.Load(Api, Spells, Classes, Talents, logger);
            Spells.RebuildDerived();
            Talents.RebuildDerived();

            Core.Classes.GearAffinity.RebuildGlobal(); // class-agnostic affinities (metal armor → magic resist), from config

            // Payouts are resolved per class up front, so a class declaring its own affinity needs the rebuilt registry.
            var affinities = new System.Collections.Generic.List<(string, System.Collections.Generic.IReadOnlyList<(string, float)>)>();
            foreach (var c in Classes.All.Values) affinities.Add((c.Id, c.AttributeAffinity));
            Core.Attributes.RpgAttributes.SyncClassAffinity(affinities, logger);
        }

        // Harmony patches that enforce a stun (movement/jump, attacking, dropping items). See StunPatches.
        private void PatchStun()
        {
            if (harmony != null) return;
            harmony = new Harmony(HarmonyId);

            var pmodule = new HarmonyMethod(typeof(StunPatches).GetMethod(nameof(StunPatches.Prefix_PModuleApplicable)));
            harmony.Patch(typeof(PModuleOnGround).GetMethod("Applicable"), prefix: pmodule);
            harmony.Patch(typeof(PModuleInAir).GetMethod("Applicable"), prefix: pmodule);
            harmony.Patch(typeof(PModuleInLiquid).GetMethod("Applicable"), prefix: pmodule);

            var receiveDamage = typeof(Entity).GetMethod("ReceiveDamage");
            harmony.Patch(receiveDamage,
                prefix: new HarmonyMethod(typeof(StunPatches).GetMethod(nameof(StunPatches.Prefix_ReceiveDamage))));
            // Separate patch call: CombatTextPatches owns its own prefix/postfix pair (HP-before/after
            // snapshot via __state) so it isn't coupled to StunPatches' damage-mitigation prefix.
            harmony.Patch(receiveDamage,
                prefix: new HarmonyMethod(typeof(CombatTextPatches).GetMethod(nameof(CombatTextPatches.Prefix_ReceiveDamage))),
                postfix: new HarmonyMethod(typeof(CombatTextPatches).GetMethod(nameof(CombatTextPatches.Postfix_ReceiveDamage))));

            var dropItem = typeof(Vintagestory.Server.ServerPlayerInventoryManager).GetMethod("DropItem");
            if (dropItem != null)
            {
                harmony.Patch(dropItem,
                    prefix: new HarmonyMethod(typeof(StunPatches).GetMethod(nameof(StunPatches.Prefix_DropItem))));
            }

            // Hunter Split Shot: the vanilla bow release looses extra arrows when an armed Split Shot is up.
            var bowStop = typeof(Vintagestory.GameContent.ItemBow).GetMethod("OnHeldInteractStop");
            if (bowStop != null)
            {
                harmony.Patch(bowStop,
                    postfix: new HarmonyMethod(typeof(BowPatches).GetMethod(nameof(BowPatches.Postfix_OnHeldInteractStop))));
            }
        }

        // Client-only Harmony patch: right-click an item with a bound spell → cast it (SpellItemUsePatches).
        // The target type lives in the client assembly, so it's resolved by name (no compile-time reference) and
        // only patched here, on the client. harmony is created in PatchStun (Start), which runs before this.
        private void PatchItemSpellUse()
        {
            if (harmony == null) return;
            var t = HarmonyLib.AccessTools.TypeByName("Vintagestory.Client.NoObf.SystemMouseInWorldInteractions");
            var m = t == null ? null : HarmonyLib.AccessTools.Method(t, "TryBeginUseActiveSlotItem", new[]
            {
                typeof(BlockSelection), typeof(EntitySelection), typeof(EnumHandInteract), typeof(EnumHandHandling).MakeByRefType()
            });
            if (m == null) { Mod.Logger.Warning("[canrpgclasses] item-spell use patch target not found; right-click cast disabled."); return; }
            harmony.Patch(m, prefix: new HarmonyMethod(typeof(Core.HarmonyPatches.SpellItemUsePatches).GetMethod(nameof(Core.HarmonyPatches.SpellItemUsePatches.Prefix))));

            // Tooltip: show the bound spell on the item's held-item info (any item, reads the stack attribute).
            harmony.Patch(typeof(CollectibleObject).GetMethod(nameof(CollectibleObject.GetHeldItemInfo)),
                postfix: new HarmonyMethod(typeof(Core.HarmonyPatches.SpellItemUsePatches).GetMethod(nameof(Core.HarmonyPatches.SpellItemUsePatches.Postfix_GetHeldItemInfo))));
        }

        // Client-only Harmony patch: hide worn armour while in a model-swap form (Spirit Wolf / Transmute). Targets
        // PlayerModelLib's WearablesTesselatorBehavior by name (no compile reference); no-op if the mod is absent or
        // its internals moved. See ModelSwapArmorPatches.
        private void PatchModelSwapArmor()
        {
            if (harmony == null) return;
            var t = HarmonyLib.AccessTools.TypeByName("PlayerModelLib.WearablesTesselatorBehavior");
            var m = t == null ? null : HarmonyLib.AccessTools.Method(t, "ProcessSlot");
            if (m == null) { Mod.Logger.Notification("[canrpgclasses] PlayerModelLib wearables tesselator not found; armour stays visible in swap forms."); return; }
            Core.HarmonyPatches.ModelSwapArmorPatches.Init(t!);
            harmony.Patch(m, prefix: new HarmonyMethod(typeof(Core.HarmonyPatches.ModelSwapArmorPatches).GetMethod(nameof(Core.HarmonyPatches.ModelSwapArmorPatches.Prefix_ProcessSlot))));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            ClientApi = api;

            ClientChannel = api.Network.RegisterChannel(ChannelName);
            RegisterMessageTypes(ClientChannel);
            ClientChannel.SetMessageHandler<SpellCastSyncPacket>(OnCastSync);
            ClientChannel.SetMessageHandler<SpellCastCancelPacket>(OnCastCancel);
            ClientChannel.SetMessageHandler<SpellCooldownPacket>(OnCooldown);
            ClientChannel.SetMessageHandler<SpellCooldownSyncPacket>(OnCooldownSync);
            ClientChannel.SetMessageHandler<SpellMessagePacket>(OnSpellMessage);
            ClientChannel.SetMessageHandler<SetViewYawPacket>(OnSetViewYaw);
            ClientChannel.SetMessageHandler<DamageNumberPacket>(OnDamageNumber);
            ClientChannel.SetMessageHandler<BalanceConfigPacket>(OnBalanceConfig);
            ClientChannel.SetMessageHandler<PartyResourceMsg>(OnPartyResource);
            ClientChannel.SetMessageHandler<SelectClassResultPacket>(OnSelectClassResult);
            ClientChannel.SetMessageHandler<ClassRestrictionsPacket>(OnClassRestrictions);
            ClientChannel.SetMessageHandler<SkillFxPacket>(p => skillFxClient?.OnPacket(p));
            ClientChannel.SetMessageHandler<ContentSyncPacket>(OnContentSync);
            ClientChannel.SetMessageHandler<ContentEditResultPacket>(OnContentEditResult);

            viewYawForcer = new ViewYawForcer(api);
            shieldDomeRenderer = new ShieldDomeRenderer(api);
            shadowformTint = new ShadowformTint(api);
            effectVisualsEmitter = new EffectVisualsEmitter(api);
            beamRenderer = new BeamRenderer(api);
            boltRenderer = new BoltRenderer(api);
            skillFxClient = new SkillFxClient(api, boltRenderer);

            PatchItemSpellUse();
            PatchModelSwapArmor();

            // Registers itself with the Ortho render stage; nothing to open.
            combatOverlay = new Client.Render.CombatOverlayRenderer(api);

            partyResourceOverlay = new PartyResourceOverlay();

            SetupHotbar(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            ServerApi = api;

            ServerChannel = api.Network.RegisterChannel(ChannelName);
            RegisterMessageTypes(ServerChannel);
            ServerChannel.SetMessageHandler<SpellRequestPacket>(OnSpellRequest);
            ServerChannel.SetMessageHandler<TalentSpendPacket>(OnTalentSpend);
            ServerChannel.SetMessageHandler<TalentRespecPacket>(OnTalentRespec);
            ServerChannel.SetMessageHandler<TalentLoadoutPacket>(OnTalentLoadout);
            ServerChannel.SetMessageHandler<AdminProgressionPacket>(OnAdminProgression);
            ServerChannel.SetMessageHandler<SelectClassPacket>(OnSelectClass);
            ServerChannel.SetMessageHandler<BindSpellPacket>(OnBindSpell);
            ServerChannel.SetMessageHandler<BalanceEditPacket>(OnBalanceEdit);
            ServerChannel.SetMessageHandler<TalentTreeEditPacket>(OnTalentTreeEdit);
            ServerChannel.SetMessageHandler<ClassEditPacket>(OnClassEdit);
            ServerChannel.SetMessageHandler<SpellEditPacket>(OnSpellEdit);
            ServerChannel.SetMessageHandler<PetNamePacket>(OnPetName);

            // Layer persisted admin overrides (from the balance editor GUI) on top of the asset defaults that
            // AssetsLoaded already scanned, then rebuild the registries so spell/talent constructors (which read
            // BalanceConfig once, at construction) bake the overridden numbers in before any player connects.
            BalanceConfig.LoadOverrides(api);
            // Content authored in-game, same idea: read before the rebuild, since RebuildRegistries layers it on
            // top of the asset content files.
            Core.Content.ContentStore.Load(api);
            RebuildRegistries(Mod.Logger);

            // Which RPG class each vanilla character class may take (server-side mod config; writes a disabled
            // template on first run). Synced to each client on join so the picker matches what we'd accept.
            Core.Classes.ClassRestrictions.LoadServer(api);

            api.Event.OnEntityDeath += OnEntityDeath;
            // Send the authoritative balance config to each player as they finish joining (server = source of truth).
            api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
            api.Event.PlayerDisconnect += OnPlayerDisconnectCleanup;
            // Breaking a block is a revealing action - drop break-on-attack invisibility (Stealth/Slip Away/…) just
            // like attacking does, and trip the Stealth re-stealth lockout.
            api.Event.DidBreakBlock += OnDidBreakBlock;
            // Push each party member's class-resource fraction to their party, so canparty's frames show a mana/energy
            // bar per member (canparty syncs only HP). ~1s cadence - cheap, resource bars don't need to be frame-tight.
            partyResourceTickId = api.Event.RegisterGameTickListener(_ => PushPartyResources(), 1000);

            // Lingering damage zones (Hallowed Ground): one shared tick listener for all active zones.
            ZoneManager.Start(api);

            // Hunter pet content: registers the summon/pet-target impacts + the assist/pet-damage damage hook
            // into the core executor and pipeline (server-authoritative; no core reference to hunter content).
            Hunter.HunterPetSystem.Init();
            // Hunter shots: registers the "empower next bow shot" impact + its consume-on-arrow-hit hook.
            Hunter.HunterShots.Init();
            // Warrior rage: registers the rage-on-hit/on-damage-taken damage hook, the out-of-combat decay tick,
            // and the GainResource impact (Charge/Wild Rage build rage). Server-authoritative; no core coupling.
            Warrior.WarriorRage.Init();
            // Shaman totems: registers the PlaceTotem impact (its own action - Spawn's single handler belongs to
            // the hunter's wolf). The totem entity itself ticks via EBShamanTotem.
            Shaman.ShamanTotemSystem.Init();
            // Druid Bear-form Rage: registers the secondary "rage" pool, its rage-on-hit/on-damage-taken hook (gated
            // on Ursine Form) and the out-of-combat decay tick. Bear abilities spend it via Spell.SecondaryResourceId.
            Druid.DruidRage.Init();
            // Druid Cat-form Energy: registers the secondary "energy" pool + its passive regen tick. Cat abilities
            // spend it via Spell.SecondaryResourceId (paced by energy + combo + the shared cat GCD).
            Druid.DruidEnergy.Init();
            // Unified crowd-control layer: drives the player-fear wander tick (the Fear/Silence/Stun impacts are
            // registered in SpellExecutor.RegisterDefaults; the shared DR + gates live in ControlState).
            Core.Control.ControlState.Init();
            // Server-driven world visuals for ongoing states (forms/DR auras, DoT wisps, CC rings, HoT sparkles) -
            // so every nearby client sees them, distinct per effect. Absorb shields use their own client mesh dome.
            Core.Visuals.EffectVisuals.Init();
            // Training dummy: the /canrpgdummy damage-ignore hook + infinite-HP top-up tick (debug/testing tool).
            Core.Debug.TrainingDummy.Init();

            RegisterCommands(api);
        }

        private void OnDidBreakBlock(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            if (byPlayer?.Entity != null) SpellExecutor.BreakCasterInvisibility(byPlayer.Entity);
        }

        private void OnEntityDeath(Entity dead, DamageSource damageSource)
        {
            // Crowd control doesn't carry across death: you don't respawn still stunned/feared/silenced (also drops
            // the fear wander state and restores a mid-flight blind's screen effect).
            Core.Control.ControlState.ClearAll(dead);

            // Combo points don't carry across death (no-op for non-rogues, whose combo is always 0).
            ResourceState.ResetCombo(dead);

            // A "starts empty" resource (warrior rage) doesn't carry across death - you don't respawn still enraged.
            // Regenerating pools (energy/mana/focus) are left alone; they refill on their own.
            var deadPool = ResourceState.PrimaryPool(dead);
            if (deadPool != null && !deadPool.StartFull) ResourceState.Set(dead, deadPool, 0f);

            // A hunter's wolf died → put its owner's summon on the death lockout and clear the link.
            if (Hunter.HunterPetSystem.IsPet(dead)) Hunter.HunterPetSystem.OnPetDeath(dead);

            if (dead is EntityPlayer) return; // no XP for PvP kills (for now)
            if (damageSource?.GetCauseEntity() is not EntityPlayer killer) return;

            var prog = killer.GetBehavior<EBProgression>();
            if (prog == null) return;

            float maxHp = dead.WatchedAttributes.GetTreeAttribute("health")?.GetFloat("maxhealth", 1f) ?? 1f;
            prog.AddXp((long)(maxHp * EBProgression.XpPerHealth));
        }

        public override void Dispose()
        {
            base.Dispose();
            // Each side's instance tears down only its own statics. In singleplayer both instances are disposed,
            // and clearing the shared ServerApi/ClientApi from the wrong side would leave the server's event
            // subscriptions and tick listeners dangling.
            bool isServer = ReferenceEquals(ServerInstance, this);
            if (isServer && ServerApi != null)
            {
                ServerApi.Event.OnEntityDeath -= OnEntityDeath;
                ServerApi.Event.PlayerNowPlaying -= OnPlayerNowPlaying;
                ServerApi.Event.PlayerDisconnect -= OnPlayerDisconnectCleanup;
                ServerApi.Event.DidBreakBlock -= OnDidBreakBlock;
                if (partyResourceTickId != 0) ServerApi.Event.UnregisterGameTickListener(partyResourceTickId);
                partyResourceTickId = 0;
                ZoneManager.Stop();
                // These statics own their own server tick listeners + a re-entry guard; tear them down here so a
                // second world in the same process (singleplayer re-enter) re-registers on the fresh ServerApi.
                Core.Control.ControlState.Stop();
                Warrior.WarriorRage.Stop();
                Druid.DruidRage.Stop();
                Druid.DruidEnergy.Stop();
                Core.Visuals.EffectVisuals.Stop();
                Core.Debug.TrainingDummy.Stop();
            }
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            DisposeClientGui();
            if (ReferenceEquals(ClientInstance, this)) { ClientInstance = null; ClientApi = null; }
            if (isServer) { ServerInstance = null; ServerApi = null; }
            ServerChannel = null;
            ClientChannel = null;
        }
    }
}
