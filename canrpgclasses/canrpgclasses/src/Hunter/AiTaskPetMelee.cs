using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using canrpgclasses.Core.Execution;

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
            // Only the commanded foe (even a player, which the vanilla filter rejects) or whatever attacked the wolf.
            // No entityCodes scan, so creatures of other mods are fought too, and nothing is picked on its own.
            // The vanilla filter we skip is what rejects corpses, so check it here.
            if (e == null || !e.Alive) return false;
            if (e == HunterPetSystem.ForcedTargetOf(entity)) return CanSense(e, range);
            // A stray hit from the owner or a party member is not a reason to bite back.
            if (e == attackedByEntity && !SpellExecutor.IsAlly(entity, e)) return CanSense(e, range);
            return false;
        }
    }
}
