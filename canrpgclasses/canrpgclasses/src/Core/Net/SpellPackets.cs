using System.Collections.Generic;
using ProtoBuf;

namespace canrpgclasses.Core.Net
{
    public enum SpellRequestAction
    {
        Cast = 0,
        Release = 1,
        Cancel = 2
    }

    /// <summary>client → server: the player wants to cast / release / cancel a spell.</summary>
    [ProtoContract]
    public class SpellRequestPacket
    {
        [ProtoMember(1)] public int Action;
        [ProtoMember(2)] public string SpellId = "";
        [ProtoMember(3)] public float Progress;
        [ProtoMember(4)] public long TargetEntityId;
        [ProtoMember(5)] public bool HasAimPos;
        [ProtoMember(6)] public double AimX;
        [ProtoMember(7)] public double AimY;
        [ProtoMember(8)] public double AimZ;
        /// <summary>A pick made before casting, e.g. the Waypoint destination's name.</summary>
        [ProtoMember(9)] public string? Choice;
    }

    /// <summary>server → nearby clients: someone started casting (for animation/fx).</summary>
    [ProtoContract]
    public class SpellCastSyncPacket
    {
        [ProtoMember(1)] public long CasterEntityId;
        [ProtoMember(2)] public string SpellId = "";
        [ProtoMember(3)] public int CastMode;
        [ProtoMember(4)] public float Duration;
    }

    /// <summary>server → owner: a single cooldown was set/updated.</summary>
    [ProtoContract]
    public class SpellCooldownPacket
    {
        [ProtoMember(1)] public string Key = "";
        [ProtoMember(2)] public float Duration;
        [ProtoMember(3)] public float Remaining;
    }

    /// <summary>server → owner: the full cooldown map (reconnect / balance).</summary>
    [ProtoContract]
    public class SpellCooldownSyncPacket
    {
        [ProtoMember(1)] public List<SpellCooldownPacket> Cooldowns = new List<SpellCooldownPacket>();
    }

    /// <summary>server → owner: a short status message (e.g. "not enough stamina").</summary>
    [ProtoContract]
    public class SpellMessagePacket
    {
        [ProtoMember(1)] public string Message = "";
    }

    /// <summary>server → nearby clients: a one-shot spell sound.</summary>
    [ProtoContract]
    public class SpellSoundPacket
    {
        [ProtoMember(1)] public string Location = "";
        [ProtoMember(2)] public float Volume;
        [ProtoMember(3)] public double X;
        [ProtoMember(4)] public double Y;
        [ProtoMember(5)] public double Z;
    }

    /// <summary>server → owner: a channeled (CastMode.Charge) cast was interrupted/cancelled - hide the cast bar.</summary>
    [ProtoContract]
    public class SpellCastCancelPacket
    {
        [ProtoMember(1)] public long CasterEntityId;
    }

    /// <summary>server → owner: snap view yaw. Applied to MouseYaw client-side because TeleportTo's yaw is
    /// unreliable for the owner camera (async chunk load).</summary>
    [ProtoContract]
    public class SetViewYawPacket
    {
        [ProtoMember(1)] public float Yaw;
    }

    /// <summary>server → owner: floating combat text - damage the player dealt, shown over the victim's
    /// world position by the client overlay.</summary>
    [ProtoContract]
    public class DamageNumberPacket
    {
        [ProtoMember(1)] public double X;
        [ProtoMember(2)] public double Y;
        [ProtoMember(3)] public double Z;
        [ProtoMember(4)] public float Amount;
        /// <summary>True = healing the player did (shown green with a "+"), false = damage (gold).</summary>
        [ProtoMember(5)] public bool IsHeal;
    }

    /// <summary>server → client (on join): authoritative <see cref="canrpgclasses.Core.Config.BalanceConfig"/> JSON;
    /// the client overwrites its own and rebuilds its registries.</summary>
    [ProtoContract]
    public class BalanceConfigPacket
    {
        [ProtoMember(1)] public string Json = "";
        /// <summary>Attribute definitions, also server-owned config.</summary>
        [ProtoMember(2)] public string Attributes = "";
    }

    /// <summary>client → server: admin edit of one balance number. Server re-checks controlserver, applies, persists
    /// and rebroadcasts <see cref="BalanceConfigPacket"/>.</summary>
    [ProtoContract]
    public class BalanceEditPacket
    {
        [ProtoMember(1)] public int Category; // canrpgclasses.Core.Config.BalanceConfig.BalanceCategory
        [ProtoMember(2)] public string Id = ""; // spell localId / talent "classId:localId" / affinity "classId:key" (ignored for Global)
        [ProtoMember(3)] public string Key = "";
        [ProtoMember(4)] public float Value;
    }

    /// <summary>server → party members: each member's resource fraction for the canparty frames (canparty only syncs HP).
    /// Parallel arrays; <see cref="Colors"/> is packed RGBA.</summary>
    [ProtoContract]
    public class PartyResourceMsg
    {
        [ProtoMember(1)] public string[] Uids = System.Array.Empty<string>();
        [ProtoMember(2)] public float[] Fractions = System.Array.Empty<float>();
        [ProtoMember(3)] public int[] Colors = System.Array.Empty<int>();
    }

    public enum AdminAction { AddXp = 0, SetLevel = 1, AddLevel = 2 }

    /// <summary>client(admin) → server: change a target player's progression. Server requires the
    /// controlserver privilege and resolves the target by UID.</summary>
    [ProtoContract]
    public class AdminProgressionPacket
    {
        [ProtoMember(1)] public string TargetUid = "";
        [ProtoMember(2)] public int Action;   // AdminAction
        [ProtoMember(3)] public long Value;
    }
}
