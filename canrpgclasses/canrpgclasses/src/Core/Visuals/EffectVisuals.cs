using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using canrpgclasses.Core.Effects;
using canrpgclasses.Core.Spells;
using EBEffects = effectshud.src.EBEffectsAffected;

namespace canrpgclasses.Core.Visuals
{
    /// <summary>
    /// World visuals for ongoing states (auras, DoT wisps, CC rings): the server syncs a per-entity bitmask of
    /// active <see cref="Specs"/> rows and clients spawn the particles locally. Detection must be server-side -
    /// effectshud effects sync only to their bearer, so the mask is how others see a mob's DoTs.
    /// </summary>
    public static class EffectVisuals
    {
        /// <summary>Synced per-entity bitmask of active visual states (bit i = Specs[i] active).</summary>
        // 64 bits: the Specs list is past 32 rows, and a 32-bit mask would overflow (1 << 32 wraps in C#). The
        // key carries an L suffix because VS casts rather than converts attributes, so reading an older save's
        // int value with GetLong would throw.
        public const string MaskAttr = "canrpgVisualsMaskL";

        /// <summary>Synced per-entity COUNTER pack for visuals that show a number, not just a state - the orbiting
        /// charge/stack motes (shield charges, Mystic Charges, Shadow Orbs, Maelstrom = orbiting orbs). eight byte
        /// slots in one long (slot i = bits 8i..8i+7), published exactly like the mask: server-detected (stack tiers
        /// only sync to their bearer, so other clients learn the count from here), written on change only.</summary>
        // Key bumped when the pack widened from int (4 slots) to long (8 slots): a save/session that still had the
        // old IntAttribute under the previous key would throw on GetLong (VS casts, doesn't convert). The new key
        // starts clean as a LongAttribute; the orphaned int is simply never read again.
        public const string CountsAttr = "canrpgFxCountsL";

        /// <summary>Synced on a caster while a beam channel (Spell.BeamFx, e.g. Mind Rend) is running: the target's
        /// entity id (0 = no beam) plus the school for the colour. The client resolves both entities each visual
        /// tick, so the beam follows a moving target; the server clears it on every channel-end path. State, not a
        /// packet: a beam outlives any single event.</summary>
        public const string BeamTargetAttr = "canrpgBeamTarget";
        public const string BeamSchoolAttr = "canrpgBeamSchool";

        /// <summary>Synced on the BEARER of a lingering link effect (Stone Ward): the entity id of the other end
        /// - the protector who cast it (0 = no link) - plus the school for the colour. A persistent thin line the
        /// TetherRenderer draws bearer → protector, so you can see who a shielded ally is bonded to. Recomputed each
        /// server tick from live effect state (relog-safe), same publish-on-change discipline as the mask.</summary>
        public const string TetherTargetAttr = "canrpgTetherTarget";
        public const string TetherSchoolAttr = "canrpgTetherSchool";

        private enum Style { Aura, Wisp, Overhead, Heal, Front }

        private readonly struct Spec
        {
            // Detector gets the entity and its effects behavior (fetched once per entity per tick, not per spec).
            public readonly System.Func<Entity, EBEffects?, bool> Active;
            public readonly int Color; // ARGB (ColorUtil.ToRgba(a,r,g,b))
            public readonly Style Style;
            // True when the detector only reads the effects behavior (Eff) - such rows are skipped wholesale for
            // entities with no active effects, which is nearly every mob in the world. Rows reading WatchedAttributes
            // directly (CC flags, pet wrath) pass false and are always evaluated.
            public readonly bool NeedsEffects;
            public Spec(System.Func<Entity, EBEffects?, bool> active, int color, Style style, bool needsEffects = true)
            { Active = active; Color = color; Style = style; NeedsEffects = needsEffects; }
        }

        private static bool Eff(EBEffects? ce, string id) => ce?.HasEffect(id) ?? false;
        private static bool Flag(Entity e, string key) => e.WatchedAttributes?.GetBool(key) ?? false;

