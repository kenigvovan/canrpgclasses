using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace canrpgclasses.Core.EB
{
    /// <summary>Expires timed combat-state flags. The first tick after load wipes them all, since deadlines are in
    /// ElapsedMilliseconds, which resets on restart.</summary>
    public class EBCombatState : EntityBehavior
    {
        public const string Name = "canrpgcombatstate";

        private static readonly (string flag, string until)[] Timed =
        {
            (CombatFlags.Evasion, CombatFlags.EvasionUntil),
            (CombatFlags.Stunned, CombatFlags.StunUntil),
            (CombatFlags.Absorb,  CombatFlags.AbsorbUntil),
            (CombatFlags.InCombat, CombatFlags.InCombatUntil),
            (CombatFlags.Silenced, CombatFlags.SilenceUntil),
            (CombatFlags.Feared, CombatFlags.FearUntil),
            (CombatFlags.Rooted, CombatFlags.RootUntil),
            (CombatFlags.Polymorphed, CombatFlags.PolymorphUntil),
        };

        private bool cleanedOnLoad;

        public EBCombatState(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        public override void OnGameTick(float dt)
        {
            if (entity.Api.Side != EnumAppSide.Server) return;
            var wa = entity.WatchedAttributes;

            if (!cleanedOnLoad)
            {
                cleanedOnLoad = true;
                // Restore the model only for transmute, not Spirit Wolf (same swap, re-applied by EBAuras).
                // Captured before the loop clears the flag.
                bool wasPolymorphedOnLoad = wa.HasAttribute(CombatFlags.Polymorphed);
                foreach (var (flag, until) in Timed) Remove(wa, flag, until);
                wa.RemoveAttribute(CombatFlags.BreakOnDamageMask);
                wa.RemoveAttribute(CombatFlags.CcMask); // aggregate CC gate: all flags were just dropped
                canrpgclasses.Core.Control.ControlState.RestoreBlindVision(entity); // a Blind may have been mid-flight
                canrpgclasses.Core.Control.ControlState.ClearFear(wa); // wander state isn't a (flag, until) pair
                if (wasPolymorphedOnLoad)
                    canrpgclasses.Core.Integration.PlayerModelSwap.Restore(entity as EntityPlayer);
                // With no active aura the action lock is an orphan from an abnormal session end; a live form re-asserts it.
                if (string.IsNullOrEmpty(wa.GetString(AttrKeys.ActiveAura, "")))
                    wa.RemoveAttribute(CombatFlags.FormActionLock);
                return;
            }

            long now = entity.World.ElapsedMilliseconds;
            bool wasStunned = wa.HasAttribute(CombatFlags.Stunned);
            bool wasFeared = wa.HasAttribute(CombatFlags.Feared);
            bool wasPolymorphed = wa.HasAttribute(CombatFlags.Polymorphed);
            bool removedAny = false;
            foreach (var (flag, until) in Timed)
                if (wa.HasAttribute(flag) && now >= wa.GetLong(until, 0))
                {
                    Remove(wa, flag, until);
                    removedAny = true;
                }

            if (removedAny) canrpgclasses.Core.Control.ControlState.RefreshCcGate(wa);

            // State that isn't a (flag, until) pair: blind vision, fear wander anchor, transmute model.
            if (wasStunned && !wa.HasAttribute(CombatFlags.Stunned))
                canrpgclasses.Core.Control.ControlState.RestoreBlindVision(entity);

            if (wasFeared && !wa.HasAttribute(CombatFlags.Feared))
                canrpgclasses.Core.Control.ControlState.ClearFear(wa);

            if (wasPolymorphed && !wa.HasAttribute(CombatFlags.Polymorphed))
                canrpgclasses.Core.Integration.PlayerModelSwap.Restore(entity as EntityPlayer);
        }

        private static void Remove(ITreeAttribute wa, string flag, string until)
        {
            if (wa.HasAttribute(flag)) wa.RemoveAttribute(flag);
            if (wa.HasAttribute(until)) wa.RemoveAttribute(until);
        }
    }
}
