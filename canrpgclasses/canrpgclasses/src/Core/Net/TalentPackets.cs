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

    /// <summary>client → server: respec and re-spend a saved build. Each point is validated, so a build that no
    /// longer fits is applied as far as it can be.</summary>
    [ProtoContract]
    public class TalentLoadoutPacket
    {
        [ProtoMember(1)] public string[] Ids = System.Array.Empty<string>();
        [ProtoMember(2)] public int[] Ranks = System.Array.Empty<int>();
    }
}
