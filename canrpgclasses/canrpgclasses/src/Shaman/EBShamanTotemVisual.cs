using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace canrpgclasses.Shaman
{
    /// <summary>Client-only: swaps the totem shape per <see cref="EBShamanTotem.KindKey"/> in OnTesselation.</summary>
    public class EBShamanTotemVisual : EntityBehavior
    {
        public const string Name = "canrpgshamantotemvisual";

        public EBShamanTotemVisual(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        public override void Initialize(EntityProperties properties, Vintagestory.API.Datastructures.JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            // The kind sync can land after the first tesselation, so re-tesselate when it arrives.
            if (entity.Api.Side == EnumAppSide.Client)
                entity.WatchedAttributes.RegisterModifiedListener(EBShamanTotem.KindKey, () => entity.MarkShapeModified());
        }

        /// <summary>Names the totem after its kind; one entity type serves all kinds, so only a behavior can tell them
        /// apart at name time.</summary>
        public override string GetName(ref EnumHandling handling)
        {
            string kind = entity.WatchedAttributes.GetString(EBShamanTotem.KindKey, "");
            if (kind == "") return base.GetName(ref handling); // not synced yet
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
                // Earthbind and Mana Spring reuse the stone and reed shapes.
                ShamanTotemSystem.KindEarthbind => "shamantotem_earth",
                ShamanTotemSystem.KindManaSpring => "shamantotem_stream",
                _ => null
            };
            if (shapeName == null) return; // not synced yet; the listener re-tesselates

            var loc = new AssetLocation(canrpgclassesModSystem.ModId, "shapes/entity/" + shapeName + ".json");
            var shape = Shape.TryGet(entity.Api, loc);
            if (shape == null) return;

            // Same as ShapeTesselatorManager does on load, otherwise element parent links are missing.
            shape.ResolveReferences(entity.Api.Logger, loc.ToString());
            entityShape = shape;
            shapeIsCloned = true;
        }
    }
}
