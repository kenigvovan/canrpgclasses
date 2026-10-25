using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using canrpgclasses.Core;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Control;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Portals;
using canrpgclasses.Core.Progression;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter
{
    /// <summary>Tame Beast: a channel on a weakened wild creature. Progress lives on the creature and resets if the
    /// channel breaks; the last tick removes it and adds its species to the hunter's stable.</summary>
    public static class PetTaming
    {
        private const string ByKey = "canrpgTameBy";
        private const string TicksKey = "canrpgTameTicks";
        private const string LastKey = "canrpgTameLast";

        public static void Init()
        {
            SpellExecutor.RegisterImpact(ImpactAction.TameTick, Tick);
            SpellExecutor.RegisterPrecheck(ImpactAction.TameTick, (c, s, imp, aim) =>
                Check(c, SpellExecutor.GetAimedEntity(c, aim, s.Range > 0 ? s.Range : 6f), out _));
        }

        /// <summary>Why this creature can't be tamed by this hunter, or null (with its species) if it can.</summary>
        private static string? Check(EntityAgent caster, Entity? target, out string species)
        {
            species = "";
            var world = caster.World;
            int level = caster.GetBehavior<EBProgression>()?.Level ?? 1;
            int unlock = (int)BalanceConfig.Global("tameUnlockLevel", 10f);
            if (level < unlock) return Msg("tame-level", unlock);
            if (target == null || !target.Alive) return Msg("tame-notarget");

            var found = PetSpecies.ForWild(world, target);
            if (found == null) return Msg("tame-cannot");
            species = found;

            // Bred, owned or another hunter's: off limits.
            var wa = target.WatchedAttributes;
            if (wa.GetInt("generation", 0) > 0 || wa.HasAttribute("ownedby") || wa.HasAttribute(AttrKeys.GuardedByPlayerUid))
                return Msg("tame-owned");

            int need = PetSpecies.MinTameLevel(world, species);
            if (level < need) return Msg("tame-level", need);

            var health = wa.GetTreeAttribute("health");
            float max = health?.GetFloat("maxhealth", 0f) ?? 0f;
            float frac = max > 0f ? health!.GetFloat("currenthealth", max) / max : 1f;
            float allowed = BalanceConfig.Global("tameMaxHealthFraction", 0.35f);
            if (frac > allowed) return Msg("tame-weaken", (int)System.Math.Round(allowed * 100f));

            if (PetStable.IsFull(caster)) return Msg("tame-full", PetStable.MaxSlots(caster));
            return null;
        }

        private static void Tick(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var caster = ctx.Caster;
            if (Check(caster, target, out string species) is string refusal)
            {
                PortalSpells.Tell(caster, refusal);
                RpgCombatApi.Interrupt(caster);
                return;
            }

            var wa = target.WatchedAttributes;
            long now = target.World.ElapsedMilliseconds;
            float interval = ctx.Spell.ChannelTicks > 1 ? ctx.Spell.CastDuration / (ctx.Spell.ChannelTicks - 1) : 1f;
            bool continuing = wa.GetLong(ByKey, 0) == caster.EntityId && now - wa.GetLong(LastKey, 0) <= (long)(interval * 1000f) + 1500;
            int ticks = (continuing ? wa.GetInt(TicksKey, 0) : 0) + 1;
            wa.SetLong(ByKey, caster.EntityId);
            wa.SetLong(LastKey, now);
            wa.SetInt(TicksKey, ticks);

            // One stun for the whole channel: repeated stuns would hit diminishing returns. Damage breaks it, so an
            // abandoned taming isn't free crowd control.
            if (ticks == 1)
                ControlState.Apply(target, ControlType.Stun, ctx.Spell.CastDuration + 1f, caster, breakOnDamage: true);

            BondParticles(ticks / (float)ctx.Spell.ChannelTicks).Emit(target.World, target);
            if (ticks < ctx.Spell.ChannelTicks) return;

            Complete(caster, target, species);
        }

        private static void Complete(EntityAgent caster, Entity wild, string species)
        {
            // The pet wears the coat the wild creature had: same texture list, same index.
            int color = wild.WatchedAttributes.GetInt("textureIndex", 0);
            if (color < 0 || color >= PetSpecies.ColorCount(caster.World, species)) color = 0;
            if (PetStable.Add(caster, species, color) < 0) { PortalSpells.Tell(caster, Msg("tame-full", PetStable.MaxSlots(caster))); return; }

            ControlState.BreakStun(wild);
            var at = wild.Pos.Copy();
            BondBurst.Emit(wild.World, wild);
            wild.World.PlaySoundAt(new AssetLocation("game:sounds/effect/deepbell"), wild, null, true, 24f);
            wild.Die(EnumDespawnReason.Removed); // no drops, no corpse

            // The tamed beast takes the wild one's place.
            HunterPetSystem.DismissActive(caster);
            HunterPetSystem.SummonPet(caster, at: at);
            bool rare = PetSpecies.IsRareColor(caster.World, species, color);
            PortalSpells.Tell(caster, Msg(rare ? "tame-success-rare" : "tame-success", Lang.Get("canrpgclasses:pet-look-" + species)));
            PetJournal.Record(caster, species, color);
        }

        // Warm motes that thicken and grow as the bond forms.
        private static ParticleSpec BondParticles(float progress) => new ParticleSpec
        {
            ColorA = 220, ColorR = 255, ColorG = 205, ColorB = 120, Glow = true,
            MinQuantity = 3f + 12f * progress, AddQuantity = 3f + 6f * progress,
            MinSize = 0.15f, MaxSize = 0.3f + 0.3f * progress,
            LifeLength = 1.2f, Gravity = -0.05f, VelocityHoriz = 0.25f, VelocityY = 0.5f
        };

        private static readonly ParticleSpec BondBurst = new ParticleSpec
        {
            ColorA = 235, ColorR = 255, ColorG = 220, ColorB = 140, Glow = true,
            MinQuantity = 40f, AddQuantity = 20f, MinSize = 0.25f, MaxSize = 0.7f,
            LifeLength = 1.5f, Gravity = -0.1f, VelocityHoriz = 1.5f, VelocityY = 1.5f
        };

        private static string Msg(string key, params object[] args) => Lang.Get("canrpgclasses:msg-" + key, args);
    }
}
