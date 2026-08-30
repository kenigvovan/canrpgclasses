using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Economy;
using canrpgclasses.Core.Talents;

namespace canrpgclasses.Core.Classes
{
    /// <summary>Re-pick policy for changing class (config <c>classChangeReselectMode</c> in core.json).</summary>
    public enum ReselectMode { Unrestricted = 0, Cooldown = 1, AdminOnly = 2, Fee = 3 }

    /// <summary>
    /// Server-authoritative class selection / change: first pick free, later changes gated by
    /// <see cref="ReselectMode"/>. Switching class respecs talents.
    /// </summary>
    public static class ClassChange
    {
        /// <summary>True once the player has explicitly chosen a class (so the first pick is free and the
        /// selection screen only auto-opens for the undecided). Synced for the client.</summary>
        public const string ChosenKey = Core.AttrKeys.ClassChosen;
        /// <summary>Server time (ms) of the last paid/cooldown-gated change, for the cooldown mode.</summary>
        public const string LastChangeKey = Core.AttrKeys.ClassChangedMs;
        /// <summary>Admin-granted free re-pick: the next change skips cooldown/fee/admin-only, then clears.</summary>
        public const string FreeKey = Core.AttrKeys.ClassFreeRepick;

        public static bool HasChosen(Entity e) => e?.WatchedAttributes?.GetBool(ChosenKey, false) ?? false;

        public static void GrantFreeRepick(Entity e)
        {
            if (e == null) return;
            e.WatchedAttributes.SetBool(FreeKey, true);
            e.WatchedAttributes.MarkPathDirty(FreeKey);
        }

        public static ReselectMode Mode(ICoreAPI api)
        {
            var m = (ReselectMode)(int)BalanceConfig.Global("classChangeReselectMode", 0f);
            // Fee needs caneconomy; without it, don't silently let anyone re-pick for free - fall back to admin-only.
            if (m == ReselectMode.Fee && !EconomyBridge.Available(api)) return ReselectMode.AdminOnly;
            return m;
        }

        public static double CooldownDays(ICoreAPI api) => BalanceConfig.Global("classChangeCooldownDays", 7f);
        public static decimal Fee(ICoreAPI api) => (decimal)BalanceConfig.Global("classChangeFee", 1000f);

        /// <summary>Milliseconds remaining on the change cooldown (0 if none / not in cooldown mode). Works on either
        /// side - reads the synced last-change stamp from the entity's WatchedAttributes.</summary>
        public static long CooldownRemainingMs(Entity e, ICoreAPI api)
        {
            if (e == null) return 0;
            long last = e.WatchedAttributes.GetLong(LastChangeKey, 0);
            if (last <= 0) return 0;
            long window = (long)(CooldownDays(api) * 24 * 60 * 60 * 1000);
            long elapsed = e.World.ElapsedMilliseconds - last;
            return elapsed >= window ? 0 : window - elapsed;
        }

        /// <summary>Authoritative entry point. Validates the policy (unless <paramref name="adminOverride"/>), takes
        /// any fee, switches class (respeccing talents) and records the change. <paramref name="reason"/> is a lang
        /// key suffix on failure. The first pick is always free and ignores cooldown/fee.</summary>
        public static bool TryChange(IServerPlayer player, string classId, bool adminOverride, out string reason)
        {
            reason = "";
            var entity = player?.Entity;
            var api = entity?.Api;
            if (entity == null || api == null) { reason = "no-entity"; return false; }

            var mod = canrpgclassesModSystem.For(api);
            if (mod == null || mod.Classes.Get(classId) == null) { reason = "unknown-class"; return false; }

            bool firstPick = !HasChosen(entity);
            bool sameClass = TalentState.CurrentClass(entity) == classId && HasChosen(entity);
            if (sameClass) { reason = "already-this-class"; return false; }

            // Vanilla-character-class gate (config). Checked before the re-pick policy so a forbidden pick is
            // refused before any fee is taken, and applied to the free first pick too - the point of the rule is
            // which class a given character may ever be, not how often they may switch. An admin override skips
            // it, like every other gate here.
            if (!adminOverride && !ClassRestrictions.Allowed(entity, classId, out string restricted))
            { reason = restricted; return false; }

            bool free = firstPick || entity.WatchedAttributes.GetBool(FreeKey, false);
            if (!adminOverride && !free)
            {
                switch (Mode(api))
                {
                    case ReselectMode.Unrestricted:
                        break;

                    case ReselectMode.Cooldown:
                        if (CooldownRemainingMs(entity, api) > 0) { reason = "on-cooldown"; return false; }
                        break;

                    case ReselectMode.AdminOnly:
                        reason = "admin-only";
                        return false;

                    case ReselectMode.Fee:
                        decimal fee = Fee(api);
                        if (!EconomyBridge.TryCharge(player, fee, out string err))
                        {
                            reason = err.Length > 0 ? err : "payment-failed";
                            return false;
                        }
                        break;
                }
            }

            Commit(entity, classId);
            return true;
        }

        /// <summary>Applies the class switch + respec and stamps the change/chosen flags. Used by the policy path
        /// above and by the admin command (which bypasses the policy).</summary>
        public static void Commit(Entity entity, string classId)
        {
            entity.GetBehavior<EBTalents>()?.SetClass(classId);
            entity.WatchedAttributes.SetBool(ChosenKey, true);
            entity.WatchedAttributes.SetLong(LastChangeKey, entity.World.ElapsedMilliseconds);
            entity.WatchedAttributes.SetBool(FreeKey, false);
            entity.WatchedAttributes.MarkPathDirty(ChosenKey);
        }
    }
}
