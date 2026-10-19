using System;
using System.Collections.Generic;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.Effects;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace canrpgclasses.Client
{
    /// <summary>Client side of the utility buffs. Player physics runs on the owning client, so slow fall and water
    /// walking adjust the local player's motion each frame; the light is set on every flagged player nearby.</summary>
    public class UtilityBuffsClient : IRenderer
    {
        private readonly ICoreClientAPI capi;
        private readonly long tickId;
        private readonly HashSet<long> lit = new();
        private readonly BlockPos probe = new(0, 0, 0);
        private SimpleParticleProperties? orb;

        // Before player physics (1.0), so our change is what the physics step works with.
        public double RenderOrder => 0.4;
        public int RenderRange => 0;

        public UtilityBuffsClient(ICoreClientAPI capi)
        {
            this.capi = capi;
            capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "canrpgclasses-utilitybuffs");
            tickId = capi.Event.RegisterGameTickListener(OnTick, 200);
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            var e = capi.World?.Player?.Entity;
            if (e == null || !e.Alive) return;
            var wa = e.WatchedAttributes;

            if (wa.GetBool(UtilityFlags.WaterWalk)) WaterWalk(e);

            if (wa.GetBool(UtilityFlags.SlowFall) && !e.FeetInLiquid)
            {
                double max = BalanceConfig.Spell("feather_fall").F("maxFallSpeed", 0.08f);
                if (e.Pos.Motion.Y < -max) e.Pos.Motion.Y = -max;
            }
        }

        /// <summary>Keeps the feet on the surface of water (not lava). Sneak to dive.</summary>
        private void WaterWalk(EntityPlayer e)
        {
            if (e.Controls.Sneak) return;
            var ba = capi.World.BlockAccessor;
            var pos = e.Pos;
            probe.Set((int)Math.Floor(pos.X), (int)Math.Floor(pos.Y - 0.05), (int)Math.Floor(pos.Z));
            probe.dimension = pos.Dimension;
            if (!IsWater(ba.GetBlock(probe, BlockLayersAccess.Fluid))) return;

            // Top of the water column, at most 3 blocks up.
            int top = probe.Y + 1;
            for (int i = 0; i < 3; i++)
            {
                probe.Y = top;
                if (!IsWater(ba.GetBlock(probe, BlockLayersAccess.Fluid))) break;
                top++;
            }

            if (pos.Y < top - 1.5)
            {
                // Deep under: float up.
                if (pos.Motion.Y < 0.15) pos.Motion.Y = 0.15;
                return;
            }
            // A jump still leaves the surface.
            if (pos.Motion.Y > 0.02) return;
            pos.Y = top;
            pos.Motion.Y = 0;
            // Without it physics treats the player as airborne: weak control and almost no friction, like ice.
            e.OnGround = true;
        }

        private static bool IsWater(Block b) => b.IsLiquid() && b.LiquidCode != "lava";

        private void OnTick(float dt)
        {
            var world = capi.World;
            if (world?.Player?.Entity == null) return;
            var me = world.Player.Entity;
            var light = new byte[]
            {
                (byte)BalanceConfig.Spell("guiding_light").I("lightHue", 8),
                (byte)BalanceConfig.Spell("guiding_light").I("lightSaturation", 2),
                (byte)BalanceConfig.Spell("guiding_light").I("lightBrightness", 16)
            };

            foreach (var p in world.AllOnlinePlayers)
            {
                if (p?.Entity is not EntityPlayer e) continue;
                bool on = e.Alive && e.WatchedAttributes.GetBool(UtilityFlags.MagicLight);
                if (on)
                {
                    e.LightHsv = light;
                    lit.Add(e.EntityId);
                    if (e.Pos.SquareDistanceTo(me.Pos.XYZ) < 48 * 48) Orb(e);
                }
                else if (lit.Remove(e.EntityId))
                {
                    e.LightHsv = null;
                }
            }
        }

        /// <summary>A small glowing mote hovering over the shoulder, so others see where the light comes from.</summary>
        private void Orb(EntityPlayer e)
        {
            orb ??= new SimpleParticleProperties(1, 2, ColorUtil.ToRgba(230, 255, 235, 170), new Vec3d(), new Vec3d(),
                new Vec3f(-0.02f, -0.01f, -0.02f), new Vec3f(0.02f, 0.03f, 0.02f), 0.5f, 0f, 0.25f, 0.4f, EnumParticleModel.Quad)
            {
                VertexFlags = 255,
                WithTerrainCollision = false,
                SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -0.3f)
            };

            // Behind the right shoulder, relative to the body's facing.
            double yaw = e.BodyYaw;
            double sx = Math.Sin(yaw + GameMath.PIHALF) * 0.45 - Math.Sin(yaw) * 0.2;
            double sz = Math.Cos(yaw + GameMath.PIHALF) * 0.45 - Math.Cos(yaw) * 0.2;
            double y = e.Pos.InternalY + e.LocalEyePos.Y + 0.35;
            orb.MinPos.Set(e.Pos.X + sx - 0.05, y, e.Pos.Z + sz - 0.05);
            orb.AddPos.Set(0.1, 0.1, 0.1);
            capi.World.SpawnParticles(orb);
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Before);
            capi.Event.UnregisterGameTickListener(tickId);
        }
    }
}