        // The one place each state's world visual is declared. Distinct colour per effect so they're tellable apart.
        // Order defines the mask bits - append new rows at the end (bit meanings are only transient WA state, but
        // mid-list inserts would briefly mis-colour effects on clients from an older tick).
        private static readonly Spec[] Specs =
        {
            // --- Forms / power auras ---
            new Spec((e, ce) => Eff(ce, Priest.PriestEffectIds.ShadowGuise),        ColorUtil.ToRgba( 90,  80,  20, 100), Style.Aura),  // dark violet
            // (Reckless Stance intentionally has NO world particle - removed on request; the stance is still a
            //  gameplay effect, just no aura. Its old bit slot is gone; downstream bits shift up by one, which is fine
            //  since server publish + client emit share this same array.)
            new Spec((e, ce) => e.WatchedAttributes.GetFloat(Hunter.HunterPetSystem.PetWrathBonusKey, 1f) > 1.01f,
                                                                                   ColorUtil.ToRgba( 90, 225,  70,  35), Style.Aura, needsEffects: false),  // bestial red (pet)

            // --- Defensive damage-reduction shells (distinct blues/whites/purple) ---
            new Spec((e, ce) => Eff(ce, Warrior.FullGuardEffectId.Id),            ColorUtil.ToRgba(110, 120, 160, 225), Style.Front),  // steel blue - a shield raised in front
            new Spec((e, ce) => Eff(ce, Warrior.ShieldGuardEffectId.Id),           ColorUtil.ToRgba( 90, 150, 195, 235), Style.Front),  // light blue - a shield raised in front
            new Spec((e, ce) => Eff(ce, Priest.PriestEffectIds.SuppressPain),   ColorUtil.ToRgba(110, 235, 240, 250), Style.Aura),  // white
            new Spec((e, ce) => Eff(ce, Priest.PriestEffectIds.Dissipate),        ColorUtil.ToRgba(110, 150,  90, 215), Style.Aura),  // purple
            new Spec((e, ce) => Eff(ce, Priest.PriestEffectIds.InnerFlame),         ColorUtil.ToRgba( 70, 235, 215, 135), Style.Aura),  // soft gold
            new Spec((e, ce) => Eff(ce, Priest.PriestEffectIds.ShelteringSpirit),    ColorUtil.ToRgba(110, 255, 240, 185), Style.Aura),  // radiant gold

            // --- Heal-over-time (rising sparkles) ---
            new Spec((e, ce) => Eff(ce, Priest.PriestEffectIds.SoothingPrayer),             ColorUtil.ToRgba( 90, 150, 240, 150), Style.Heal),  // green-gold

            // --- Damage-over-time wisps (subtle, on the afflicted target) ---
            new Spec((e, ce) => Eff(ce, Priest.PriestEffectIds.ShadowBrand),    ColorUtil.ToRgba(130,  95,  45, 130), Style.Wisp),  // shadow
            new Spec((e, ce) => Eff(ce, Priest.PriestEffectIds.ConsumingPlague),   ColorUtil.ToRgba(130, 130, 165,  70), Style.Wisp),  // sickly violet-green
            new Spec((e, ce) => Eff(ce, Priest.PriestEffectIds.SacredFlame),          ColorUtil.ToRgba(140, 255, 155,  45), Style.Wisp),  // ember
            new Spec((e, ce) => Eff(ce, "jagged_blades"),                               ColorUtil.ToRgba(140, 200,  25,  25), Style.Wisp),  // blood
            new Spec((e, ce) => Eff(ce, "poison"),                                 ColorUtil.ToRgba(130, 100, 200,  45), Style.Wisp),  // venom

            // --- Crowd control (overhead ring) ---
            new Spec((e, ce) => Flag(e, CombatFlags.Stunned),                      ColorUtil.ToRgba(220, 255, 235,  70), Style.Overhead, needsEffects: false), // yellow stars
            new Spec((e, ce) => Flag(e, CombatFlags.Feared),                       ColorUtil.ToRgba(200, 185,  70, 210), Style.Overhead, needsEffects: false), // violet
            new Spec((e, ce) => Flag(e, CombatFlags.Silenced),                     ColorUtil.ToRgba(200, 235,  70,  70), Style.Overhead, needsEffects: false), // red
            new Spec((e, ce) => Flag(e, CombatFlags.Rooted),                       ColorUtil.ToRgba(150, 150, 210, 255), Style.Aura,     needsEffects: false), // frost-blue ice at the feet
            new Spec((e, ce) => Flag(e, CombatFlags.Polymorphed),                  ColorUtil.ToRgba(200, 245, 245, 245), Style.Overhead, needsEffects: false), // white puff (sheep)

            // --- Mage ---
            new Spec((e, ce) => Eff(ce, Mage.MageEffectIds.Chilled),               ColorUtil.ToRgba(170, 190, 220, 255), Style.Wisp),  // frost chill
            new Spec((e, ce) => Eff(ce, Mage.MageEffectIds.Ignite),                ColorUtil.ToRgba(140, 255, 150,  40), Style.Wisp),  // fire embers
            new Spec((e, ce) => Eff(ce, Mage.MageEffectIds.VolatileEmber),            ColorUtil.ToRgba(150, 255, 110,  30), Style.Wisp),  // hotter fire
            new Spec((e, ce) => Eff(ce, Mage.MageEffectIds.Conflagration),            ColorUtil.ToRgba( 90, 255, 130,  40), Style.Aura),  // fire aura
            new Spec((e, ce) => Eff(ce, Mage.MageEffectIds.IcyVeins),              ColorUtil.ToRgba( 90, 140, 200, 255), Style.Aura),  // frost aura
            new Spec((e, ce) => Eff(ce, Mage.MageEffectIds.MysticSurge),           ColorUtil.ToRgba( 90, 190,  90, 235), Style.Aura),  // arcane aura

            // --- Shaman ---
            // (Static Shield moved to the orbiting-charge visuals below - its charge count IS the visual.)
            new Spec((e, ce) => Eff(ce, Shaman.ShamanEffectIds.EmberShock),        ColorUtil.ToRgba(140, 255, 140,  40), Style.Wisp),  // fire embers on the burning target
            new Spec((e, ce) => Eff(ce, Shaman.ShamanEffectIds.ElementalSurge),  ColorUtil.ToRgba( 90, 130, 230, 210), Style.Aura),  // teal-violet elemental aura
            // (Spirit Wolf carries NO world particles - the spectral look comes from its own texture, not a shroud.)
            new Spec((e, ce) => Eff(ce, Shaman.ShamanEffectIds.TotemEarth),        ColorUtil.ToRgba( 80, 165, 130,  75), Style.Aura),  // earthen dust
            new Spec((e, ce) => Eff(ce, Shaman.ShamanEffectIds.WarChant),         ColorUtil.ToRgba(110, 235,  60,  50), Style.Aura),  // bloodlust red
            new Spec((e, ce) => Eff(ce, Shaman.ShamanEffectIds.ThunderCleave),       ColorUtil.ToRgba(140, 150, 220, 255), Style.Wisp),  // marked: crackling blue

            // --- Druid ---
            new Spec((e, ce) => Eff(ce, Druid.DruidEffectIds.BarkHide),            ColorUtil.ToRgba( 90, 110,  80,  45), Style.Aura),  // bark-brown defensive shell
            // Heal-over-time (rising sparkles, greens to set them apart from the priest's green-gold Soothing Prayer).
            new Spec((e, ce) => Eff(ce, Druid.DruidEffectIds.VerdantRenewal),        ColorUtil.ToRgba( 90,  95, 220, 110), Style.Heal),  // wild green
            new Spec((e, ce) => Eff(ce, Druid.DruidEffectIds.RestorativeBloom),            ColorUtil.ToRgba( 90, 130, 235, 130), Style.Heal),  // bright regrowth green
            new Spec((e, ce) => Eff(ce, Druid.DruidEffectIds.SpreadingBloom),          ColorUtil.ToRgba( 90, 150, 240, 170), Style.Heal),  // yellow-green bloom
            new Spec((e, ce) => Eff(ce, Druid.DruidEffectIds.LivingBlossom),           ColorUtil.ToRgba(110, 170, 255, 120), Style.Heal),  // vivid bloom green
            // Damage-over-time wisps on the afflicted target.
            new Spec((e, ce) => Eff(ce, Druid.DruidEffectIds.LunarFlare),            ColorUtil.ToRgba(130, 180, 205, 245), Style.Wisp),  // astral moonlight
            new Spec((e, ce) => Eff(ce, Druid.DruidEffectIds.StingingSwarm),         ColorUtil.ToRgba(130, 150, 175,  70), Style.Wisp),  // sickly insect green
            new Spec((e, ce) => Eff(ce, Druid.DruidEffectIds.ClawSlash),                ColorUtil.ToRgba(130, 150, 200,  60), Style.Wisp),  // nature claw
            new Spec((e, ce) => Eff(ce, Druid.DruidEffectIds.DeepRend),                 ColorUtil.ToRgba(140, 200,  40,  40), Style.Wisp),  // heavy bleed (blood)
            new Spec((e, ce) => Eff(ce, Druid.DruidEffectIds.Gash),            ColorUtil.ToRgba(140, 190,  30,  30), Style.Wisp),  // bear bleed (blood)
        };

