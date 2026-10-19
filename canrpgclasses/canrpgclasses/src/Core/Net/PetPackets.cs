using ProtoBuf;

namespace canrpgclasses.Core.Net
{
    /// <summary>client → server: rename the pet. Stored on the owner and re-applied to every summon; empty clears it.</summary>
    [ProtoContract]
    public class PetNamePacket
    {
        [ProtoMember(1)] public string Name = "";
    }
}
