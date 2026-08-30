using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using canrpgclasses.Core.Net;

namespace canrpgclasses.Core.EB
{
    /// <summary>
    /// Per-spell / per-group cooldowns. Server-authoritative, mirrored on the client for HUD display; remaining
    /// time is persisted so it survives a relog.
    /// </summary>
    public class EBSpellCooldowns : EntityBehavior
    {
        public const string Name = "canrpgspellcooldowns";
        private const string AttrKey = Core.AttrKeys.Cooldowns;

        private readonly Dictionary<string, long> expiryMs = new Dictionary<string, long>();
        private readonly Dictionary<string, float> totalSeconds = new Dictionary<string, float>();

        public EBSpellCooldowns(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        private long NowMs => entity.World.ElapsedMilliseconds;
        private bool IsServer => entity.Api.Side == EnumAppSide.Server;

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            if (IsServer) Deserialize();
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);
            if (IsServer) Serialize();
        }

        public bool IsOnCooldown(string key) => expiryMs.TryGetValue(key, out var end) && end > NowMs;

        public float RemainingSeconds(string key)
        {
            if (!expiryMs.TryGetValue(key, out var end)) return 0;
            float rem = (end - NowMs) / 1000f;
            return rem > 0 ? rem : 0;
        }

        public float TotalSeconds(string key) => totalSeconds.TryGetValue(key, out var t) ? t : 0;

        public void SetCooldown(string key, float seconds, bool sync = true, float total = -1)
        {
            if (seconds <= 0) return;
            expiryMs[key] = NowMs + (long)(seconds * 1000f);
            totalSeconds[key] = total > 0 ? total : seconds;
            if (sync && IsServer) SyncOne(key);
        }

        /// <summary>Clears a cooldown (Regroup reset). On the server this pushes a Remaining=0 packet so the
        /// owner's HUD overlay drops immediately. On the client it just removes the local entry.</summary>
        public void ClearCooldown(string key, bool sync = true)
        {
            bool had = expiryMs.Remove(key);
            totalSeconds.Remove(key);
            if (had && sync && IsServer)
            {
                var (player, mod) = Owner();
                if (player != null && mod?.ServerChannel != null)
                    mod.ServerChannel.SendPacket(new SpellCooldownPacket { Key = key, Duration = 0, Remaining = 0 }, player);
            }
        }

        private void Serialize()
        {
            var snapshot = new Dictionary<string, float>();
            foreach (var kv in expiryMs)
            {
                float rem = (kv.Value - NowMs) / 1000f;
                if (rem > 0) snapshot[kv.Key] = rem;
            }
            entity.WatchedAttributes.SetString(AttrKey, JsonConvert.SerializeObject(snapshot));
        }

        private void Deserialize()
        {
            if (!entity.WatchedAttributes.HasAttribute(AttrKey)) return;
            var snapshot = JsonConvert.DeserializeObject<Dictionary<string, float>>(entity.WatchedAttributes.GetString(AttrKey));
            if (snapshot == null) return;
            foreach (var kv in snapshot) SetCooldown(kv.Key, kv.Value, sync: false);
        }

        private void SyncOne(string key)
        {
            var (player, mod) = Owner();
            if (player == null || mod?.ServerChannel == null) return;
            mod.ServerChannel.SendPacket(new SpellCooldownPacket
            {
                Key = key,
                Duration = TotalSeconds(key),
                Remaining = RemainingSeconds(key)
            }, player);
        }

        public void SyncAll()
        {
            var (player, mod) = Owner();
            if (player == null || mod?.ServerChannel == null) return;

            var packet = new SpellCooldownSyncPacket();
            foreach (var key in expiryMs.Keys)
            {
                if (RemainingSeconds(key) <= 0) continue;
                packet.Cooldowns.Add(new SpellCooldownPacket
                {
                    Key = key,
                    Duration = TotalSeconds(key),
                    Remaining = RemainingSeconds(key)
                });
            }
            mod.ServerChannel.SendPacket(packet, player);
        }

        private (IServerPlayer?, canrpgclassesModSystem?) Owner()
        {
            if (!IsServer) return (null, null);
            var player = (entity as EntityPlayer)?.Player as IServerPlayer;
            // For(api) is side-correct by construction: on the server side it always yields the server
            // instance (whose ServerChannel is set), never the client one.
            return (player, canrpgclassesModSystem.For(entity.Api));
        }
    }
}
