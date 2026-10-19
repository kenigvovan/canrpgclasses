using System;
using System.Collections.Generic;
using canrpgclasses.Core.Spells;
using Vintagestory.API.Client;

namespace canrpgclasses.Client
{
    /// <summary>Looping sound of a cast bar or channel; follows the caster until it ends or is cancelled.</summary>
    public class SpellCastSounds
    {
        private readonly ICoreClientAPI capi;
        private readonly long tickId;
        private readonly Dictionary<long, (ILoadedSound Sound, long EndMs)> active = new();
        private readonly List<long> done = new();
        private readonly Func<float> userVolume;

        public SpellCastSounds(ICoreClientAPI capi, Func<float> userVolume)
        {
            this.capi = capi;
            this.userVolume = userVolume;
            tickId = capi.Event.RegisterGameTickListener(OnTick, 100);
        }

        public void Start(long casterId, Spell spell, float seconds)
        {
            Stop(casterId);
            if (SpellSounds.Resolve(spell, SpellSounds.Slot.Cast) is not { } s) return;
            float volume = s.Volume * userVolume();
            var caster = capi.World.GetEntityById(casterId);
            if (volume <= 0f || caster == null) return;

            var sound = capi.World.LoadSound(new SoundParams
            {
                Location = s.Location.Clone().WithPathAppendixOnce(".ogg"),
                ShouldLoop = true,
                DisposeOnFinish = false,
                Position = caster.Pos.XYZ.ToVec3f(),
                Volume = volume,
                Range = 24f
            });
            if (sound == null) return;
            sound.Start();
            active[casterId] = (sound, capi.World.ElapsedMilliseconds + (long)(seconds * 1000f));
        }

        public void Stop(long casterId)
        {
            if (!active.Remove(casterId, out var a)) return;
            a.Sound.FadeOut(0.25f, s => s.Dispose());
        }

        private void OnTick(float dt)
        {
            long now = capi.World.ElapsedMilliseconds;
            foreach (var kv in active)
            {
                var caster = capi.World.GetEntityById(kv.Key);
                if (caster == null || !caster.Alive || now >= kv.Value.EndMs) done.Add(kv.Key);
                else kv.Value.Sound.SetPosition(caster.Pos.XYZ.ToVec3f());
            }
            foreach (var id in done) Stop(id);
            done.Clear();
        }

        public void Dispose()
        {
            capi.Event.UnregisterGameTickListener(tickId);
            foreach (var a in active.Values) a.Sound.Dispose();
            active.Clear();
        }
    }
}