        // ---- Orbiting-charge visuals: a COUNT of motes circling the bearer (charge shields) ----

        private readonly struct OrbitSpec
        {
            /// <summary>Remaining charges (0 = nothing to draw). The effect's Tier is the charge count for both
            /// shields, so this is just a tier read.</summary>
            public readonly System.Func<Entity, EBEffects?, int> Count;
            public readonly int Color;      // ARGB
            public readonly float Radius;   // orbit radius around the body axis
            public readonly float Height;   // fraction of body height the ring sits at
            /// <summary>Vertical bob amplitude: 0 = a flat ring (grounded, earthy); >0 = each mote rides its own
            /// phase-shifted up/down wave, so the swarm FLIES around the body instead of spinning like a carousel.</summary>
            public readonly float Bob;
            public OrbitSpec(System.Func<Entity, EBEffects?, int> count, int color, float radius, float height, float bob = 0f)
            { Count = count; Color = color; Radius = radius; Height = height; Bob = bob; }
        }

        private static int TierOf(EBEffects? ce, string id)
        {
            int t = ce?.GetEffectTier(id) ?? -1;
            return t < 0 ? 0 : Math.Min(t, 255); // one byte per slot
        }

        // Slot index = byte position in CountsAttr; append-only (each slot is a fixed effect type, so never reorder -
        // one long holds up to 8 slots). Several can ride the same body (a shaman's Static Shield + Maelstrom +
        // a Resto's Stone Ward), hence separate slots, colours and rings.
        private static readonly OrbitSpec[] CountSpecs =
        {
            new OrbitSpec((e, ce) => TierOf(ce, Shaman.ShamanEffectIds.StaticShield),
                ColorUtil.ToRgba(235, 60, 120, 255), 0.75f, 0.55f, bob: 0.35f), // electric-blue orbs weaving around the torso
            new OrbitSpec((e, ce) => TierOf(ce, Shaman.ShamanRestoIds.StoneWard),
                ColorUtil.ToRgba(235, 120, 210, 120), 0.6f, 0.3f), // heavy stones in a flat low ring
            new OrbitSpec((e, ce) => TierOf(ce, Mage.MageEffectIds.MysticCharges),
                ColorUtil.ToRgba(235, 200, 90, 240), 0.7f, 0.6f, bob: 0.3f), // violet arcane motes
            new OrbitSpec((e, ce) => TierOf(ce, Priest.PriestEffectIds.ShadowOrbs),
                ColorUtil.ToRgba(225, 130, 45, 150), 0.62f, 0.68f, bob: 0.4f), // dark shadow orbs, high and weaving
            new OrbitSpec((e, ce) => TierOf(ce, Shaman.ShamanEffectIds.Maelstrom),
                ColorUtil.ToRgba(230, 70, 200, 220), 0.72f, 0.5f, bob: 0.35f), // stormy teal charges
        };

