using System.Collections.Generic;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Net;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace canrpgclasses.Core.Portals
{
    public enum PortalKind : byte { Gate = 0, Waypoint = 1, Summon = 2 }

    /// <summary>Server-side portal pairs: players walking into one end come out of the other. Temporary, not
    /// persisted; one pair per owner and kind. Server main thread only.</summary>
    public static class PortalManager
    {
        private sealed class Pair
        {
            public long Id;
            public Entity Owner = null!;
            public PortalKind Kind;
            public int Dim;
            public Vec3d[] Foot = new Vec3d[2];
            public float[] Yaw = new float[2];
            public long ExpiresMs;
            public int TintA, TintB;
            public byte[]? ImageA;
            /// <summary>Who may pass; null = every player.</summary>
            public HashSet<string>? Allowed;
            public readonly HashSet<string> Known = new();
        }

        private const double SyncRange = 128;
        private const long PassCooldownMs = 1000;
        private const string PassSound = "game:sounds/effect/translocate-breakdimension";

        private static readonly List<Pair> pairs = new();
        // The end a player just came out of: ignored until they step out of it.
        private static readonly Dictionary<string, (long id, int end)> arrived = new();
        private static readonly Dictionary<string, long> passCooldownUntil = new();
        private static ICoreServerAPI? sapi;
        private static long tickId, nextId, nextSyncMs;

        public static void Start(ICoreServerAPI api)
        {
            sapi = api;
            tickId = api.Event.RegisterGameTickListener(OnTick, 250);
        }

        public static void Stop()
        {
            if (sapi != null && tickId != 0) sapi.Event.UnregisterGameTickListener(tickId);
            tickId = 0;
            sapi = null;
            pairs.Clear();
            arrived.Clear();
            passCooldownUntil.Clear();
        }

        /// <summary>Opens a pair, replacing the owner's previous one of the same kind. Yaws are snapped to an axis.</summary>
        public static void Open(Entity owner, PortalKind kind, Vec3d footA, float yawA, Vec3d footB, float yawB,
            float seconds, int tintA, int tintB, byte[]? imageA = null, HashSet<string>? allowed = null)
        {
            if (sapi == null) return;
            Close(owner, kind);

            var p = new Pair
            {
                Id = ++nextId,
                Owner = owner,
                Kind = kind,
                Dim = owner.Pos.Dimension,
                ExpiresMs = sapi.World.ElapsedMilliseconds + (long)(seconds * 1000f),
                TintA = tintA,
                TintB = tintB,
                ImageA = imageA,
                Allowed = allowed
            };
            p.Foot[0] = footA; p.Yaw[0] = PortalGeometry.SnapYaw(yawA);
            p.Foot[1] = footB; p.Yaw[1] = PortalGeometry.SnapYaw(yawB);
            pairs.Add(p);

            for (int i = 0; i < 2; i++) PlaySound(p.Foot[i]);
            Sync(p);
        }

        public static void Close(Entity owner, PortalKind kind)
        {
            for (int i = pairs.Count - 1; i >= 0; i--)
                if (pairs[i].Owner == owner && pairs[i].Kind == kind) CloseAt(i);
        }

        /// <summary>On disconnect: a rejoining client has no portals, so it must be sent them again.</summary>
        public static void Forget(string uid)
        {
            foreach (var p in pairs) p.Known.Remove(uid);
            arrived.Remove(uid);
            passCooldownUntil.Remove(uid);
        }

        private static void CloseAt(int index)
        {
            var p = pairs[index];
            pairs.RemoveAt(index);
            if (sapi == null || p.Known.Count == 0) return;

            var packet = new PortalStatePacket { Id = p.Id, Open = false };
            var channel = canrpgclassesModSystem.For(sapi)?.ServerChannel;
            foreach (var uid in p.Known)
                if (sapi.World.PlayerByUid(uid) is IServerPlayer sp && sp.ConnectionState == EnumClientState.Playing)
                    channel?.SendPacket(packet, sp);
        }

        /// <summary>A spot near <paramref name="p"/> where <paramref name="entity"/>'s body fits, stepping back along
        /// <paramref name="back"/> out of walls and settling onto the ground below. Null if nothing fits.</summary>
        public static Vec3d? FindStandSpot(EntityAgent entity, Vec3d p, Vec3d back)
        {
            var world = entity.World;
            var ct = world.CollisionTester;
            var ba = world.BlockAccessor;
            var box = entity.CollisionBox;
            if (box == null) return p;

            for (double d = 0; d <= 2.0; d += 0.25)
            {
                for (int dy = 0; dy <= 2; dy++)
                {
                    var cand = new Vec3d(p.X + back.X * d, p.Y + dy, p.Z + back.Z * d);
                    if (ct.IsColliding(ba, box, cand, false)) continue;

                    // Settle onto the ground, at most 4 blocks down.
                    for (int i = 0; i < 40; i++)
                    {
                        var below = cand.AddCopy(0, -0.1, 0);
                        if (ct.IsColliding(ba, box, below, false)) break;
                        cand = below;
                    }
                    return cand;
                }
            }
            return null;
        }

        private static void OnTick(float dt)
        {
            if (sapi == null || pairs.Count == 0) return;
            long now = sapi.World.ElapsedMilliseconds;

            for (int i = pairs.Count - 1; i >= 0; i--)
            {
                var p = pairs[i];
                if (now >= p.ExpiresMs || !p.Owner.Alive || p.Owner.State == EnumEntityState.Despawned) CloseAt(i);
            }

            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player?.Entity is not EntityPlayer e || !e.Alive || player.PlayerUID == null) continue;
                TryPass(e, player.PlayerUID, now);
            }

            if (now >= nextSyncMs)
            {
                nextSyncMs = now + 2000;
                foreach (var p in pairs) Sync(p);
            }
        }

        private static void TryPass(EntityPlayer e, string uid, long now)
        {
            var pos = e.Pos;

            if (arrived.TryGetValue(uid, out var last))
            {
                var lp = pairs.Find(x => x.Id == last.id);
                if (lp != null && lp.Dim == pos.Dimension && PortalGeometry.Contains(lp.Foot[last.end], lp.Yaw[last.end], pos.X, pos.Y, pos.Z))
                    return;
                arrived.Remove(uid);
            }
            if (passCooldownUntil.TryGetValue(uid, out long until) && now < until) return;

            foreach (var p in pairs)
            {
                if (p.Dim != pos.Dimension || (p.Allowed != null && !p.Allowed.Contains(uid))) continue;
                for (int end = 0; end < 2; end++)
                {
                    if (!PortalGeometry.Contains(p.Foot[end], p.Yaw[end], pos.X, pos.Y, pos.Z)) continue;
                    int other = 1 - end;
                    Teleport(e, p.Foot[other], p.Yaw[other]);
                    arrived[uid] = (p.Id, other);
                    passCooldownUntil[uid] = now + PassCooldownMs;
                    return;
                }
            }
        }

        private static void Teleport(EntityPlayer e, Vec3d foot, float yaw)
        {
            // One block out of the plane if there is room, else the plane itself (the arrival guard stops a bounce).
            var fwd = PortalGeometry.Forward(yaw);
            Vec3d dest = foot.AddCopy(fwd.X, 0, fwd.Z);
            var box = e.CollisionBox;
            if (box != null && e.World.CollisionTester.IsColliding(e.World.BlockAccessor, box, dest, false)) dest = foot;

            PlaySound(e.Pos.XYZ);
            var epos = e.Pos.Copy();
            epos.SetPos(dest.X, dest.Y, dest.Z);
            epos.Yaw = yaw;
            e.TeleportTo(epos);
            SpellExecutor.SendViewYaw(e, yaw);
            PlaySound(dest);
        }

        private static void PlaySound(Vec3d at)
            => sapi?.World.PlaySoundAt(new AssetLocation(PassSound), at.X, at.Y, at.Z, null, true, 24f);

        /// <summary>Sends the pair to players in range who haven't got it yet.</summary>
        private static void Sync(Pair p)
        {
            if (sapi == null) return;
            PortalStatePacket? packet = null;
            var channel = canrpgclassesModSystem.For(sapi)?.ServerChannel;
            double r2 = SyncRange * SyncRange;

            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player is not IServerPlayer sp || sp.ConnectionState != EnumClientState.Playing || sp.Entity == null) continue;
                if (p.Known.Contains(sp.PlayerUID)) continue;
                var pos = sp.Entity.Pos;
                if (pos.Dimension != p.Dim) continue;
                if (pos.SquareDistanceTo(p.Foot[0]) > r2 && pos.SquareDistanceTo(p.Foot[1]) > r2) continue;

                packet ??= new PortalStatePacket
                {
                    Id = p.Id, Open = true, Dim = p.Dim,
                    AX = p.Foot[0].X, AY = p.Foot[0].Y, AZ = p.Foot[0].Z, AYaw = p.Yaw[0],
                    BX = p.Foot[1].X, BY = p.Foot[1].Y, BZ = p.Foot[1].Z, BYaw = p.Yaw[1],
                    TintA = p.TintA, TintB = p.TintB, ImageA = p.ImageA
                };
                channel?.SendPacket(packet, sp);
                p.Known.Add(sp.PlayerUID);
            }
        }
    }
}
