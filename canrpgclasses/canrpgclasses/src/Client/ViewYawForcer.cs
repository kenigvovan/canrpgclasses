using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Snaps the local camera yaw toward a server-requested angle for a short window (shadow_step lands facing
    /// its target). Writes <c>mouseYaw</c> per frame instead of trusting <c>TeleportTo</c>'s yaw, which races the
    /// async position sync.
    /// </summary>
    public class ViewYawForcer : IRenderer
    {
        private readonly ICoreClientAPI capi;
        private float targetYaw;
        private long untilMs;

        public ViewYawForcer(ICoreClientAPI capi)
        {
            this.capi = capi;
            capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "canrpgviewyaw");
        }

        // After the camera (RenderOrder 0.0), like the quern's physics renderer (1.0).
        public double RenderOrder => 0.5;
        public int RenderRange => 9999;

        public void Trigger(float yaw, int durationMs)
        {
            targetYaw = yaw;
            untilMs = capi.World.ElapsedMilliseconds + durationMs;
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (capi.World.ElapsedMilliseconds >= untilMs) return;
            if (capi.World.Player?.Entity is not EntityPlayer ep) return;

            // Overhead camera owns its own yaw; only snap first/third person.
            if (capi.World.Player.CameraMode != EnumCameraMode.Overhead)
                capi.Input.MouseYaw = targetYaw;
            ep.BodyYaw = targetYaw;
            ep.WalkYaw = targetYaw;
            ep.Pos.Yaw = targetYaw;
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Before);
        }
    }
}
