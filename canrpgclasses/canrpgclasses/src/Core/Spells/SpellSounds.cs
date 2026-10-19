using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using canrpgclasses.Core.Net;

namespace canrpgclasses.Core.Spells
{
    /// <summary>Spell sounds from <c>config/sounds.json</c>: a set per school, overridable per spell.</summary>
    public static class SpellSounds
    {
        public enum Slot { Cast, Release, Impact }

        private const string AssetFile = "config/sounds.json";
        private const int Range = 32;

        private static volatile FileModel file = new FileModel();

        public static void Load(ICoreAPI api)
        {
            var asset = api.Assets.TryGet(new AssetLocation(canrpgclassesModSystem.ModId, AssetFile));
            try { file = asset?.ToObject<FileModel>() ?? new FileModel(); }
            catch (Exception e) { api.Logger.Warning("[canrpgclasses] bad sounds config: {0}", e.Message); }
        }

        /// <summary>The sound for this slot, or null for silence.</summary>
        public static (AssetLocation Location, float Volume)? Resolve(Spell spell, Slot slot)
        {
            var f = file;
            SetModel? own = null;
            f.spells?.TryGetValue(spell.LocalId, out own);

            var (path, volume) = own?.Get(slot) ?? default;
            if (path == null)
            {
                var fallback = Fallback(f, spell, own?.@as, slot);
                path = fallback.Path;
                volume ??= fallback.Volume;
            }
            if (string.IsNullOrEmpty(path)) return null;

            float global = f.volume != null && f.volume.TryGetValue(slot.ToString().ToLowerInvariant(), out var g) ? g : 1f;
            return (new AssetLocation(path), volume ?? global);
        }

        /// <summary>Server: sent to nearby players instead of PlaySoundAt, so each client applies its own volume.</summary>
        public static void Play(IWorldAccessor world, Spell spell, Slot slot, Vec3d at)
        {
            if (Resolve(spell, slot) is not { } s) return;
            var channel = canrpgclassesModSystem.For(world.Api)?.ServerChannel;
            if (channel == null) return;

            var packet = new SpellSoundPacket { Location = s.Location.ToString(), Volume = s.Volume, X = at.X, Y = at.Y, Z = at.Z };
            foreach (var p in world.GetPlayersAround(at, Range, Range))
                if (p is IServerPlayer sp) channel.SendPacket(packet, sp);
        }

        public static void PlayLocal(IWorldAccessor world, SpellSoundPacket p, float userVolume)
        {
            float v = p.Volume * userVolume;
            if (v <= 0f || string.IsNullOrEmpty(p.Location)) return;
            world.PlaySoundAt(new AssetLocation(p.Location), p.X, p.Y, p.Z, null, true, Range, v);
        }

        // Physical skills stay silent unless they hit or throw something; the impact follows what the spell does.
        private static (string? Path, float? Volume) Fallback(FileModel f, Spell spell, string? schoolOverride, Slot slot)
        {
            string school = string.IsNullOrEmpty(schoolOverride) ? spell.School.ToString() : schoolOverride!;
            bool physical = school.StartsWith("Physical", StringComparison.Ordinal);
            SetModel? set = null;
            f.schools?.TryGetValue(school, out set);
            var own = set?.Get(slot) ?? default;

            bool damage = spell.Impacts.Exists(i => i.Action is ImpactAction.Damage or ImpactAction.DamageZone);
            switch (slot)
            {
                case Slot.Release:
                    return physical && !damage && spell.Deliver.Type != DeliveryType.Projectile ? default : own;
                case Slot.Impact:
                    if (damage) return own;
                    if (spell.Impacts.Exists(i => i.Action == ImpactAction.Heal)) return (f.heal, null);
                    if (spell.Impacts.Exists(i => i.Action == ImpactAction.Shield)) return (f.shield, null);
                    return physical ? default : own;
                default:
                    return physical ? default : own;
            }
        }

        private class SetModel
        {
            public string? @as { get; set; }
            public string? cast { get; set; }
            public string? release { get; set; }
            public string? impact { get; set; }
            public float? castVolume { get; set; }
            public float? releaseVolume { get; set; }
            public float? impactVolume { get; set; }

            public (string? Path, float? Volume) Get(Slot slot) => slot switch
            {
                Slot.Cast => (cast, castVolume),
                Slot.Release => (release, releaseVolume),
                _ => (impact, impactVolume)
            };
        }

        private class FileModel
        {
            public string? _comment { get; set; }
            public Dictionary<string, float>? volume { get; set; }
            public string? heal { get; set; }
            public string? shield { get; set; }
            public Dictionary<string, SetModel>? schools { get; set; }
            public Dictionary<string, SetModel>? spells { get; set; }
        }
    }
}
