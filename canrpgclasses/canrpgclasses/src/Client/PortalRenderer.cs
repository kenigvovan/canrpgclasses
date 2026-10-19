using System.Collections.Generic;
using canrpgclasses.Core.Net;
using canrpgclasses.Core.Portals;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace canrpgclasses.Client
{
    /// <summary>Draws open portals from <see cref="PortalStatePacket"/>s: a shimmering plane per end (portalplane
    /// shader, ported from candungeons) plus sparks drifting up through it. Cosmetic only.</summary>
    public class PortalRenderer : IRenderer
    {
        private sealed class End
        {
            public int Dim;
            public Vec3d Foot = null!;
            public bool AlongX;
            public Vec3f TintA = null!, TintB = null!;
            public int SparkColor;
            /// <summary>Destination snapshot shown instead of the bent view; null = none.</summary>
            public LoadedTexture? Image;
        }

        private readonly ICoreClientAPI capi;
        private readonly Dictionary<long, End[]> portals = new();
        private readonly Matrixf matrix = new();
        private readonly MeshRef quad;
        private readonly long glowTickId;
        private IShaderProgram? prog;
        private SimpleParticleProperties? glow;
        private float counter;
        private string? captureKey;

        public double RenderOrder => 0.05;
        public int RenderRange => 100;

        public PortalRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;
            quad = capi.Render.UploadMesh(QuadMeshUtil.GetQuad());
            capi.Event.ReloadShader += LoadShader;
            LoadShader();
            capi.Event.RegisterRenderer(this, EnumRenderStage.AfterBlit, "canrpgclasses-portalplane");
            glowTickId = capi.Event.RegisterGameTickListener(_ => Glow(), 200);
        }

        private bool LoadShader()
        {
            prog = capi.Shader.NewShaderProgram();
            prog.AssetDomain = canrpgclassesModSystem.ModId;
            prog.VertexShader = capi.Shader.NewShader(EnumShaderType.VertexShader);
            prog.FragmentShader = capi.Shader.NewShader(EnumShaderType.FragmentShader);
            capi.Shader.RegisterFileShaderProgram("portalplane", prog);
            return prog.Compile();
        }

        public void OnPacket(PortalStatePacket p)
        {
            Remove(p.Id);
            if (!p.Open) return;

            var a = ColorOf(p.TintA);
            var b = ColorOf(p.TintB);
            int spark = ColorUtil.ToRgba(220, (int)(b.X * 255), (int)(b.Y * 255), (int)(b.Z * 255));
            portals[p.Id] = new[]
            {
                new End { Dim = p.Dim, Foot = new Vec3d(p.AX, p.AY, p.AZ), AlongX = PortalGeometry.AlongX(p.AYaw), TintA = a, TintB = b, SparkColor = spark, Image = LoadImage(p.ImageA) },
                new End { Dim = p.Dim, Foot = new Vec3d(p.BX, p.BY, p.BZ), AlongX = PortalGeometry.AlongX(p.BYaw), TintA = a, TintB = b, SparkColor = spark }
            };
        }

        private void Remove(long id)
        {
            if (!portals.TryGetValue(id, out var ends)) return;
            foreach (var e in ends) e.Image?.Dispose();
            portals.Remove(id);
        }

        private LoadedTexture? LoadImage(byte[]? data)
        {
            if (PortalShots.Decode(data) is not int[] px) return null;
            var tex = new LoadedTexture(capi, 0, PortalShots.Width, PortalShots.Height);
            capi.Render.LoadOrUpdateTextureFromBgra(px, true, 1, ref tex);
            return tex;
        }

        /// <summary>Server asked for a destination snapshot: taken on the next frame, before the GUI is drawn.</summary>
        public void RequestCapture(string key) => captureKey = key;

        private void Capture()
        {
            string key = captureKey!;
            captureKey = null;
            try
            {
                using var bmp = capi.Render.GrabScreenshot(capi.Render.FrameWidth, capi.Render.FrameHeight, false, true);
                var data = PortalShots.Encode(bmp.Pixels, bmp.Width, bmp.Height);
                canrpgclassesModSystem.ClientInstance?.ClientChannel?.SendPacket(new PortalShotPacket { Key = key, Data = data });
            }
            catch (System.Exception e)
            {
                capi.Logger.Warning("[canrpgclasses] portal snapshot failed: {0}", e.Message);
            }
        }

        private static Vec3f ColorOf(int rgb)
            => new Vec3f(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        // Camera position carries the dimension stride in Y.
        private static double InternalY(End e) => e.Foot.Y + e.Dim * BlockPos.DimensionBoundary;

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            // AfterBlit: the world is on screen, the HUD not yet. Before drawing portals, so they aren't in the shot.
            if (captureKey != null) Capture();
            if (portals.Count == 0 || prog == null || prog.LoadError) return;

            var render = capi.Render;
            var player = capi.World.Player.Entity;
            var cam = player.CameraPos;
            counter = GameMath.Mod(counter + deltaTime, 628.31854f);

            // Culling is already off after the blit and the GUI drawn next expects it off.
            render.GLDepthMask(false);
            render.GlDisableCullFace();
            prog.Use();
            prog.Uniform("rgbaFogIn", render.FogColor);
            prog.Uniform("fogMinIn", render.FogMin);
            prog.Uniform("fogDensityIn", render.FogDensity);
            // Unit 0 last: the GUI drawn next binds its textures to the active unit.
            prog.BindTexture2D("depthTex", render.FrameBuffers[0].DepthTextureId, 1);
            prog.BindTexture2D("primaryFb", render.FrameBuffers[0].ColorTextureIds[0], 0);
            prog.UniformMatrix("projectionMatrix", render.CurrentProjectionMatrix);
            prog.Uniform("counter", counter);
            prog.Uniform("invFrameSize", 1f / render.FrameWidth, 1f / render.FrameHeight);
            prog.Uniform("planeSize", PortalGeometry.Width, PortalGeometry.Height);

            foreach (var ends in portals.Values)
            {
                foreach (var e in ends)
                {
                    if (e.Dim != player.Pos.Dimension) continue;

                    float dx = (float)(e.Foot.X - cam.X);
                    float dy = (float)(InternalY(e) + PortalGeometry.Height / 2 - cam.Y);
                    float dz = (float)(e.Foot.Z - cam.Z);

                    matrix.Identity().Translate(dx, dy, dz).ReverseMul(render.CameraMatrixOriginf);
                    if (!e.AlongX) matrix.RotateYDeg(90);
                    // The quad spans -1..1.
                    matrix.Scale(PortalGeometry.Width / 2, PortalGeometry.Height / 2, 1);

                    prog.UniformMatrix("modelViewMatrix", matrix.Values);
                    prog.Uniform("worldPos", new Vec4f(dx, dy, dz, 0));
                    prog.Uniform("tintA", e.TintA);
                    prog.Uniform("tintB", e.TintB);
                    prog.Uniform("hasImage", e.Image != null ? 1f : 0f);
                    if (e.Image != null)
                    {
                        // Quad u runs along +X, or -Z once rotated; mirror it when that points to the viewer's left.
                        double uDotRight = e.AlongX ? -dz : -dx;
                        prog.Uniform("flipU", uDotRight < 0 ? 1f : 0f);
                        prog.BindTexture2D("imageTex", e.Image.TextureId, 2);
                        prog.BindTexture2D("primaryFb", render.FrameBuffers[0].ColorTextureIds[0], 0);
                    }
                    render.RenderMesh(quad);
                }
            }

            prog.Stop();
            render.GLDepthMask(true);
        }

        private void Glow()
        {
            if (portals.Count == 0) return;
            var player = capi.World.Player?.Entity;
            if (player == null) return;

            glow ??= new SimpleParticleProperties(1, 2, 0, new Vec3d(), new Vec3d(),
                new Vec3f(-0.05f, 0.1f, -0.05f), new Vec3f(0.05f, 0.3f, 0.05f),
                2f, -0.01f, 0.15f, 0.3f, EnumParticleModel.Quad)
            {
                VertexFlags = 128,
                WithTerrainCollision = false,
                SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -0.15f)
            };

            double thin = 2 * PortalGeometry.PlaneDepth;
            foreach (var ends in portals.Values)
            {
                foreach (var e in ends)
                {
                    if (e.Dim != player.Pos.Dimension || player.Pos.SquareDistanceTo(e.Foot) > 48 * 48) continue;
                    double halfW = PortalGeometry.Width / 2;
                    glow.Color = e.SparkColor;
                    glow.MinPos.Set(e.Foot.X - (e.AlongX ? halfW : PortalGeometry.PlaneDepth), InternalY(e),
                        e.Foot.Z - (e.AlongX ? PortalGeometry.PlaneDepth : halfW));
                    glow.AddPos.Set(e.AlongX ? PortalGeometry.Width : thin, PortalGeometry.Height - 0.5, e.AlongX ? thin : PortalGeometry.Width);
                    capi.World.SpawnParticles(glow);
                }
            }
        }

        public void Dispose()
        {
            capi.Event.ReloadShader -= LoadShader;
            capi.Event.UnregisterRenderer(this, EnumRenderStage.AfterBlit);
            capi.Event.UnregisterGameTickListener(glowTickId);
            quad.Dispose();
            prog?.Dispose();
            foreach (var ends in portals.Values)
                foreach (var e in ends) e.Image?.Dispose();
            portals.Clear();
        }
    }
}
