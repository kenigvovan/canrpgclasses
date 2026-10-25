using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace canrpgclasses.Hunter
{
    /// <summary>Client-only: dresses the pet's shape from its worn cosmetics (tree place → item code). Each item's
    /// <c>attributes.petCosmetic</c> says where and how: { place, bone, fallbackBone, kind, texture }. Parts are built
    /// from the bone's own size, so they fit any species, mod creatures included.</summary>
    public class EBPetCosmetics : EntityBehavior
    {
        public const string Name = "canrpgpetcosmetics";
        public const string Key = "canrpgPetCosmetics";

        public EBPetCosmetics(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            if (entity.Api.Side == EnumAppSide.Client)
                entity.WatchedAttributes.RegisterModifiedListener(Key, () => entity.MarkShapeModified());
        }

        /// <summary>The cosmetic description of an item, or null if it isn't a pet cosmetic.</summary>
        public static JsonObject? Spec(CollectibleObject? item)
        {
            var spec = item?.Attributes?["petCosmetic"];
            return spec != null && spec.Exists ? spec : null;
        }

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
        {
            base.OnTesselation(ref entityShape, shapePathForLogging, ref shapeIsCloned, ref willDeleteElements);
            if (entity.Api.Side != EnumAppSide.Client) return;
            var worn = entity.WatchedAttributes.GetTreeAttribute(Key);
            if (worn == null || worn.Count == 0) return;

            if (!shapeIsCloned) { entityShape = entityShape.Clone(); shapeIsCloned = true; }
            foreach (var entry in worn)
            {
                var item = entity.World.GetItem(new AssetLocation((entry.Value as StringAttribute)?.value ?? ""));
                var spec = Spec(item);
                if (spec == null) continue;

                var bone = Find(entityShape.Elements, spec["bone"].AsString(entry.Key))
                    ?? Find(entityShape.Elements, spec["fallbackBone"].AsString("head"));
                if (bone?.From == null || bone.To == null) continue;

                var part = Build(spec, bone);
                if (part != null) Attach(bone, part);
            }
        }

        /// <summary>Geometry per kind; a new kind is a new case here plus items that name it.</summary>
        private static ShapeElement? Build(JsonObject spec, ShapeElement bone)
        {
            string texture = spec["texture"].AsString("");
            return spec["kind"].AsString("band") switch
            {
                "band" => Band(bone, texture, spec["width"].AsDouble(1.2), spec["at"].AsDouble(0.3), spec["margin"].AsDouble(0.4)),
                _ => null
            };
        }

        /// <summary>A strip across the bone's long axis, a little wider than the bone. Child coordinates are relative to
        /// the parent's From corner.</summary>
        private static ShapeElement Band(ShapeElement bone, string texture, double width, double at, double margin)
        {
            double[] size = { bone.To[0] - bone.From[0], bone.To[1] - bone.From[1], bone.To[2] - bone.From[2] };
            int axis = 0;
            for (int i = 1; i < 3; i++) if (System.Math.Abs(size[i]) > System.Math.Abs(size[axis])) axis = i;

            var from = new double[3];
            var to = new double[3];
            for (int i = 0; i < 3; i++)
            {
                if (i == axis) { from[i] = size[i] * at; to[i] = from[i] + width; }
                else { from[i] = -margin; to[i] = size[i] + margin; }
            }

            var faces = new ShapeElementFace[6];
            for (int i = 0; i < 6; i++) faces[i] = new ShapeElementFace { Texture = texture, Uv = new float[] { 0, 0, 8, 2 } };
            return new ShapeElement
            {
                Name = "canrpgCosmetic",
                From = from,
                To = to,
                RotationOrigin = new double[] { 0, 0, 0 },
                FacesResolved = faces,
                JointId = bone.JointId // moves with the bone in every animation
            };
        }

        private static void Attach(ShapeElement bone, ShapeElement part)
        {
            var kids = bone.Children ?? System.Array.Empty<ShapeElement>();
            var grown = new ShapeElement[kids.Length + 1];
            kids.CopyTo(grown, 0);
            grown[kids.Length] = part;
            bone.Children = grown;
            part.ParentElement = bone;
        }

        private static ShapeElement? Find(ShapeElement[]? elements, string part)
        {
            if (elements == null || string.IsNullOrEmpty(part)) return null;
            foreach (var e in elements)
            {
                if (e.Name != null && e.Name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0) return e;
                var hit = Find(e.Children, part);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
