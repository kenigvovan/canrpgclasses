using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Hunter
{
    /// <summary>Gives tameable wild creatures their coat once, rare ones at <c>wildRareCoatChance</c>. The coat is the
    /// vanilla <c>textureIndex</c>, which the tamed pet keeps, so a rare beast is spotted in the wild.</summary>
    public static class WildCoats
    {
        private const string RolledKey = "canrpgCoat";

        public static void Init(ICoreServerAPI sapi)
        {
            sapi.Event.OnEntitySpawn += Roll;
            sapi.Event.OnEntityLoaded += Roll;
        }

        private static void Roll(Entity e)
        {
            var wa = e.WatchedAttributes;
            if (wa.HasAttribute(RolledKey) || PetSpecies.ForWild(e.World, e) is not { } look) return;
            wa.SetBool(RolledKey, true);
            // Vanilla picked uniformly, rare coats included; this keeps them rare.
            if (PetSpecies.ColorCount(e.World, look) > 1)
                wa.SetInt("textureIndex", PetSpecies.RollColor(e.World, look, BalanceConfig.Global("wildRareCoatChance", 0.03f)));
        }
    }
}
