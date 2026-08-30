using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Visuals;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Draws channel beams and shield tethers as camera-facing ribbons: one shared unit quad placed by a
    /// hand-built model matrix, so no per-frame mesh rebuild. Draws on AfterOIT - see
    /// <see cref="ShieldDomeRenderer"/> for why.
    /// </summary>
    public class BeamRenderer : IRenderer
    {
        private readonly ICoreClientAPI capi;
        private MeshRef? quad;
        private readonly Matrixf mv = new Matrixf();
        private readonly float[] model = new float[16];
        private readonly Vec4f colorScratch = new Vec4f();

        // Beam casters appear/disappear on a seconds scale - same throttled spatial scan as the shield dome.
        private const long ScanIntervalMs = 150;
        private readonly System.Collections.Generic.List<Entity> beamers = new();
        private readonly System.Collections.Generic.List<Entity> tetherers = new();
        private long lastScanMs = -ScanIntervalMs;
        private readonly ActionConsumable<Entity> beamMatcher;
        private readonly ActionConsumable<Entity> tetherMatcher;

        public double RenderOrder => 0.6;
        public int RenderRange => 60;

        public BeamRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;
            beamMatcher = e => e.Alive && (e.WatchedAttributes?.GetLong(EffectVisuals.BeamTargetAttr, 0) ?? 0) != 0;
            tetherMatcher = e => e.Alive && (e.WatchedAttributes?.GetLong(EffectVisuals.TetherTargetAttr, 0) ?? 0) != 0;
            quad = capi.Render.UploadMesh(GenUnitQuad());
            capi.Event.RegisterRenderer(this, EnumRenderStage.AfterOIT, "canrpgbeam");
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (quad == null) return;
            var world = capi.World;
            var self = world?.Player?.Entity;
            if (self == null) return;

            long now = world!.ElapsedMilliseconds;
            Vec3d camPos = self.CameraPos;

            if (now - lastScanMs >= ScanIntervalMs)
            {
                lastScanMs = now;
                beamers.Clear();
                tetherers.Clear();
                var found = world.GetEntitiesAround(camPos, RenderRange, RenderRange, beamMatcher);
                for (int i = 0; i < found.Length; i++) beamers.Add(found[i]);
                var linked = world.GetEntitiesAround(camPos, RenderRange, RenderRange, tetherMatcher);
                for (int i = 0; i < linked.Length; i++) tetherers.Add(linked[i]);
            }
            if (beamers.Count == 0 && tetherers.Count == 0) return;

            var rpi = capi.Render;
            IShaderProgram prog = capi.Shader.GetProgram((int)EnumShaderProgram.Wireframe);
            if (prog == null) return;

            bool began = false;
            foreach (var caster in beamers)
            {
                if (!caster.Alive) continue;
                long targetId = caster.WatchedAttributes.GetLong(EffectVisuals.BeamTargetAttr, 0);
                if (targetId == 0) continue; // channel ended since the last scan
                var target = world.GetEntityById(targetId);
                if (target == null || !target.Alive) continue;

                // Endpoints, camera-relative. Start a touch below the eyes and a step toward the target, so in
                // first person your own beam leaves from "your hands", not through the camera plane.
                double ax = caster.Pos.X - camPos.X;
                double ay = caster.Pos.Y + (caster.LocalEyePos.Y > 0 ? caster.LocalEyePos.Y - 0.25 : caster.SelectionBox.Y2 * 0.7) - camPos.Y;
                double az = caster.Pos.Z - camPos.Z;
                double bx = target.Pos.X - camPos.X;
                double by = target.Pos.Y + target.SelectionBox.Y2 * 0.6 - camPos.Y;
                double bz = target.Pos.Z - camPos.Z;

                double ex = bx - ax, ey = by - ay, ez = bz - az;
                double len = Math.Sqrt(ex * ex + ey * ey + ez * ez);
                if (len < 0.4) continue;
                double t = len < 1.5 ? 0 : 0.35 / len; // pull the start off the caster's face
                ax += ex * t; ay += ey * t; az += ez * t;
                ex = bx - ax; ey = by - ay; ez = bz - az;

                // Billboard basis: width axis perpendicular to both the beam and the view ray to its midpoint,
                // so the ribbon always faces the camera edge-on-never.
                double mx = ax + ex * 0.5, my = ay + ey * 0.5, mz = az + ez * 0.5; // midpoint (camera at origin)
                double sx = ey * mz - ez * my, sy = ez * mx - ex * mz, sz = ex * my - ey * mx; // cross(axis, mid)
                double slen = Math.Sqrt(sx * sx + sy * sy + sz * sz);
                if (slen < 1e-6) continue; // looking straight down the beam - it has no visible side
                sx /= slen; sy /= slen; sz /= slen;

                if (!began)
                {
                    prog.Use();
                    rpi.GLEnableDepthTest();
                    rpi.GLDepthMask(false);
                    rpi.GlDisableCullFace();
                    rpi.GlToggleBlend(true, EnumBlendMode.Standard);
                    prog.Uniform("origin", 0f, 0f, 0f);
                    prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);
                    began = true;
                }

                var school = (SpellSchool)caster.WatchedAttributes.GetInt(EffectVisuals.BeamSchoolAttr, 0);
                var pal = ParticleSpec.ForSchool(school);
                float pulse = 1f + 0.15f * (float)Math.Sin(now / 90.0);

                // Pass 1: the wide translucent sheath in the school's colour.
                DrawRibbon(rpi, prog, ax, ay, az, ex, ey, ez, sx, sy, sz, 0.34f * pulse,
                    pal.ColorR / 255f, pal.ColorG / 255f, pal.ColorB / 255f, 0.35f);
                // Pass 2: the narrow core - near-black for Shadow, white-hot otherwise.
                bool shadow = school == SpellSchool.Shadow;
                DrawRibbon(rpi, prog, ax, ay, az, ex, ey, ez, sx, sy, sz, 0.11f * pulse,
                    shadow ? 0.09f : 1f, shadow ? 0.03f : 1f, shadow ? 0.13f : 1f, 0.8f);
            }

            // Tethers: a thin STEADY cord from a shielded ally to the protector who cast it (Stone Ward). Same
            // ribbon recipe as a beam but slim and calm - a lifeline, not a channel. Shares the GL setup above.
            foreach (var bearer in tetherers)
            {
                if (!bearer.Alive) continue;
                long anchorId = bearer.WatchedAttributes.GetLong(EffectVisuals.TetherTargetAttr, 0);
                if (anchorId == 0 || anchorId == bearer.EntityId) continue; // link ended, or points at self
                var anchor = world.GetEntityById(anchorId);
                if (anchor == null || !anchor.Alive) continue;

                double ax = bearer.Pos.X - camPos.X;
                double ay = bearer.Pos.Y + bearer.SelectionBox.Y2 * 0.55 - camPos.Y;
                double az = bearer.Pos.Z - camPos.Z;
                double bx = anchor.Pos.X - camPos.X;
                double by = anchor.Pos.Y + anchor.SelectionBox.Y2 * 0.55 - camPos.Y;
                double bz = anchor.Pos.Z - camPos.Z;

                double ex = bx - ax, ey = by - ay, ez = bz - az;
                double len = Math.Sqrt(ex * ex + ey * ey + ez * ez);
                if (len < 0.5) continue;

                double mx = ax + ex * 0.5, my = ay + ey * 0.5, mz = az + ez * 0.5;
                double sx = ey * mz - ez * my, sy = ez * mx - ex * mz, sz = ex * my - ey * mx;
                double slen = Math.Sqrt(sx * sx + sy * sy + sz * sz);
                if (slen < 1e-6) continue; // looking straight down the cord
                sx /= slen; sy /= slen; sz /= slen;

                if (!began)
                {
                    prog.Use();
                    rpi.GLEnableDepthTest();
                    rpi.GLDepthMask(false);
                    rpi.GlDisableCullFace();
                    rpi.GlToggleBlend(true, EnumBlendMode.Standard);
                    prog.Uniform("origin", 0f, 0f, 0f);
                    prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);
                    began = true;
                }

                var tschool = (SpellSchool)bearer.WatchedAttributes.GetInt(EffectVisuals.TetherSchoolAttr, 0);
                var tpal = ParticleSpec.ForSchool(tschool);
                float tpulse = 0.8f + 0.2f * (float)Math.Sin(now / 150.0); // a gentle, slow breathe
                DrawRibbon(rpi, prog, ax, ay, az, ex, ey, ez, sx, sy, sz, 0.06f,
                    tpal.ColorR / 255f, tpal.ColorG / 255f, tpal.ColorB / 255f, 0.5f * tpulse);
            }

            if (began)
            {
                prog.Stop();
                rpi.GlEnableCullFace();
                rpi.GLDepthMask(true);
            }
        }

        /// <summary>One quad pass: model matrix column X = full beam axis, column Y = width along the billboard
        /// side vector, translate = start point. The unit quad (x 0..1, y ±0.5) lands exactly on the beam.</summary>
        private void DrawRibbon(IRenderAPI rpi, IShaderProgram prog,
            double ax, double ay, double az, double ex, double ey, double ez,
            double sx, double sy, double sz, float width, float r, float g, float b, float alpha)
        {
            // Column-major, like every VS matrix.
            model[0] = (float)ex; model[1] = (float)ey; model[2] = (float)ez; model[3] = 0f;
            model[4] = (float)(sx * width); model[5] = (float)(sy * width); model[6] = (float)(sz * width); model[7] = 0f;
            model[8] = 0f; model[9] = 0f; model[10] = 1f; model[11] = 0f;
            model[12] = (float)ax; model[13] = (float)ay; model[14] = (float)az; model[15] = 1f;

            mv.Set(rpi.CameraMatrixOrigin).Mul(model);
            prog.UniformMatrix("modelViewMatrix", mv.Values);
            prog.Uniform("colorIn", colorScratch.Set(r, g, b, alpha));
            rpi.RenderMesh(quad);
        }

        // A unit ribbon quad: x 0..1 along the beam, y -0.5..0.5 across it. Same vertex format as the shield dome
        // (xyz + white rgba + glow flags) for the flat-colour Wireframe shader.
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
        }
    }
}
