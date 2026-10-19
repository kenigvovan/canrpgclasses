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

    /// <summary>Server-side class change: first pick free, later ones gated by <see cref="ReselectMode"/>. Respecs talents.</summary>
    public static class ClassChange
    {
        /// <summary>Set once a class was explicitly chosen; until then the pick is free and the picker auto-opens.</summary>
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

        /// <summary>Milliseconds left on the change cooldown (0 if none). Works on either side.</summary>
        public static long CooldownRemainingMs(Entity e, ICoreAPI api)
        {
            if (e == null) return 0;
            long last = e.WatchedAttributes.GetLong(LastChangeKey, 0);
            if (last <= 0) return 0;
            long window = (long)(CooldownDays(api) * 24 * 60 * 60 * 1000);
            long elapsed = e.World.ElapsedMilliseconds - last;
            return elapsed >= window ? 0 : window - elapsed;
        }

        /// <summary>Checks the policy (unless <paramref name="adminOverride"/>), takes any fee and commits.
        /// <paramref name="reason"/> is a lang key suffix on failure.</summary>
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

            // Checked before any fee is taken, and on the free first pick too: it limits which class a character
            // may ever be, not how often they switch.
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

        /// <summary>Switches class (with respec) and stamps the flags, bypassing the policy.</summary>
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
