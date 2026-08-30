using ProtoBuf;

namespace canrpgclasses.Core.Net
{
    /// <summary>client → server: spend one talent point on the given talent.</summary>
    [ProtoContract]
    public class TalentSpendPacket
    {
        [ProtoMember(1)] public string TalentId = "";
    }

    /// <summary>client → server: refund all talent points.</summary>
    [ProtoContract]
    public class TalentRespecPacket
    {
    }

    /// <summary>client → server: respec, then re-spend to match a saved build (parallel id/rank arrays). The
    /// server validates every point (tier gates, prerequisites, available points), so a build that no longer
    /// fits - e.g. after a level loss - is applied as far as the points allow.</summary>
    [ProtoContract]
    public class TalentLoadoutPacket
    {
        [ProtoMember(1)] public string[] Ids = System.Array.Empty<string>();
        [ProtoMember(2)] public int[] Ranks = System.Array.Empty<int>();
    }
}
