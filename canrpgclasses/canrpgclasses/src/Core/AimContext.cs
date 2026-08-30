using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace canrpgclasses.Core
{
    /// <summary>
    /// What the caster is aiming at when a spell is released: an entity under the crosshair
    /// and/or a world position. Built on the server from the client's <c>SpellRequestPacket</c>.
    /// </summary>
    public class AimContext
    {
        public Entity? TargetEntity;
        public Vec3d? AimPosition;

        public static readonly AimContext None = new AimContext();
    }
}