        // ---- Tether links: a persistent line from a bearer to another entity (Stone Ward → the shaman who cast it) ----

        private readonly struct TetherSpec
        {
            /// <summary>The other end's entity id for this bearer (0 = no link). Read from the link effect's snapshot.</summary>
            public readonly System.Func<Entity, EBEffects?, long> AnchorId;
            public readonly SpellSchool School;
            public TetherSpec(System.Func<Entity, EBEffects?, long> anchorId, SpellSchool school)
            { AnchorId = anchorId; School = school; }
        }

        private static long SnapshotCaster(EBEffects? ce, string effectId)
            => ce != null && ce.TryGetEffect(effectId, out var eff) && eff is Effects.SnapshotEffect se ? se.casterId : 0L;

        private static readonly TetherSpec[] TetherSpecs =
        {
            new TetherSpec((e, ce) => SnapshotCaster(ce, Shaman.ShamanRestoIds.StoneWard), SpellSchool.Nature),
        };

        private static bool inited;
        private static long serverTickId;

        // ---- Server: detect states, publish the mask (only on change) ----
        public static void Init()
        {
            if (inited) return;
            inited = true;
            serverTickId = canrpgclassesModSystem.ServerApi?.Event.RegisterGameTickListener(ServerTick, 500) ?? 0;
        }

