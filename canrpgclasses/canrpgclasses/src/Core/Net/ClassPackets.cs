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

    /// <summary>server → client (on join): the vanilla-character-class restrictions as JSON. They live in the
    /// server's mod-config folder, so the client has no copy of its own; without this the class picker could not
    /// tell which classes the server would refuse. See
    /// <see cref="canrpgclasses.Core.Classes.ClassRestrictions"/>.</summary>
    [ProtoContract]
    public class ClassRestrictionsPacket
    {
        [ProtoMember(1)] public string Json = "";
    }
}
