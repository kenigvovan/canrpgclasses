using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using canrpgclasses.Core.Execution;

namespace canrpgclasses.Hunter
{
    /// <summary>Mode-gated seek. Unlike vanilla (re-scans and randomly bails) it holds the commanded target
    /// while it's alive and sensable.</summary>
    public class AiTaskPetSeek : AiTaskSeekEntity
    {
        public AiTaskPetSeek(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
            : base(entity, taskConfig, aiConfig)
        {
            // The base leap branch dereferences AnimManager.Animator, which is null server-side for our pet (NRE).
            leapAtTarget = false;
        }

        public override bool ShouldExecute()
        {
            if (!HunterPetSystem.TaskModeAllows(entity)) return false;
            NowSeekRange = getSeekRange();

            if (targetEntity != null && (!targetEntity.Alive || !CanSense(targetEntity, NowSeekRange)))
                targetEntity = null;

            if (targetEntity == null)
            {
                // Prefer the explicitly ordered/assisted foe, then any revenge attacker.
                var forced = HunterPetSystem.ForcedTargetOf(entity);
                if (forced != null && CanSense(forced, NowSeekRange)) targetEntity = forced;
                else if (attackedByEntity != null && attackedByEntity.Alive && !SpellExecutor.IsAlly(entity, attackedByEntity)
                         && CanSense(attackedByEntity, NowSeekRange))
                    targetEntity = attackedByEntity;
            }

            if (targetEntity != null && CanSense(targetEntity, NowSeekRange))
            {
                targetPos = targetEntity.Pos.XYZ;
                return true;
            }

            // No free hunting in any mode: the wolf only goes after what it was sent at or what attacked it.
            return false;
        }
    }
}
