using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Draws one-shot lightning bolts as a jagged ribbon of camera-facing quads that flickers between a few
    /// pre-jittered shapes and fades over ~0.3s - crisper than the particle version. Fed by
    /// <see cref="SkillFxClient"/>; AfterOIT stage, see <see cref="ShieldDomeRenderer"/> for why.
    /// </summary>
    public class BoltRenderer : IRenderer
    {
        private readonly ICoreClientAPI capi;
        private MeshRef? quad;
        private readonly Matrixf mv = new Matrixf();
        private readonly float[] model = new float[16];
        private readonly Vec4f colorScratch = new Vec4f();

        private const float TtlMs = 320f;
        private const int Variants = 3;      // pre-jittered shapes cycled through for the flicker
        private const float FlickerMs = 70f;
        private const int MaxBolts = 48;     // hard cap so a chain storm can't grow the list unbounded

        private sealed class Bolt
        {
            public Vec3d[][] Shapes = System.Array.Empty<Vec3d[]>(); // one jagged world-point polyline per variant
            public long StartMs;
            public float R, G, B;
        }
        private readonly System.Collections.Generic.List<Bolt> bolts = new();

        public double RenderOrder => 0.6;
        public int RenderRange => 60;

        public BoltRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;
            quad = capi.Render.UploadMesh(GenUnitQuad());
            capi.Event.RegisterRenderer(this, EnumRenderStage.AfterOIT, "canrpgbolt");
        }

        /// <summary>Registers a bolt between two world points. <paramref name="color"/> is packed A,R,G,B (only
        /// R,G,B are used; alpha comes from the fade).</summary>
        public void Add(Vec3d a, Vec3d b, int color)
        {
            if (bolts.Count >= MaxBolts) bolts.RemoveAt(0); // drop the oldest rather than the new hit
            var shapes = new Vec3d[Variants][];
            for (int i = 0; i < Variants; i++) shapes[i] = Jagged(a, b);
            bolts.Add(new Bolt
            {
                Shapes = shapes,
                StartMs = capi.World.ElapsedMilliseconds,
                R = ((color >> 16) & 0xff) / 255f,
                G = ((color >> 8) & 0xff) / 255f,
                B = (color & 0xff) / 255f
            });
        }

        /// <summary>A jagged polyline from a to b: perpendicular random-walk offsets with a t·(1-t) envelope so
        /// both ends stay pinned to the actual endpoints while the middle forks about.</summary>
        private Vec3d[] Jagged(Vec3d a, Vec3d b)
        {
            var rnd = capi.World.Rand;
            Vec3d axis = b.SubCopy(a);
            double len = axis.Length();
            int n = (int)GameMath.Clamp(len / 0.45, 3, 22);

            Vec3d dir = len > 1e-6 ? axis.Clone().Normalize() : new Vec3d(0, 1, 0);
            Vec3d u = Math.Abs(dir.Y) < 0.9 ? dir.Cross(new Vec3d(0, 1, 0)) : dir.Cross(new Vec3d(1, 0, 0));
            u.Normalize();
            Vec3d v = dir.Cross(u);

            var pts = new Vec3d[n + 1];
            double ou = 0, ov = 0;
            for (int i = 0; i <= n; i++)
            {
                double t = (double)i / n;
                double env = 4.0 * t * (1.0 - t);
                ou = ou * 0.5 + (rnd.NextDouble() - 0.5) * 0.8;
                ov = ov * 0.5 + (rnd.NextDouble() - 0.5) * 0.8;
                pts[i] = new Vec3d(
                    a.X + axis.X * t + (u.X * ou + v.X * ov) * env * 0.35,
                    a.Y + axis.Y * t + (u.Y * ou + v.Y * ov) * env * 0.35,
                    a.Z + axis.Z * t + (u.Z * ou + v.Z * ov) * env * 0.35);
            }
            return pts;
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (quad == null || bolts.Count == 0) return;
            var self = capi.World?.Player?.Entity;
            if (self == null) return;

            long now = capi.World!.ElapsedMilliseconds;
            Vec3d camPos = self.CameraPos;

            var rpi = capi.Render;
            IShaderProgram prog = capi.Shader.GetProgram((int)EnumShaderProgram.Wireframe);
            if (prog == null) return;

            prog.Use();
            rpi.GLEnableDepthTest();
            rpi.GLDepthMask(false);
            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true, EnumBlendMode.Standard);
            prog.Uniform("origin", 0f, 0f, 0f);
            prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);

            for (int bi = bolts.Count - 1; bi >= 0; bi--)
            {
                var bolt = bolts[bi];
                float age = now - bolt.StartMs;
                if (age >= TtlMs) { bolts.RemoveAt(bi); continue; }

                float fade = 1f - age / TtlMs;                       // snap on, fade out
                int shapeIdx = (int)(age / FlickerMs) % Variants;     // flicker between the pre-jittered shapes
                var pts = bolt.Shapes[shapeIdx];

                for (int i = 0; i < pts.Length - 1; i++)
                {
                    DrawSegment(rpi, prog, pts[i], pts[i + 1], camPos, 0.11f, bolt.R, bolt.G, bolt.B, 0.42f * fade);
                    DrawSegment(rpi, prog, pts[i], pts[i + 1], camPos, 0.04f, 1f, 1f, 1f, 0.85f * fade);
                }
            }

            prog.Stop();
            rpi.GlEnableCullFace();
            rpi.GLDepthMask(true);
        }

        /// <summary>One ribbon quad for a segment p0→p1 (billboarded edge-on toward the camera). Model matrix
        /// column X = the segment axis, column Y = the camera-perpendicular width, translate = p0. Mirror of
        /// <see cref="BeamRenderer"/>'s DrawRibbon.</summary>
        private void DrawSegment(IRenderAPI rpi, IShaderProgram prog, Vec3d p0, Vec3d p1, Vec3d camPos,
            float width, float r, float g, float b, float alpha)
        {
            double ax = p0.X - camPos.X, ay = p0.Y - camPos.Y, az = p0.Z - camPos.Z;
            double ex = p1.X - p0.X, ey = p1.Y - p0.Y, ez = p1.Z - p0.Z;
            double mx = ax + ex * 0.5, my = ay + ey * 0.5, mz = az + ez * 0.5; // segment midpoint, camera at origin
            double sx = ey * mz - ez * my, sy = ez * mx - ex * mz, sz = ex * my - ey * mx; // cross(axis, midpoint)
            double slen = Math.Sqrt(sx * sx + sy * sy + sz * sz);
            if (slen < 1e-9) return;
            sx /= slen; sy /= slen; sz /= slen;

            model[0] = (float)ex; model[1] = (float)ey; model[2] = (float)ez; model[3] = 0f;
            model[4] = (float)(sx * width); model[5] = (float)(sy * width); model[6] = (float)(sz * width); model[7] = 0f;
            model[8] = 0f; model[9] = 0f; model[10] = 1f; model[11] = 0f;
            model[12] = (float)ax; model[13] = (float)ay; model[14] = (float)az; model[15] = 1f;

            mv.Set(rpi.CameraMatrixOrigin).Mul(model);
            prog.UniformMatrix("modelViewMatrix", mv.Values);
            prog.Uniform("colorIn", colorScratch.Set(r, g, b, alpha));
            rpi.RenderMesh(quad);
        }

        // A unit ribbon quad: x 0..1 along the segment, y -0.5..0.5 across it.
        private static MeshData GenUnitQuad()
        {
            var m = new MeshData(4, 6, false, false, true, true);
            float[] xs = { 0f, 1f, 1f, 0f };
            float[] ys = { -0.5f, -0.5f, 0.5f, 0.5f };
            for (int i = 0; i < 4; i++)
            {
                m.xyz[i * 3] = xs[i]; m.xyz[i * 3 + 1] = ys[i]; m.xyz[i * 3 + 2] = 0f;
                m.Rgba[i * 4] = 255; m.Rgba[i * 4 + 1] = 255; m.Rgba[i * 4 + 2] = 255; m.Rgba[i * 4 + 3] = 255;
                m.Flags[i] = 256;
            }
            m.VerticesCount = 4;
            m.AddIndex(0); m.AddIndex(1); m.AddIndex(2);
            m.AddIndex(0); m.AddIndex(2); m.AddIndex(3);
            return m;
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.AfterOIT);
            quad?.Dispose();
            quad = null;
            bolts.Clear();
        }
    }
}
