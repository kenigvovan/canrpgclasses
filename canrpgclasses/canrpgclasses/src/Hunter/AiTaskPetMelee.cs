using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace canrpgclasses.Hunter
{
    /// <summary>Mode-gated melee, like <see cref="AiTaskPetSeek"/>.</summary>
    public class AiTaskPetMelee : AiTaskMeleeAttack
    {
        public AiTaskPetMelee(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
            : base(entity, taskConfig, aiConfig) { }

        public override bool ShouldExecute() => HunterPetSystem.TaskModeAllows(entity) && base.ShouldExecute();

        public override bool IsTargetableEntity(Entity e, float range)
        {
            // Always allow the commanded foe (even a player, which the vanilla filter rejects), or the wolf
            // drops it after the first bite.
            if (e != null && e == HunterPetSystem.ForcedTargetOf(entity)) return CanSense(e, range);
            return base.IsTargetableEntity(e, range);
        }
    }
}
