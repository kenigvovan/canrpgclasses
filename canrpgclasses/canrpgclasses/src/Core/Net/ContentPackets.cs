using ProtoBuf;

namespace canrpgclasses.Core.Net
{
    /// <summary>server → client: the content authored in-game, as JSON. The client adopts it into
    /// <see cref="canrpgclasses.Core.Content.ContentStore"/> and rebuilds its registries, so its talent tree and
    /// spellbook show exactly what the server enforces. Sent on join and after every edit.</summary>
    [ProtoContract]
    public class ContentSyncPacket
    {
        [ProtoMember(1)] public string Json = "";
    }

    /// <summary>client → server: one talent tree as edited in the tree editor, replacing whatever that tree held
    /// before. A whole tree rather than a single talent, because that is also how a talent gets deleted - it
    /// simply isn't in the list any more. The server re-checks the controlserver privilege before applying.</summary>
    [ProtoContract]
    public class TalentTreeEditPacket
    {
        [ProtoMember(1)] public string ClassId = "";
        [ProtoMember(2)] public int TreeIndex;
        /// <summary>The tree's talents, serialized as a JSON array of the same shape content files use.</summary>
        [ProtoMember(3)] public string TalentsJson = "";
    }

    /// <summary>client → server: one class definition from the class editor, added or replaced whole. With
    /// <see cref="Delete"/> set it removes the authored class named by <see cref="ClassId"/> instead. Only classes
    /// that came from data can be touched - a compiled class carries behaviour a record can't replace.</summary>
    [ProtoContract]
    public class ClassEditPacket
    {
        [ProtoMember(1)] public string ClassJson = "";
        [ProtoMember(2)] public bool Delete;
        [ProtoMember(3)] public string ClassId = "";
    }

    /// <summary>client → server: one spell definition from the spell editor, added or replaced whole. With
    /// <see cref="Delete"/> set it removes the authored spell named by <see cref="SpellId"/> instead.</summary>
    [ProtoContract]
    public class SpellEditPacket
    {
        [ProtoMember(1)] public string SpellJson = "";
        [ProtoMember(2)] public bool Delete;
        [ProtoMember(3)] public string SpellId = "";
    }

    /// <summary>server → the editing admin: how the edit went, so the editor can show problems instead of
    /// silently dropping a talent.</summary>
    [ProtoContract]
    public class ContentEditResultPacket
    {
        [ProtoMember(1)] public bool Ok;
        [ProtoMember(2)] public string Message = "";
    }
}
