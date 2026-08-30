using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace canrpgclasses.Core.Economy
{
    /// <summary>
    /// Soft dependency on the <c>caneconomy</c> mod, reached entirely by reflection so this mod compiles and runs
    /// whether or not caneconomy is installed. Accounts are keyed by <see cref="IPlayer.PlayerUID"/>.
    /// </summary>
    public static class EconomyBridge
    {
        private static bool resolved;
        private static MethodInfo? getHandler;     // static caneconomy.caneconomy.getHandler() -> EconomyHandler
        private static MethodInfo? getBalance;     // EconomyHandler.getBalance(string) -> decimal
        private static MethodInfo? withdraw;       // EconomyHandler.withdraw(string, decimal) -> OperationResult

        /// <summary>Whether caneconomy is loaded and its API resolved - i.e. fee payments can actually be taken.</summary>
        public static bool Available(ICoreAPI api)
        {
            if (api?.ModLoader?.IsModEnabled("caneconomy") != true) return false;
            Resolve();
            return getHandler != null && getBalance != null && withdraw != null;
        }

        /// <summary>Reads the player's balance, or null if caneconomy is unavailable / errored.</summary>
        public static decimal? Balance(IServerPlayer player)
        {
            if (player == null) return null;
            Resolve();
            object? handler = getHandler?.Invoke(null, null);
            if (handler == null || getBalance == null) return null;
            try { return (decimal)getBalance.Invoke(handler, new object[] { player.PlayerUID })!; }
            catch { return null; }
        }

        /// <summary>Charges <paramref name="amount"/> from the player's account. Returns false (with a reason) if
        /// caneconomy is unavailable or the balance is insufficient. Checks the balance before withdrawing.</summary>
        public static bool TryCharge(IServerPlayer player, decimal amount, out string error)
        {
            error = "";
            if (amount <= 0m) return true; // nothing to charge

            decimal? bal = Balance(player);
            if (bal == null) { error = "economy-unavailable"; return false; }
            if (bal.Value < amount) { error = "insufficient-funds"; return false; }

            object? handler = getHandler?.Invoke(null, null);
            if (handler == null || withdraw == null) { error = "economy-unavailable"; return false; }
            try
            {
                withdraw.Invoke(handler, new object[] { player.PlayerUID, amount });
                return true;
            }
            catch { error = "economy-unavailable"; return false; }
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            try
            {
                Type? modType = FindType("caneconomy.caneconomy");
                Type? handlerType = FindType("caneconomy.src.interfaces.EconomyHandler");
                if (modType == null || handlerType == null) return;

                getHandler = modType.GetMethod("getHandler", BindingFlags.Public | BindingFlags.Static);
                getBalance = handlerType.GetMethod("getBalance", new[] { typeof(string) });
                withdraw = handlerType.GetMethod("withdraw", new[] { typeof(string), typeof(decimal) });
            }
            catch { /* leave members null → Available() returns false */ }
        }

        private static Type? FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, throwOnError: false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
