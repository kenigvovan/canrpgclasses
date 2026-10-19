using ProtoBuf;

namespace canrpgclasses.Core.Net
{
    /// <summary>server → client: in-game authored content as JSON, adopted into <see cref="canrpgclasses.Core.Content.ContentStore"/>.
    /// Sent on join and after every edit.</summary>
    [ProtoContract]
    public class ContentSyncPacket
    {
        [ProtoMember(1)] public string Json = "";
    }

    /// <summary>client → server: a whole edited talent tree, replacing the old one (a missing talent = deleted).
    /// Server re-checks controlserver.</summary>
    [ProtoContract]
    public class TalentTreeEditPacket
    {
        [ProtoMember(1)] public string ClassId = "";
        [ProtoMember(2)] public int TreeIndex;
        /// <summary>The tree's talents, serialized as a JSON array of the same shape content files use.</summary>
        [ProtoMember(3)] public string TalentsJson = "";
    }

    /// <summary>client → server: add/replace one class, or remove <see cref="ClassId"/> with <see cref="Delete"/>.
    /// Only data-authored classes; compiled ones carry behaviour a record can't replace.</summary>
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
