using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using ProtoBuf;
using Vintagestory.API.Server;

namespace canrpgclasses.Core.Portals
{
    /// <summary>server → client: take a destination snapshot for this key.</summary>
    [ProtoContract]
    public class PortalShotRequestPacket
    {
        [ProtoMember(1)] public string Key = "";
    }

    /// <summary>client → server: the snapshot (<see cref="PortalShots"/> format).</summary>
    [ProtoContract]
    public class PortalShotPacket
    {
        [ProtoMember(1)] public string Key = "";
        [ProtoMember(2)] public byte[]? Data;
    }

    /// <summary>Destination snapshots shown in Waypoint portals: a fixed-size RGB image, deflated.
    /// Keys: "a:&lt;point name&gt;" for admin points, "p" for the sender's own anchor.</summary>
    public static class PortalShots
    {
        public const int Width = 128, Height = 192;
        public const int MaxBytes = Width * Height * 3;
        public const string PersonalKey = "p";
        public const string AdminPrefix = "a:";

        /// <summary>Center crop at the portal's 2:3 aspect, box-averaged down. <paramref name="bgra"/> is top-down
        /// 0xAARRGGBB, as a flipped screenshot gives it.</summary>
        public static byte[] Encode(int[] bgra, int srcW, int srcH)
        {
            int cropH = srcH, cropW = srcH * Width / Height;
            if (cropW > srcW) { cropW = srcW; cropH = srcW * Height / Width; }
            int x0 = (srcW - cropW) / 2, y0 = (srcH - cropH) / 2;

            var rgb = new byte[Width * Height * 3];
            for (int y = 0; y < Height; y++)
            {
                int sy0 = y0 + y * cropH / Height, sy1 = Math.Max(sy0 + 1, y0 + (y + 1) * cropH / Height);
                for (int x = 0; x < Width; x++)
                {
                    int sx0 = x0 + x * cropW / Width, sx1 = Math.Max(sx0 + 1, x0 + (x + 1) * cropW / Width);
                    long r = 0, g = 0, b = 0; int n = 0;
                    for (int sy = sy0; sy < sy1; sy++)
                        for (int sx = sx0; sx < sx1; sx++)
                        {
                            int c = bgra[sy * srcW + sx];
                            r += (c >> 16) & 0xFF; g += (c >> 8) & 0xFF; b += c & 0xFF; n++;
                        }
                    int i = (y * Width + x) * 3;
                    rgb[i] = (byte)(r / n); rgb[i + 1] = (byte)(g / n); rgb[i + 2] = (byte)(b / n);
                }
            }

            using var ms = new MemoryStream();
            using (var z = new DeflateStream(ms, CompressionLevel.Optimal)) z.Write(rgb, 0, rgb.Length);
            return ms.ToArray();
        }

        /// <summary>Back to top-down 0xFFRRGGBB pixels, or null if the data is malformed.</summary>
        public static int[]? Decode(byte[]? data)
        {
            if (data == null || data.Length == 0) return null;
            var rgb = new byte[MaxBytes];
            try
            {
                using var z = new DeflateStream(new MemoryStream(data), CompressionMode.Decompress);
                int read = 0, got;
                while (read < rgb.Length && (got = z.Read(rgb, read, rgb.Length - read)) > 0) read += got;
                if (read != rgb.Length) return null;
            }
            catch (InvalidDataException) { return null; }

            var px = new int[Width * Height];
            for (int i = 0; i < px.Length; i++)
                px[i] = unchecked((int)0xFF000000) | (rgb[i * 3] << 16) | (rgb[i * 3 + 1] << 8) | rgb[i * 3 + 2];
            return px;
        }

        /// <summary>Server-side sanity check before storing a client's upload.</summary>
        public static bool Valid(byte[]? data) => data != null && data.Length > 0 && data.Length <= MaxBytes && Decode(data) != null;

        // ---- server: ask a client for a snapshot and store what comes back ----

        /// <summary>Entity attribute (saved, not synced) holding the personal anchor's snapshot.</summary>
        public const string PersonalShotAttr = "canrpgPortalAnchorShot";

        // Only a requested upload is accepted, within this window.
        private const long RequestWindowMs = 15000;
        private static readonly Dictionary<string, (string key, long until)> pending = new();

        public static void Request(IServerPlayer player, string key)
        {
            if (player.Entity == null) return;
            pending[player.PlayerUID] = (key, Environment.TickCount64 + RequestWindowMs);
            canrpgclassesModSystem.For(player.Entity.Api)?.ServerChannel?.SendPacket(new PortalShotRequestPacket { Key = key }, player);
        }

        public static void OnUpload(IServerPlayer from, PortalShotPacket p)
        {
            if (from?.Entity == null || !pending.TryGetValue(from.PlayerUID, out var req)) return;
            pending.Remove(from.PlayerUID);
            if (req.key != p.Key || Environment.TickCount64 > req.until || !Valid(p.Data)) return;

            if (p.Key == SummonRitual.ShotKey)
                SummonRitual.SetShot(from.PlayerUID, p.Data!);
            else if (p.Key == PersonalKey)
                from.Entity.Attributes.SetBytes(PersonalShotAttr, p.Data!);
            else if (p.Key.StartsWith(AdminPrefix) && PortalAnchors.Find(p.Key.Substring(AdminPrefix.Length)) is PortalAnchor a)
                PortalAnchors.SetShot(a, p.Data!);
        }

        public static void Forget(string uid) => pending.Remove(uid);
    }
}
