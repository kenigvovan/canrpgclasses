using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace canrpgclasses.Shaman
{
    /// <summary>
    /// Client-only: swaps the totem's rendered shape per <see cref="EBShamanTotem.KindKey"/> in OnTesselation.
    /// One entity type, three shapes; a late kind sync re-tesselates via the listener.
    /// </summary>
    public class EBShamanTotemVisual : EntityBehavior
    {
        public const string Name = "canrpgshamantotemvisual";

        public EBShamanTotemVisual(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        public override void Initialize(EntityProperties properties, Vintagestory.API.Datastructures.JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            // The kind arrives via watched-attribute sync, which may land after the first tesselation - re-tesselate
            // when it does so the right shape shows up without a relog.
            if (entity.Api.Side == EnumAppSide.Client)
                entity.WatchedAttributes.RegisterModifiedListener(EBShamanTotem.KindKey, () => entity.MarkShapeModified());
        }

        /// <summary>Names the totem after its kind in the look-at readout ("Ember Totem", "Wellspring Totem", ...).
        /// One entity type serves all five kinds, so the entity's own <c>item-creature-shamantotem</c> string can only
        /// ever say "Totem" - the kind lives on a watched attribute, and only a behavior can read it at name time.</summary>
        public override string GetName(ref EnumHandling handling)
        {
            string kind = entity.WatchedAttributes.GetString(EBShamanTotem.KindKey, "");
            if (kind == "") return base.GetName(ref handling); // not synced yet: fall back to the entity's own name
            handling = EnumHandling.PreventDefault;
            return Lang.Get("canrpgclasses:entity-shamantotem-" + kind);
        }

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
        {
            base.OnTesselation(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
            if (entity.Api.Side != EnumAppSide.Client) return;

            string? shapeName = entity.WatchedAttributes.GetString(EBShamanTotem.KindKey, "") switch
            {
                ShamanTotemSystem.KindSearing => "shamantotem_searing",
                ShamanTotemSystem.KindStream => "shamantotem_stream",
                ShamanTotemSystem.KindEarth => "shamantotem_earth",
                // Earthbind reuses the stone body (earthy snare); Mana Spring reuses the reed (a water totem). Both get
                // their own dedicated shape later if wanted - for now the geometry fits the theme.
                ShamanTotemSystem.KindEarthbind => "shamantotem_earth",
                ShamanTotemSystem.KindManaSpring => "shamantotem_stream",
                _ => null
            };
            if (shapeName == null) return; // kind not synced yet: keep the default shape until the listener re-tesselates

            var loc = new AssetLocation(canrpgclassesModSystem.ModId, "shapes/entity/" + shapeName + ".json");
            var shape = Shape.TryGet(entity.Api, loc);
            if (shape == null) return;

            // Resolve element parent/child links exactly as the game does when loading an entity shape (ShapeTesselatorManager).
            shape.ResolveReferences(entity.Api.Logger, loc.ToString());
            entityShape = shape;
            shapeIsCloned = true;
        }
    }
}
