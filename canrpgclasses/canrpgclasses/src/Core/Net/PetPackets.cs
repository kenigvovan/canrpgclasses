using ProtoBuf;

namespace canrpgclasses.Core.Net
{
    /// <summary>client → server: the hunter renames their pet. The name is kept on the owner and re-applied to
    /// every summoned wolf. An empty name clears it. (Just a string - carries no hunter-specific dependency.)</summary>
    [ProtoContract]
    public class PetNamePacket
    {
        [ProtoMember(1)] public string Name = "";
    }
}
