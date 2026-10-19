using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using canrpgclasses.Core.Control;

namespace canrpgclasses.Core.EB
{
    /// <summary>CC enforcement the Harmony patches don't cover: zeroes motion while rooted and interrupts hand use.
    /// Fear doesn't root - its wander needs the motion.</summary>
    public class EBStun : EntityBehavior
    {
        public const string Name = "canrpgstun";

        public EBStun(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        public override void OnGameTick(float dt)
        {
            if (entity.Api.Side != EnumAppSide.Server) return;

            if (ControlState.RootsMotion(entity))
            {
                var pos = entity.Pos;
                if (pos != null) { pos.Motion.X = 0; pos.Motion.Z = 0; }
            }

            if (ControlState.InterruptsHandUse(entity)
                && entity is EntityAgent agent && agent.Controls.HandUse != EnumHandInteract.None)
            {
                agent.TryStopHandAction(true, EnumItemUseCancelReason.Death);
            }
        }
    }
}
