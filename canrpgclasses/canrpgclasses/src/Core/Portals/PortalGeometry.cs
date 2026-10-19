using System;
using Vintagestory.API.MathTools;

namespace canrpgclasses.Core.Portals
{
    /// <summary>Shape of a portal opening, shared by server (who is inside) and client (where to draw).
    /// An end is given by its foot point (bottom middle) and the yaw you walk out of it.</summary>
    public static class PortalGeometry
    {
        public const float Width = 2f;
        public const float Height = 3f;
        /// <summary>How far from the plane, across it, an entity still counts as inside.</summary>
        public const double PlaneDepth = 0.3;

        /// <summary>Yaw snapped to the nearest axis: the plane is always X- or Z-aligned.</summary>
        public static float SnapYaw(float yaw)
        {
            const float quarter = GameMath.PIHALF;
            return (float)Math.Round(GameMath.Mod(yaw, GameMath.TWOPI) / quarter) * quarter % GameMath.TWOPI;
        }

        /// <summary>Facing ±Z means the plane spans X.</summary>
        public static bool AlongX(float yaw) => Math.Abs(Math.Cos(yaw)) >= Math.Abs(Math.Sin(yaw));

        public static Vec3d Forward(float yaw) => new Vec3d(Math.Sin(yaw), 0, Math.Cos(yaw));

        public static bool Contains(Vec3d foot, float yaw, double x, double y, double z)
        {
            double dy = y - foot.Y;
            if (dy < -0.5 || dy > Height - 1) return false;
            double dx = x - foot.X, dz = z - foot.Z;
            bool alongX = AlongX(yaw);
            double along = alongX ? dx : dz, across = alongX ? dz : dx;
            return Math.Abs(along) <= Width / 2 && Math.Abs(across) <= PlaneDepth;
        }
    }
}