        /// <summary>Tears down the server tick listener and resets the static guard, so a second world loaded in the
        /// same process (singleplayer re-enter) re-registers on the new ServerApi. Called from the mod's Dispose.</summary>
        public static void Stop()
        {
            var api = canrpgclassesModSystem.ServerApi;
            if (api != null && serverTickId != 0) api.Event.UnregisterGameTickListener(serverTickId);
            serverTickId = 0;
            inited = false;
        }

        private static void ServerTick(float dt)
        {
            var world = canrpgclassesModSystem.ServerApi?.World;
            if (world == null || world.LoadedEntities == null) return;

            foreach (var e in world.LoadedEntities.Values)
            {
                if (e == null || e is not EntityAgent) continue;
                var wa = e.WatchedAttributes;

                long mask = 0;
                long counts = 0;
                long tether = 0;
                int tetherSchool = 0;
                if (e.Alive)
                {
                    var ce = e.GetBehavior<EBEffects>();
                    // Nearly every mob in the world carries zero effects - skip all effect-based detectors for them
                    // outright, so the sweep only pays the handful of direct WA reads (CC flags, pet wrath) per entity.
                    bool hasEffects = ce?.activeEffects != null && ce.activeEffects.Count > 0;
                    for (int i = 0; i < Specs.Length; i++)
                    {
                        if (Specs[i].NeedsEffects && !hasEffects) continue;
                        if (Specs[i].Active(e, ce)) mask |= 1L << i;
                    }
                    if (hasEffects)
                    {
                        for (int i = 0; i < CountSpecs.Length; i++)
                            counts |= (long)CountSpecs[i].Count(e, ce) << (8 * i);
                        // First active tether source wins (an entity carries at most one link in practice). Recomputed
                        // every tick from live effect state, so it's relog-safe unlike a one-shot OnStart write.
                        for (int i = 0; i < TetherSpecs.Length; i++)
                        {
                            long id = TetherSpecs[i].AnchorId(e, ce);
                            if (id != 0 && id != e.EntityId) { tether = id; tetherSchool = (int)TetherSpecs[i].School; break; }
                        }
                    }
                }

                // SetInt/SetLong marks the attribute dirty and re-syncs it - so only write when it actually changed.
                if (tether != wa.GetLong(TetherTargetAttr, 0)) wa.SetLong(TetherTargetAttr, tether);
                if (tetherSchool != wa.GetInt(TetherSchoolAttr, 0)) wa.SetInt(TetherSchoolAttr, tetherSchool);
                if (mask != wa.GetLong(MaskAttr, 0)) wa.SetLong(MaskAttr, mask);
                if (counts != wa.GetLong(CountsAttr, 0)) wa.SetLong(CountsAttr, counts);
            }
        }

