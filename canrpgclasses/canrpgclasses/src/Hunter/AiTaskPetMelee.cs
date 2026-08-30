using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace canrpgclasses.Hunter
{
    /// <summary>The hunter wolf's melee task, gated by its command mode the same way as <see cref="AiTaskPetSeek"/>:
    /// full auto in Aggressive, order/assist-window-only in Defensive, never in Passive. It also treats the commanded
    /// foe as always targetable (mirrors PetAI), so the vanilla melee re-scan keeps biting a player or ordered target
    /// the pet was sent at, instead of only landing the single revenge-ping bite and then disengaging.</summary>
    public class AiTaskPetMelee : AiTaskMeleeAttack
    {
        public AiTaskPetMelee(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
            : base(entity, taskConfig, aiConfig) { }

        public override bool ShouldExecute() => HunterPetSystem.TaskModeAllows(entity) && base.ShouldExecute();

        public override bool IsTargetableEntity(Entity e, float range)
        {
            // Always allow the commanded foe (even a player, which the vanilla code filter rejects) so the wolf keeps
            // striking the target it was ordered onto rather than dropping it after the first hit.
            if (e != null && e == HunterPetSystem.ForcedTargetOf(entity)) return CanSense(e, range);
            return base.IsTargetableEntity(e, range);
        }
    }
}
