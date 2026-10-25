using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace canrpgclasses.Core.Entities
{
    /// <summary>Animated model held on a controlled target (Entangling Roots' vines). Not an EntityAgent, so spells
    /// and AI never target it; it despawns as soon as the target's control flag drops.</summary>
    public class EntityControlFx : Entity
    {
        private const string TargetKey = "fxTarget", FlagKey = "fxFlag", UntilKey = "fxUntil";

        public override bool IsInteractable => false;
        public override bool StoreWithChunk => false;
        public override bool ApplyGravity => false;
        public override bool ShouldReceiveDamage(DamageSource damageSource, float damage) => false;

        /// <summary>Server: spawns <paramref name="code"/> at the target, kept while <paramref name="flag"/> is set.</summary>
        public static void Spawn(Entity target, string code, string flag, float seconds)
        {
            var world = target.World;
            var type = world.GetEntityType(new AssetLocation(code));
            if (type == null) { world.Logger.Warning("[canrpgclasses] control fx entity {0} not found", code); return; }

            var fx = world.ClassRegistry.CreateEntity(type);
            fx.Pos.SetFrom(target.Pos);
            fx.Pos.Yaw = (float)world.Rand.NextDouble() * GameMath.TWOPI;
            fx.WatchedAttributes.SetLong(TargetKey, target.EntityId);
            fx.WatchedAttributes.SetString(FlagKey, flag);
            fx.WatchedAttributes.SetLong(UntilKey, world.ElapsedMilliseconds + (long)((seconds + 1f) * 1000f));
            world.SpawnEntity(fx);
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (World.Side == EnumAppSide.Client) { KeepAnimating(); return; }
            if (!Alive) return;

            var target = World.GetEntityById(WatchedAttributes.GetLong(TargetKey));
            bool held = target is { Alive: true }
                && target.WatchedAttributes.GetBool(WatchedAttributes.GetString(FlagKey) ?? "")
                && World.ElapsedMilliseconds < WatchedAttributes.GetLong(UntilKey);
            if (!held) { Die(EnumDespawnReason.Removed); return; }

            Pos.SetPos(target!.Pos.X, target.Pos.Y, target.Pos.Z);
        }

        // "emerge" holds on its last frame; "sway" loops so the vines look alive.
        private void KeepAnimating()
        {
            if (AnimManager?.Animator == null) return;
            if (!AnimManager.IsAnimationActive("sway"))
            {
                AnimManager.StartAnimation("emerge");
                AnimManager.StartAnimation("sway");
            }
        }
    }
}