        // ---- Client: spawn the particles for a mask, locally ----
        /// <summary>Spawns one visual burst per set bit of <paramref name="mask"/> around the entity. Called by the
        /// client emitter each visual tick; world is the client world, so particles are local (no network).</summary>
        public static void EmitMask(IWorldAccessor world, Entity e, long mask)
        {
            for (int i = 0; i < Specs.Length; i++)
                if ((mask & (1L << i)) != 0) Emit(world, e, Specs[i].Style, Specs[i].Color);
        }

        /// <summary>Draws N orbiting charge orbs per <see cref="CountsAttr"/> slot. Nothing actually moves: each
        /// call spawns a large motionless particle at the orbit angle for the current time, so it reads as a solid
        /// ball instead of the streak a moving particle would leave. Needs a fast (~60ms) caller tick.</summary>
        public static void EmitOrbits(IWorldAccessor world, Entity e, long counts)
        {
            const float omega = 1.8f;  // rad/s: one lap every ~3.5s
            const float life = 0.12f;  // ~2x the 60ms tick, so exactly one ball is visible per charge at a time
            double t = world.ElapsedMilliseconds / 1000.0;
            double baseAngle = t * omega;
            var box = e.SelectionBox;

            for (int slot = 0; slot < CountSpecs.Length; slot++)
            {
                int n = (int)((counts >> (8 * slot)) & 0xFF);
                if (n == 0) continue;
                var spec = CountSpecs[slot];
                double midY = e.Pos.Y + box.Y2 * spec.Height;

                for (int i = 0; i < n; i++)
                {
                    double phase = Math.PI * 2.0 * i / n + slot * 0.9; // even spacing + slot offset so rings don't align
                    double ang = baseAngle + phase;
                    double y = midY + spec.Bob * Math.Sin(t * 2.3 + phase * 1.7);
                    var at = new Vec3d(e.Pos.X + Math.Cos(ang) * spec.Radius, y, e.Pos.Z + Math.Sin(ang) * spec.Radius);

                    var p = new SimpleParticleProperties(1, 1, spec.Color, at, at,
                        new Vec3f(0f, 0f, 0f), new Vec3f(0f, 0f, 0f),
                        life, 0f, 0.45f, 0.55f, EnumParticleModel.Quad);
                    p.MinPos.Set(at);
                    p.AddPos.Set(0, 0, 0);
                    p.VertexFlags = 255;
                    world.SpawnParticles(p);
                }
            }
        }

        // (The channel beam is drawn by the client-side BeamRenderer as a solid ribbon mesh - the Beam* attributes
        // above are its data feed; there is no particle path for it.)

