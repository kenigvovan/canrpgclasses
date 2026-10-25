using ProtoBuf;

namespace canrpgclasses.Core.Net
{
    /// <summary>client → server: rename the pet. Stored on the owner and re-applied to every summon; empty clears it.</summary>
    [ProtoContract]
    public class PetNamePacket
    {
        [ProtoMember(1)] public string Name = "";
    }

    /// <summary>client → server: stable action on a slot - "select" makes it the active pet, "release" frees it.</summary>
    [ProtoContract]
    public class PetStablePacket
    {
        [ProtoMember(1)] public string Action = "";
        [ProtoMember(2)] public int Slot;
    }
}
