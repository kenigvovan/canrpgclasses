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
    /// <summary>Mod entry point: sided APIs, registries and the network channel. Network, commands and client GUI
    /// live in the other canrpgclasses.*.cs partials.</summary>
    public partial class canrpgclassesModSystem : ModSystem
    {
        public const string ModId = "canrpgclasses";
        public const string ChannelName = "canrpgclasses";
        /// <summary>Bumped when the add-on surface (RpgCombatApi, caster hooks) changes, so add-ons can refuse an older core.</summary>
        public const int ExtensionApiLevel = 5;

        // Per side: in singleplayer both ModSystem instances share this process, so one static would be
        // overwritten by whichever started last. Resolve via For(api).
        public static canrpgclassesModSystem? ClientInstance { get; private set; }
        public static canrpgclassesModSystem? ServerInstance { get; private set; }
        public static ICoreClientAPI? ClientApi { get; private set; }
        public static ICoreServerAPI? ServerApi { get; private set; }

        /// <summary>O(1), unlike ModLoader.GetModSystem's linear scan, so hot paths resolve through this.</summary>
        public static canrpgclassesModSystem? For(ICoreAPI? api)
            => api == null ? null : api.Side == EnumAppSide.Server ? ServerInstance : ClientInstance;

        public ICoreAPI Api { get; private set; } = null!;

        public SpellRegistry Spells { get; } = new SpellRegistry();
        public RpgClassRegistry Classes { get; } = new RpgClassRegistry();
        public TalentRegistry Talents { get; } = new TalentRegistry();

        /// <summary>Problems from the last content read, shown in chat by the reload command.</summary>
        public Core.Content.ContentReport? LastContentReport { get; private set; }

        // Add-on friend/foe overrides (NPC factions). Per instance, not static: both sides share the process in singleplayer.
        private readonly System.Collections.Generic.List<System.Func<Entity, Entity, bool?>> allyResolvers = new();

        /// <summary>Adds a resolver consulted before the party rule; it returns null when it has no opinion about the pair.</summary>
        public void RegisterAllyResolver(System.Func<Entity, Entity, bool?> resolver)
        {
            if (!allyResolvers.Contains(resolver)) allyResolvers.Add(resolver);
        }

        public void UnregisterAllyResolver(System.Func<Entity, Entity, bool?> resolver) => allyResolvers.Remove(resolver);

        /// <summary>First non-null answer of the registered resolvers, or null to fall back to the party rule.</summary>
        internal bool? ResolveAllyOverride(Entity caster, Entity target)
        {
            for (int i = 0; i < allyResolvers.Count; i++)
            {
                bool? decided;
                try { decided = allyResolvers[i](caster, target); }
                catch (System.Exception e)
                {
                    // One broken add-on must not break friend/foe for players and parties.
                    caster.Api?.Logger?.Warning("[canrpgclasses] ally resolver failed: {0}", e.Message);
                    continue;
                }
                if (decided.HasValue) return decided;
            }
            return null;
        }

        // Add-on crowd control duration changes (boss immunities, tier resistances). Per instance, like the ally resolvers.
        private readonly System.Collections.Generic.List<System.Func<Entity, Core.Control.ControlType, float, Entity?, float>> controlDurationModifiers = new();

        public void RegisterControlDurationModifier(System.Func<Entity, Core.Control.ControlType, float, Entity?, float> modifier)
        {
            if (!controlDurationModifiers.Contains(modifier)) controlDurationModifiers.Add(modifier);
        }

        public void UnregisterControlDurationModifier(System.Func<Entity, Core.Control.ControlType, float, Entity?, float> modifier)
            => controlDurationModifiers.Remove(modifier);

        /// <summary>The duration after every registered modifier, in order; a modifier that throws is skipped.</summary>
        internal float ModifyControlDuration(Entity target, Core.Control.ControlType type, float seconds, Entity? source)
        {
            for (int i = 0; i < controlDurationModifiers.Count; i++)
            {
                try { seconds = controlDurationModifiers[i](target, type, seconds, source); }
                catch (System.Exception e) { target.Api?.Logger?.Warning("[canrpgclasses] control duration modifier failed: {0}", e.Message); }
            }
            return seconds;
        }

        internal IServerNetworkChannel? ServerChannel;
        internal IClientNetworkChannel? ClientChannel;

        // Id of the PushPartyResources tick listener, so Dispose can unregister it.
        private long partyResourceTickId;

        // Static and guarded: in singleplayer Start runs on both sides in one process, but we patch once.
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
            // Pet seek/melee tasks gated by the aggressive/defensive/passive order.
            Vintagestory.GameContent.AiTaskRegistry.Register<Hunter.AiTaskPetSeek>("petseek");
            Vintagestory.GameContent.AiTaskRegistry.Register<Hunter.AiTaskPetMelee>("petmelee");
            api.RegisterEntity("EntitySpellProjectile", typeof(EntitySpellProjectile));
            api.RegisterCollectibleBehaviorClass(BehaviorSpellContainer.Name, typeof(BehaviorSpellContainer));

            PatchStun();

            RegisterAuraEffects();

            Mod.Logger.Notification("[canrpgclasses] loaded ({0})", api.Side);
        }

        // Registers our effectshud effects (both sides).
        private static void RegisterAuraEffects()
        {
            void Reg(string id, System.Type type, string icon) =>
                effectshud.src.effectshud.RegisterEffect(id, type, positive: true, shouldBeRendered: true,
                    icon: new AssetLocation(icon));

            // Cosmetic CC countdowns, applied/cleared by ControlState.
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

            // Cosmetic empower-window countdowns; the bonus itself lives in WatchedAttributes.
            Reg(Core.Effects.EmpowerEffectIds.ZealotsStrike, typeof(Core.Effects.ZealotsStrikeEmpowerEffect), "canrpgclasses:textures/icons/shining-sword.svg");
            Reg(Core.Effects.EmpowerEffectIds.ViciousStrike, typeof(Core.Effects.ViciousStrikeEmpowerEffect), "canrpgclasses:textures/icons/scalpel-strike.svg");
            Reg(Core.Effects.EmpowerEffectIds.Prefix + "brutal_strike", typeof(Core.Effects.BrutalStrikeEmpowerEffect), "canrpgclasses:textures/icons/blade-fall.svg");
            Reg(Core.Effects.EmpowerEffectIds.Prefix + "death_blow", typeof(Core.Effects.DeathBlowEmpowerEffect), "canrpgclasses:textures/icons/chopped-skull.svg");

            // Own id/stat key so per-hit ConsumeStack never drains shared strengthmelee buffs.
            Reg(Core.Effects.QuickbladesEffectIds.Quickblades, typeof(Core.Effects.QuickbladesEffect), "canrpgclasses:textures/icons/sacrificial-dagger.svg");

            Reg(Core.Effects.EmpowerEffectIds.Prefix + "measured_shot", typeof(Core.Effects.MeasuredShotEmpowerEffect), "canrpgclasses:textures/icons/air-zigzag.svg");
            Reg(Core.Effects.EmpowerEffectIds.Prefix + "finishing_shot", typeof(Core.Effects.FinishingShotEmpowerEffect), "canrpgclasses:textures/icons/pierced-body.svg");
            Reg(Core.Effects.EmpowerEffectIds.Prefix + "careful_shot", typeof(Core.Effects.CarefulShotEmpowerEffect), "canrpgclasses:textures/icons/deadly-strike.svg");
            Reg(Core.Effects.EmpowerEffectIds.Prefix + "split_shot", typeof(Core.Effects.SplitShotEmpowerEffect), "canrpgclasses:textures/icons/sword-array.svg");
            Reg(Core.Effects.EmpowerEffectIds.Prefix + "venom_sting", typeof(Core.Effects.VenomStingEmpowerEffect), "canrpgclasses:textures/icons/burning-dot.svg");
            Reg(Core.Effects.EmpowerEffectIds.Prefix + "jarring_shot", typeof(Core.Effects.JarringShotEmpowerEffect), "canrpgclasses:textures/icons/knockout.svg");

            // Cosmetic; the dodge roll itself is a WatchedAttributes flag.
            Reg(Core.Effects.EvasionEffectId.Id, typeof(Core.Effects.EvasionEffect), "canrpgclasses:textures/icons/jump-across.svg");

            // Lowers animalSeekingRange; not invisibility.
            Reg(Hunter.ConcealmentEffectId.Id, typeof(Hunter.ConcealmentEffect), "canrpgclasses:textures/icons/hood.svg");

            Reg(Warrior.StanceEffectIds.Battle, typeof(Warrior.OffensiveStanceEffect), "canrpgclasses:textures/icons/battle-gear.svg");
            Reg(Warrior.StanceEffectIds.Defense, typeof(Warrior.GuardedStanceEffect), "canrpgclasses:textures/icons/cross-shield.svg");
            Reg(Warrior.StanceEffectIds.Berserk, typeof(Warrior.RecklessStanceEffect), "canrpgclasses:textures/icons/enrage.svg");

            Reg(Warrior.EnrageEffectId.Id, typeof(Warrior.EnrageEffect), "canrpgclasses:textures/icons/enrage.svg");
            Reg(Warrior.FullGuardEffectId.Id, typeof(Warrior.FullGuardEffect), "canrpgclasses:textures/icons/diamond-hard.svg");
            Reg(Warrior.ShieldGuardEffectId.Id, typeof(Warrior.ShieldGuardEffect), "canrpgclasses:textures/icons/cross-shield.svg");

            // Debuffs are registered directly, since Reg hardcodes positive:true.
            effectshud.src.effectshud.RegisterEffect(Core.Effects.WoundedEffectId.Id, typeof(Core.Effects.WoundedEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/ragged-wound.svg"));

            // Separate from WoundedEffect so the two heal-cuts balance independently.
            effectshud.src.effectshud.RegisterEffect(Warrior.MortalWoundEffectId.Id, typeof(Warrior.MortalWoundEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/swordwoman.svg"));

            effectshud.src.effectshud.RegisterEffect(Warrior.ArmorBreakEffectId.Id, typeof(Warrior.ArmorBreakEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/shoulder-armor.svg"));

            // Flag effects; the mechanic runs client-side.
            Reg(Core.Effects.UtilityEffectIds.MagicLight, typeof(Core.Effects.MagicLightEffect), "canrpgclasses:textures/icons/sunbeams.svg");
            Reg(Core.Effects.UtilityEffectIds.SlowFall, typeof(Core.Effects.SlowFallEffect), "canrpgclasses:textures/icons/two-feathers.svg");
            Reg(Core.Effects.UtilityEffectIds.WaterWalk, typeof(Core.Effects.WaterWalkEffect), "canrpgclasses:textures/icons/wave-crest.svg");

            Reg(Priest.PriestEffectIds.SoothingPrayer, typeof(Priest.SoothingPrayerEffect), "canrpgclasses:textures/icons/bandaged.svg");
            Reg(Priest.PriestEffectIds.ShelteringSpirit, typeof(Priest.ShelteringSpiritEffect), "canrpgclasses:textures/icons/angel-wings.svg");

            effectshud.src.effectshud.RegisterEffect(Priest.PriestEffectIds.SacredFlame, typeof(Priest.SacredFlameDotEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/tarot-19-the-sun.svg"));

            Reg(Priest.PriestEffectIds.InnerFlame, typeof(Priest.InnerFlameEffect), "canrpgclasses:textures/icons/heraldic-sun.svg");
            Reg(Priest.PriestEffectIds.SuppressPain, typeof(Priest.SuppressPainEffect), "canrpgclasses:textures/icons/shield-opposition.svg");
            Reg(Priest.PriestEffectIds.SteadyWill, typeof(Priest.SteadyWillEffect), "canrpgclasses:textures/icons/cross-shield.svg");
            Reg(Priest.PriestEffectIds.Dissipate, typeof(Priest.DissipateEffect), "canrpgclasses:textures/icons/swirl-string.svg");
            Reg(Priest.PriestEffectIds.ShadowGuise, typeof(Priest.ShadowGuiseEffect), "canrpgclasses:textures/icons/shadow-follower.svg");
            // No effectshud icon: shown as pips in the spell HUD. Sync ignores shouldBeRendered, so the client still gets it.
            effectshud.src.effectshud.RegisterEffect(Priest.PriestEffectIds.ShadowOrbs, typeof(Priest.ShadowOrbsEffect),
                positive: true, shouldBeRendered: false, icon: new AssetLocation("canrpgclasses:textures/icons/orbital.svg"));

            effectshud.src.effectshud.RegisterEffect(Priest.PriestEffectIds.ShadowBrand, typeof(Priest.ShadowBrandEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/despair.svg"));
            effectshud.src.effectshud.RegisterEffect(Priest.PriestEffectIds.ConsumingPlague, typeof(Priest.ConsumingPlagueEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/carrion.svg"));

            Reg(Mage.MageEffectIds.MysticGuard, typeof(Mage.MysticGuardEffect), "canrpgclasses:textures/icons/book-aura.svg");
            Reg(Mage.MageEffectIds.FrostGuard, typeof(Mage.FrostGuardEffect), "canrpgclasses:textures/icons/icebergs.svg");
            Reg(Mage.MageEffectIds.MoltenGuard, typeof(Mage.MoltenGuardEffect), "canrpgclasses:textures/icons/lava.svg");
            Reg(Mage.MageEffectIds.Conflagration, typeof(Mage.ConflagrationEffect), "canrpgclasses:textures/icons/heptagram.svg");
            Reg(Mage.MageEffectIds.IcyVeins, typeof(Mage.IcyVeinsEffect), "canrpgclasses:textures/icons/eclipse.svg");
            Reg(Mage.MageEffectIds.MysticSurge, typeof(Mage.MysticSurgeEffect), "canrpgclasses:textures/icons/embrassed-energy.svg");
            Reg(Mage.MageEffectIds.HotStreak, typeof(Mage.HotStreakEffect), "canrpgclasses:textures/icons/heraldic-sun.svg");
            Reg(Mage.MageEffectIds.FieryHaste, typeof(Mage.FieryHasteEffect), "canrpgclasses:textures/icons/fire-dash.svg");
            Reg(Mage.MageEffectIds.ClearMind, typeof(Mage.ClearMindEffect), "canrpgclasses:textures/icons/sundial.svg");
            Reg(Mage.MageEffectIds.MysticCharges, typeof(Mage.MysticChargesEffect), "canrpgclasses:textures/icons/power-ring.svg");

            effectshud.src.effectshud.RegisterEffect(Mage.MageEffectIds.Chilled, typeof(Mage.ChilledEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/wave-crest.svg"));

            Reg(Shaman.ShamanEffectIds.SpiritWolf, typeof(Shaman.SpiritWolfAuraEffect), "canrpgclasses:textures/icons/lion.svg");
            Reg(Shaman.ShamanEffectIds.StaticShield, typeof(Shaman.StaticShieldEffect), "canrpgclasses:textures/icons/shield-echoes.svg");
            Reg(Shaman.ShamanEffectIds.ElementalSurge, typeof(Shaman.ElementalSurgeEffect), "canrpgclasses:textures/icons/heraldic-sun.svg");
            Reg(Shaman.ShamanEffectIds.TotemEarth, typeof(Shaman.TotemEarthEffect), "canrpgclasses:textures/icons/mountains.svg");
            effectshud.src.effectshud.RegisterEffect(Shaman.ShamanEffectIds.EmberShock, typeof(Shaman.EmberShockEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/burning-dot.svg"));
            // Imbue riders run via effectshud's own Effect.DidAttack hook.
            Reg(Shaman.ShamanImbueIds.FlameEdge, typeof(Shaman.FlameEdgeImbueEffect), "canrpgclasses:textures/icons/match-head.svg");
            Reg(Shaman.ShamanImbueIds.FrostEdge, typeof(Shaman.FrostEdgeImbueEffect), "canrpgclasses:textures/icons/icicles-aura.svg");
            Reg(Shaman.ShamanImbueIds.GaleEdge, typeof(Shaman.GaleEdgeImbueEffect), "canrpgclasses:textures/icons/wind-hole.svg");
            Reg(Shaman.ShamanEffectIds.ThunderCleaveArmed, typeof(Shaman.ThunderCleaveArmedEffect), "canrpgclasses:textures/icons/swords-power.svg");
            Reg(Shaman.ShamanEffectIds.Maelstrom, typeof(Shaman.MaelstromEffect), "canrpgclasses:textures/icons/air-zigzag.svg");
            Reg(Shaman.ShamanEffectIds.WarChant, typeof(Shaman.WarChantEffect), "canrpgclasses:textures/icons/enrage.svg");
            Reg(Shaman.ShamanRestoIds.TideSurge, typeof(Shaman.TideSurgeEffect), "canrpgclasses:textures/icons/splash.svg");
            Reg(Shaman.ShamanRestoIds.StoneWard, typeof(Shaman.StoneWardEffect), "canrpgclasses:textures/icons/surrounded-shield.svg");
            Reg(Shaman.ShamanRestoIds.RisingTide, typeof(Shaman.RisingTideEffect), "canrpgclasses:textures/icons/wave-crest.svg");
            effectshud.src.effectshud.RegisterEffect(Shaman.ShamanEffectIds.ThunderCleave, typeof(Shaman.ThunderCleaveDebuffEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/hypersonic-bolt.svg"));

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

            // Display names; otherwise effectshud humanizes the raw id. Resolved lazily so Lang uses the viewer's language.
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
            Name(Core.Effects.UtilityEffectIds.MagicLight, "canrpgclasses:ui-eff-guiding_light");
            Name(Core.Effects.UtilityEffectIds.SlowFall,   "canrpgclasses:ui-eff-feather_fall");
            Name(Core.Effects.UtilityEffectIds.WaterWalk,  "canrpgclasses:ui-eff-water_walking");
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

            effectshud.src.effectshud.RegisterEffect(Mage.MageEffectIds.Ignite, typeof(Mage.IgniteEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/burning-dot.svg"));
            effectshud.src.effectshud.RegisterEffect(Mage.MageEffectIds.VolatileEmber, typeof(Mage.VolatileEmberEffect),
                positive: false, shouldBeRendered: true, icon: new AssetLocation("canrpgclasses:textures/icons/burning-dot.svg"));
        }

        // Assets aren't loaded yet in Start(). Balance config must load before the registries: constructors read it.
        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);

            BalanceConfig.Load(api);
            Core.Attributes.RpgAttributes.Load(api); // before the registries: content spells/talents gate on attributes
            XpRules.Load(api);
            SpellSounds.Load(api);

            RebuildRegistries(Mod.Logger);
            SpellExecutor.RegisterDefaults();

            Mod.Logger.Notification("[canrpgclasses] {0} spell(s) registered", Spells.Count);
        }

        /// <summary>Constructors read BalanceConfig once, so this must re-run after every config change.</summary>
        private void RebuildRegistries(ILogger? logger)
        {
            // Ours plus every add-on assembly referencing us (see Core.ModExtensions).
            foreach (var asm in Core.ModExtensions.ScanTargets(Api, logger))
            {
                Spells.ScanAssembly(asm, logger);
                Classes.ScanAssembly(asm, logger);
                Talents.ScanAssembly(asm, logger);
            }

            // JSON last so it can replace compiled entries; derived lookups rebuilt after.
            LastContentReport = Core.Content.ContentLoader.Load(Api, Spells, Classes, Talents, logger);
            Spells.RebuildDerived();
            Talents.RebuildDerived();

            Core.Classes.GearAffinity.RebuildGlobal();

            // Payouts are pre-resolved per class, so this needs the rebuilt class registry.
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
            // Separate patch: CombatTextPatches pairs its own prefix/postfix via __state.
            harmony.Patch(receiveDamage,
                prefix: new HarmonyMethod(typeof(CombatTextPatches).GetMethod(nameof(CombatTextPatches.Prefix_ReceiveDamage))),
                postfix: new HarmonyMethod(typeof(CombatTextPatches).GetMethod(nameof(CombatTextPatches.Postfix_ReceiveDamage))));

            var dropItem = typeof(Vintagestory.Server.ServerPlayerInventoryManager).GetMethod("DropItem");
            if (dropItem != null)
            {
                harmony.Patch(dropItem,
                    prefix: new HarmonyMethod(typeof(StunPatches).GetMethod(nameof(StunPatches.Prefix_DropItem))));
            }

            // Split Shot: extra arrows on vanilla bow release.
            var bowStop = typeof(Vintagestory.GameContent.ItemBow).GetMethod("OnHeldInteractStop");
            if (bowStop != null)
            {
                harmony.Patch(bowStop,
                    postfix: new HarmonyMethod(typeof(BowPatches).GetMethod(nameof(BowPatches.Postfix_OnHeldInteractStop))));
            }
        }

        // Client only: right-click casts an item's bound spell. Target lives in the client assembly, so it's
        // resolved by name. Relies on harmony from PatchStun (Start).
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

            // Bound-spell tooltip line.
            harmony.Patch(typeof(CollectibleObject).GetMethod(nameof(CollectibleObject.GetHeldItemInfo)),
                postfix: new HarmonyMethod(typeof(Core.HarmonyPatches.SpellItemUsePatches).GetMethod(nameof(Core.HarmonyPatches.SpellItemUsePatches.Postfix_GetHeldItemInfo))));
        }

        // Client only: hide worn armour in model-swap forms. Targets PlayerModelLib by name; no-op if absent.
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
            ClientChannel.SetMessageHandler<PortalStatePacket>(p => portalRenderer?.OnPacket(p));
            ClientChannel.SetMessageHandler<Core.Portals.PortalAnchorsPacket>(Core.Portals.PortalAnchors.OnPacket);
            ClientChannel.SetMessageHandler<Core.Portals.PortalShotRequestPacket>(p => portalRenderer?.RequestCapture(p.Key));
            ClientChannel.SetMessageHandler<Core.Portals.SummonOfferPacket>(ShowSummonOffer);
            ClientChannel.SetMessageHandler<SpellSoundPacket>(p => SpellSounds.PlayLocal(api.World, p, hudLayout?.SkillSoundVolume ?? 1f));

            viewYawForcer = new ViewYawForcer(api);
            shieldDomeRenderer = new ShieldDomeRenderer(api);
            shadowformTint = new ShadowformTint(api);
            effectVisualsEmitter = new EffectVisualsEmitter(api);
            beamRenderer = new BeamRenderer(api);
            boltRenderer = new BoltRenderer(api);
            skillFxClient = new SkillFxClient(api, boltRenderer);
            portalRenderer = new PortalRenderer(api);
            utilityBuffs = new UtilityBuffsClient(api);
            castSounds = new SpellCastSounds(api, () => hudLayout?.SkillSoundVolume ?? 1f);

            PatchItemSpellUse();
            PatchModelSwapArmor();

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
            ServerChannel.SetMessageHandler<Core.Portals.PortalShotPacket>(Core.Portals.PortalShots.OnUpload);
            ServerChannel.SetMessageHandler<Core.Portals.SummonAnswerPacket>(Core.Portals.SummonRitual.OnAnswer);

            // Overrides and in-game content must be read before the rebuild so constructors bake them in.
            BalanceConfig.LoadOverrides(api);
            Core.Content.ContentStore.Load(api);
            RebuildRegistries(Mod.Logger);

            Core.Classes.ClassRestrictions.LoadServer(api);

            api.Event.OnEntityDeath += OnEntityDeath;
            api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
            api.Event.PlayerDisconnect += OnPlayerDisconnectCleanup;
            // Breaking a block reveals you like attacking does.
            api.Event.DidBreakBlock += OnDidBreakBlock;
            // Resource bars for canparty frames (canparty syncs only HP).
            partyResourceTickId = api.Event.RegisterGameTickListener(_ => PushPartyResources(), 1000);

            ZoneManager.Start(api);
            Core.Portals.PortalManager.Start(api);
            Core.Portals.PortalSpells.Init();
            Core.Portals.PortalAnchors.Start(api);
            Core.Portals.SummonRitual.Init();

            // Class modules register their impacts and damage hooks into the core here.
            Hunter.HunterPetSystem.Init();
            Hunter.HunterShots.Init();
            Warrior.WarriorRage.Init();
            Shaman.ShamanTotemSystem.Init();
            Druid.DruidRage.Init();
            Druid.DruidEnergy.Init();
            Core.Control.ControlState.Init();
            Core.Visuals.EffectVisuals.Init();
            Core.Debug.TrainingDummy.Init();

            RegisterCommands(api);
        }

        private void OnDidBreakBlock(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            if (byPlayer?.Entity != null) SpellExecutor.BreakCasterInvisibility(byPlayer.Entity);
        }

        private void OnEntityDeath(Entity dead, DamageSource damageSource)
        {
            Core.Control.ControlState.ClearAll(dead);

            ResourceState.ResetCombo(dead);

            // Start-empty pools (rage) reset on death; regenerating ones refill on their own.
            var deadPool = ResourceState.PrimaryPool(dead);
            if (deadPool != null && !deadPool.StartFull) ResourceState.Set(dead, deadPool, 0f);

            // A hunter's wolf died → put its owner's summon on the death lockout and clear the link.
            if (Hunter.HunterPetSystem.IsPet(dead)) Hunter.HunterPetSystem.OnPetDeath(dead);

            if (dead is EntityPlayer) return; // no XP for PvP kills (for now)
            if (damageSource?.GetCauseEntity() is not EntityPlayer killer) return;

            var prog = killer.GetBehavior<EBProgression>();
            if (prog == null) return;

            prog.AddXp(XpRules.XpFor(dead, prog.Level));
        }

        public override void Dispose()
        {
            base.Dispose();
            // Each side tears down only its own statics; in singleplayer the wrong side would leave server listeners dangling.
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
                Core.Portals.PortalManager.Stop();
                Core.Portals.PortalAnchors.Stop();
                Core.Portals.SummonRitual.Stop();
                // Reset their re-entry guards so a second world in this process re-registers on the new ServerApi.
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
