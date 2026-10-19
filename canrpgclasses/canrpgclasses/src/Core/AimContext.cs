using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace canrpgclasses.Core
{
    /// <summary>What the caster aims at on release: an entity and/or a world position. Built server-side from
    /// <c>SpellRequestPacket</c>.</summary>
    public class AimContext
    {
        public Entity? TargetEntity;
        public Vec3d? AimPosition;
        /// <summary>Aim straight at TargetEntity (else AimPosition) instead of along the look direction - AI casters have no crosshair.</summary>
        public bool AimAtTarget;
        /// <summary>A pick made before casting (Waypoint destination name). Null = none.</summary>
        public string? Choice;

        public static readonly AimContext None = new AimContext();
    }
}
