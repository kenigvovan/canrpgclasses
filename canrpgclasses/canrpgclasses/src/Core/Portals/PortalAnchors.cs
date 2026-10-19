using System;
using System.Collections.Generic;
using canrpgclasses.Core.Config;
using ProtoBuf;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace canrpgclasses.Core.Portals
{
    /// <summary>Where Waypoint portals may lead (balance <c>waypoint.anchorMode</c>).</summary>
    public enum AnchorMode { Personal = 0, Admin = 1, Both = 2 }

    [ProtoContract]
    public class PortalAnchor
    {
        [ProtoMember(1)] public string Name = "";
        [ProtoMember(2)] public int Dim;
        [ProtoMember(3)] public double X;
        [ProtoMember(4)] public double Y;
        [ProtoMember(5)] public double Z;
        [ProtoMember(6)] public float Yaw;
        /// <summary>Destination snapshot (<see cref="PortalShots"/>); stays on the server, not in the list sync.</summary>
        [ProtoMember(7)] public byte[]? Shot;
        /// <summary>The caster's own anchor, not an admin point. Never stored in the list.</summary>
        public bool Personal;
    }

    /// <summary>server → client: the full list of admin portal points.</summary>
    [ProtoContract]
    public class PortalAnchorsPacket
    {
        [ProtoMember(1)] public List<PortalAnchor> Anchors = new();
    }

    /// <summary>Admin-placed Waypoint destinations: stored in the save game, synced to every client for the picker.</summary>
    public static class PortalAnchors
    {
        private const string SaveKey = "canrpgclasses-portalanchors";

        private static List<PortalAnchor> serverList = new();
        private static ICoreServerAPI? sapi;

        /// <summary>Client copy, for the destination picker.</summary>
        public static List<PortalAnchor> ClientList { get; private set; } = new();

        public static AnchorMode Mode => (AnchorMode)GameMath.Clamp(BalanceConfig.Spell("waypoint").I("anchorMode", 0), 0, 2);

        public static void Start(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.SaveGameLoaded += OnSaveGameLoaded;
        }

        public static void Stop()
        {
            if (sapi != null) sapi.Event.SaveGameLoaded -= OnSaveGameLoaded;
            sapi = null;
            serverList = new();
        }

        private static void OnSaveGameLoaded()
        {
            try { serverList = sapi?.WorldManager.SaveGame.GetData<List<PortalAnchor>>(SaveKey) ?? new(); }
            catch (Exception e)
            {
                sapi?.Logger.Warning("[canrpgclasses] bad portal anchors in save: {0}", e.Message);
                serverList = new();
            }
        }

        public static IReadOnlyList<PortalAnchor> All => serverList;

        public static PortalAnchor? Find(string name)
            => serverList.Find(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Adds or moves the named point.</summary>
        public static void Set(PortalAnchor anchor)
        {
            serverList.RemoveAll(a => string.Equals(a.Name, anchor.Name, StringComparison.OrdinalIgnoreCase));
            serverList.Add(anchor);
            serverList.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase));
            Changed();
        }

        public static void SetShot(PortalAnchor anchor, byte[] shot)
        {
            anchor.Shot = shot;
            sapi?.WorldManager.SaveGame.StoreData(SaveKey, serverList);
        }

        public static bool Remove(string name)
        {
            if (serverList.RemoveAll(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)) == 0) return false;
            Changed();
            return true;
        }

        private static void Changed()
        {
            if (sapi == null) return;
            sapi.WorldManager.SaveGame.StoreData(SaveKey, serverList);
            foreach (var p in sapi.World.AllOnlinePlayers)
                if (p is IServerPlayer sp) SendTo(sp);
        }

        public static void SendTo(IServerPlayer player)
        {
            if (sapi == null) return;
            // Without the snapshots: they only travel with an open portal.
            var list = serverList.ConvertAll(a => new PortalAnchor { Name = a.Name, Dim = a.Dim, X = a.X, Y = a.Y, Z = a.Z, Yaw = a.Yaw });
            canrpgclassesModSystem.For(sapi)?.ServerChannel?.SendPacket(new PortalAnchorsPacket { Anchors = list }, player);
        }

        public static void OnPacket(PortalAnchorsPacket p) => ClientList = p.Anchors ?? new();
    }
}
