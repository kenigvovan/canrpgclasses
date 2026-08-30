using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace canrpgclasses.Hunter
{
    /// <summary>The hunter wolf's seek task, gated by its command mode. Unlike vanilla seek (which re-scans and
    /// randomly bails every call) it holds its commanded target while it's alive and sensable.</summary>
    public class AiTaskPetSeek : AiTaskSeekEntity
    {
        public AiTaskPetSeek(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
            : base(entity, taskConfig, aiConfig)
        {
            // Never leap at the target: the leap branch in the base ContinueExecute dereferences
            // entity.AnimManager.Animator, which is null on the server for our pet, so a leap would NRE-crash the
            // entity tick. The wolf still walks/runs to its target normally.
            leapAtTarget = false;
        }

        public override bool ShouldExecute()
        {
            if (!HunterPetSystem.TaskModeAllows(entity)) return false;
            NowSeekRange = getSeekRange();

            // Drop the current target only if it actually became invalid - otherwise keep chasing it.
            if (targetEntity != null && (!targetEntity.Alive || !CanSense(targetEntity, NowSeekRange)))
                targetEntity = null;

            if (targetEntity == null)
            {
                // Prefer the explicitly ordered/assisted foe, then any revenge attacker.
                var forced = HunterPetSystem.ForcedTargetOf(entity);
                if (forced != null && CanSense(forced, NowSeekRange)) targetEntity = forced;
                else if (attackedByEntity != null && attackedByEntity.Alive && CanSense(attackedByEntity, NowSeekRange))
                    targetEntity = attackedByEntity;
            }

            if (targetEntity != null && CanSense(targetEntity, NowSeekRange))
            {
                targetPos = targetEntity.Pos.XYZ;
                return true;
            }

            // No commanded target: only an Aggressive pet wanders off to fight whatever it spots (vanilla scan).
            return HunterPetSystem.PetModeOf(entity) == PetMode.Aggressive && base.ShouldExecute();
        }
    }
}