        private static void Emit(IWorldAccessor world, Entity e, Style style, int color)
        {
            var box = e.SelectionBox;
            var rnd = world.Rand;
            Vec3d center = e.Pos.XYZ.Add(0, box.Y2 * 0.5, 0);
            double rxz = Math.Max(box.XSize, box.ZSize) * 0.5 + 0.3;
            double ry = box.Y2 * 0.5 + 0.2;

            int count; float size; float life; Vec3f vmin, vmax;
            switch (style)
            {
                case Style.Wisp:     count = 2; size = 0.18f; life = 0.7f; vmin = new Vec3f(-0.03f, 0.0f, -0.03f); vmax = new Vec3f(0.03f, 0.06f, 0.03f); break;
                case Style.Heal:     count = 2; size = 0.25f; life = 0.8f; vmin = new Vec3f(-0.02f, 0.10f, -0.02f); vmax = new Vec3f(0.02f, 0.28f, 0.02f); break;
                case Style.Overhead: count = 3; size = 0.22f; life = 0.6f; vmin = new Vec3f(-0.02f, 0.0f, -0.02f); vmax = new Vec3f(0.02f, 0.03f, 0.02f); break;
                case Style.Front:    count = 6; size = 0.26f; life = 0.6f; vmin = new Vec3f(-0.01f, 0.0f, -0.01f); vmax = new Vec3f(0.01f, 0.02f, 0.01f); break;
                default:             count = 3; size = 0.30f; life = 0.6f; vmin = new Vec3f(-0.05f, 0.0f, -0.05f); vmax = new Vec3f(0.05f, 0.10f, 0.05f); break; // Aura
            }

            // Facing basis (for a front-only shield panel): forward = look direction (flattened), right = perpendicular.
            double yaw = e.Pos.Yaw;
            double fwdX = Math.Sin(yaw), fwdZ = Math.Cos(yaw);
            double rgtX = Math.Cos(yaw), rgtZ = -Math.Sin(yaw);

            for (int i = 0; i < count; i++)
            {
                Vec3d at;
                if (style == Style.Overhead)
                {
                    double ang = rnd.NextDouble() * Math.PI * 2.0;
                    double rr = 0.35;
                    at = new Vec3d(e.Pos.X + Math.Cos(ang) * rr, e.Pos.Y + box.Y2 + 0.35, e.Pos.Z + Math.Sin(ang) * rr);
                }
                else if (style == Style.Wisp)
                {
                    at = center.AddCopy((rnd.NextDouble() - 0.5) * rxz, (rnd.NextDouble() - 0.5) * ry, (rnd.NextDouble() - 0.5) * rxz);
                }
                else if (style == Style.Heal)
                {
                    at = new Vec3d(e.Pos.X + (rnd.NextDouble() - 0.5) * rxz, e.Pos.Y + box.Y2 * 0.3, e.Pos.Z + (rnd.NextDouble() - 0.5) * rxz);
                }
                else if (style == Style.Front)
                {
                    // A flat panel in front of the body (a raised shield): spread across the facing width + full
                    // height, pushed forward of the torso, with only a thin depth.
                    double w = (rnd.NextDouble() - 0.5) * (rxz * 2.2);   // across the shield face
                    double h = (rnd.NextDouble() - 0.5) * box.Y2;        // full body height
                    double f = (rxz + 0.15) + (rnd.NextDouble() - 0.5) * 0.1; // in front, thin depth
                    at = new Vec3d(center.X + fwdX * f + rgtX * w, e.Pos.Y + box.Y2 * 0.5 + h, center.Z + fwdZ * f + rgtZ * w);
                }
                else // Aura: point on the body ellipsoid
                {
                    double u = rnd.NextDouble() * 2.0 - 1.0;
                    double phi = rnd.NextDouble() * Math.PI * 2.0;
                    double sc = Math.Sqrt(Math.Max(0.0, 1.0 - u * u));
                    at = new Vec3d(center.X + sc * Math.Cos(phi) * rxz, center.Y + u * ry, center.Z + sc * Math.Sin(phi) * rxz);
                }

                var p = new SimpleParticleProperties(1, 1, color, at, at, vmin, vmax, life, 0f, size, size * 2f, EnumParticleModel.Quad);
                p.MinPos.Set(at);
                p.AddPos.Set(0, 0, 0);
                p.VertexFlags = 255; // self-lit (glow)
                world.SpawnParticles(p);
            }
        }
    }
}
