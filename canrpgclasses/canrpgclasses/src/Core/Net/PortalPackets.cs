using ProtoBuf;

namespace canrpgclasses.Core.Net
{
    /// <summary>server → nearby clients: a portal pair opened (both ends) or closed (Open = false, only Id).
    /// Positions are the foot of each opening; Yaw is the direction you walk out of it.</summary>
    [ProtoContract]
    public class PortalStatePacket
    {
        [ProtoMember(1)] public long Id;
        [ProtoMember(2)] public bool Open;
        [ProtoMember(3)] public int Dim;
        [ProtoMember(4)] public double AX;
        [ProtoMember(5)] public double AY;
        [ProtoMember(6)] public double AZ;
        [ProtoMember(7)] public float AYaw;
        [ProtoMember(8)] public double BX;
        [ProtoMember(9)] public double BY;
        [ProtoMember(10)] public double BZ;
        [ProtoMember(11)] public float BYaw;
        /// <summary>Two RGB colours (0xRRGGBB) the swirl blends between.</summary>
        [ProtoMember(12)] public int TintA;
        [ProtoMember(13)] public int TintB;
        /// <summary>Destination snapshot drawn in end A instead of the bent view (PortalShots format), or null.</summary>
        [ProtoMember(14)] public byte[]? ImageA;
    }
}
