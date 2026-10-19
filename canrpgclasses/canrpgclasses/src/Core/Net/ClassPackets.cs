using ProtoBuf;

namespace canrpgclasses.Core.Net
{
    /// <summary>client → server: the player requests to select / change to a class (subject to the re-pick policy).</summary>
    [ProtoContract]
    public class SelectClassPacket
    {
        [ProtoMember(1)] public string ClassId = "";
    }

    /// <summary>server → owner: result of a class-select attempt (lang key + the class that's now active).</summary>
    [ProtoContract]
    public class SelectClassResultPacket
    {
        [ProtoMember(1)] public bool Success;
        [ProtoMember(2)] public string MessageKey = "";
        [ProtoMember(3)] public string ClassId = "";
    }

    /// <summary>server → client (on join): <see cref="canrpgclasses.Core.Classes.ClassRestrictions"/> as JSON; they live
    /// in the server's mod-config, so the client picker has no other copy.</summary>
    [ProtoContract]
    public class ClassRestrictionsPacket
    {
        [ProtoMember(1)] public string Json = "";
    }
}
