using ProtoBuf;

namespace canrpgclasses.Core.Net
{
    /// <summary>client → server: bind (or, with an empty SpellId, clear) a spell on the player's active held item.
    /// The server writes it into the stack's attributes; one spell per item.</summary>
    [ProtoContract]
    public class BindSpellPacket
    {
        [ProtoMember(1)] public string SpellId = "";
    }
}
