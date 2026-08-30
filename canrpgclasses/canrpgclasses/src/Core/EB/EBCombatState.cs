using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace canrpgclasses.Core.EB
{
    /// <summary>
    /// Owns expiry of the timed combat-state flags (evasion, stun, absorb, in-combat). The first tick after a
    /// load wipes them all: deadlines are in ElapsedMilliseconds, which resets on restart.
    /// </summary>
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
                // A transmute model must be un-stuck, but Spirit Wolf uses the same swap and is a legitimate
                // persistent form that EBAuras re-applies - restoring it would un-wolf a relogging shaman. Only
                // transmute sets the flag, so gate on it, captured before the loop below clears it.
                bool wasPolymorphedOnLoad = wa.HasAttribute(CombatFlags.Polymorphed);
                foreach (var (flag, until) in Timed) Remove(wa, flag, until);
                wa.RemoveAttribute(CombatFlags.BreakOnDamageMask);
                wa.RemoveAttribute(CombatFlags.CcMask); // aggregate CC gate: all flags were just dropped
                canrpgclasses.Core.Control.ControlState.RestoreBlindVision(entity); // a Blind may have been mid-flight
                canrpgclasses.Core.Control.ControlState.ClearFear(wa); // wander state isn't a (flag, until) pair
                if (wasPolymorphedOnLoad)
                    canrpgclasses.Core.Integration.PlayerModelSwap.Restore(entity as EntityPlayer);
                // Only an active toggle form holds the action lock, so with no aura on load it's an orphan from an
                // abnormal session end - drop it, or the player can't attack or cast. A live form re-asserts it.
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

            // Several Timed entries are CC flags, so keep the aggregate gate in sync when any expired.
            if (removedAny) canrpgclasses.Core.Control.ControlState.RefreshCcGate(wa);

            // The three below own state that isn't a (flag, until) pair, so the generic loop can't undo it: the
            // blind's screen effect, the fear's wander anchor, and the transmute model.
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
