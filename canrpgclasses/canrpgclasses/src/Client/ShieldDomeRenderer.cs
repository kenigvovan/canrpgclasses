using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using canrpgclasses.Core;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Draws a translucent dome around every nearby entity carrying an active absorb shield: one shared UV-sphere
    /// mesh through the flat-colour <see cref="EnumShaderProgram.Wireframe"/> shader, so it reads as a clean
    /// energy bubble. Cosmetic only - reads synced WatchedAttributes, changes no game state.
    /// </summary>
    public class ShieldDomeRenderer : IRenderer
    {
        private readonly ICoreClientAPI capi;
        private MeshRef? sphere;   // smooth energy dome (default shields)
        private MeshRef? crystal;  // angular ice crystal (frost shields: Frozen Shell / Ice Ward)
        private readonly Matrixf mv = new Matrixf();

        // Fallback tint (pale gold) when a shield didn't record a colour. Per-shield colour comes from canrpgAbsorbColor.
        private static readonly Vec4f DomeColor = new Vec4f(0.95f, 0.88f, 0.55f, 1f);
        private readonly Vec4f colorScratch = new Vec4f(); // reused per shielded entity - Vec4f is a class

        // The spatial scan for shielded entities is throttled: shields appear/disappear on a seconds scale, so a
        // full GetEntitiesAround (which walks every entity in range and allocates a result array + a closure) every
        // frame bought nothing. Between scans the cached list is re-verified per frame via Alive/ShieldAlpha.
        private const long ScanIntervalMs = 150;
        private readonly System.Collections.Generic.List<Entity> shielded = new();
        // NB: not long.MinValue - "now - lastScanMs" would overflow and never trigger the first scan.
        private long lastScanMs = -ScanIntervalMs;
        private long scanNow; // "now" for the predicate below, set right before each scan (no per-scan closure)
        private readonly ActionConsumable<Entity> shieldMatcher;

        public double RenderOrder => 0.6; // transparent range, after opaque geometry
        public int RenderRange => 60;

        public ShieldDomeRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;
            shieldMatcher = e => e.Alive && ShieldAlpha(e, scanNow) > 0f;
            sphere = capi.Render.UploadMesh(GenSphere(16, 10));
            crystal = capi.Render.UploadMesh(GenCrystal());
            // AfterOIT, not OIT: during the OIT stage the game has the weighted-blended-transparency framebuffer
            // bound (accum/revealage attachments) - a plain shader with standard blending writes garbage there and
            // the dome never shows up in the composed frame. AfterOIT runs right after MergeTransparentRenderPass
            // with the primary framebuffer + depth test restored, which is exactly what this overlay needs.
            capi.Event.RegisterRenderer(this, EnumRenderStage.AfterOIT, "canrpgshielddome");
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (sphere == null || crystal == null) return;
            var world = capi.World;
            var self = world?.Player?.Entity;
            if (self == null) return;

            long now = world!.ElapsedMilliseconds;
            Vec3d camPos = self.CameraPos;

            if (now - lastScanMs >= ScanIntervalMs)
            {
                lastScanMs = now;
                scanNow = now;
                shielded.Clear();
                var found = world.GetEntitiesAround(camPos, RenderRange, RenderRange, shieldMatcher);
                for (int i = 0; i < found.Length; i++) shielded.Add(found[i]);

                // Prune expired client-clock deadlines so the dict doesn't grow with every shield ever seen.
                if (deadlines.Count > 0)
                {
                    expiredIds.Clear();
                    foreach (var kv in deadlines) if (kv.Value.clientUntilMs < now) expiredIds.Add(kv.Key);
                    foreach (var id in expiredIds) deadlines.Remove(id);
                }
            }
            if (shielded.Count == 0) return;

            var rpi = capi.Render;
            IShaderProgram prog = capi.Shader.GetProgram((int)EnumShaderProgram.Wireframe);
            if (prog == null) return;

            bool began = false;
            foreach (var e in shielded)
            {
                if (!e.Alive) continue; // the cached list may be up to one scan interval stale
                float alpha = ShieldAlpha(e, now);
                if (alpha <= 0f) continue;

                if (!began)
                {
                    prog.Use();
                    rpi.GLEnableDepthTest();
                    rpi.GLDepthMask(false);              // translucent: test against the scene, don't write depth
                    rpi.GlDisableCullFace();             // draw BOTH faces of the sphere - otherwise the "inward"
                                                        // winding is back-face-culled and the dome is invisible from
                                                        // outside (and you're inside your own dome in 1st person)
                    rpi.GlToggleBlend(true, EnumBlendMode.Standard);
                    prog.Uniform("origin", 0f, 0f, 0f);
                    prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);
                    began = true;
                }

                var wa = e.WatchedAttributes;
                int style = wa.GetInt(CombatFlags.AbsorbStyle, 0); // 0 = smooth sphere, 1 = ice crystal, 2 = solid ice block
                MeshRef mesh = style >= 1 ? crystal : sphere;

                // Per-shield tint (frost = icy blue, else gold). Packed A,R,G,B like ColorUtil.ToRgba; keep our own alpha.
                int packed = wa.GetInt(CombatFlags.AbsorbColor, 0);
                float cr = DomeColor.R, cg = DomeColor.G, cb = DomeColor.B;
                if (packed != 0)
                {
                    // ColorUtil.ToRgba(a,r,g,b) = (a<<24)|(r<<16)|(g<<8)|b, so r is bits 16-23, b is the low byte.
                    cr = ((packed >> 16) & 0xff) / 255f;
                    cg = ((packed >> 8) & 0xff) / 255f;
                    cb = (packed & 0xff) / 255f;
                }

                // Style 2 (Frozen Shell): a fatter, near-opaque solid ice encasement so it reads clearly different from
                // Ice Ward's thin translucent crystal (style 1).
                bool solid = style == 2;
                float grow = solid ? 0.35f : 0f;
                float drawAlpha = solid ? Math.Min(0.72f, alpha * 4f) : alpha;

                var box = e.SelectionBox;
                double cx = e.Pos.X, cy = e.Pos.Y + box.Y2 * 0.5, cz = e.Pos.Z;
                float rxz = Math.Max(box.XSize, box.ZSize) * 0.5f + 0.45f + grow;
                float ry = box.Y2 * 0.5f + 0.5f + grow;

                mv.Identity().Set(rpi.CameraMatrixOrigin)
                  .Translate(cx - camPos.X, cy - camPos.Y, cz - camPos.Z)
                  .Scale(rxz, ry, rxz);

                prog.UniformMatrix("modelViewMatrix", mv.Values);
                prog.Uniform("colorIn", colorScratch.Set(cr, cg, cb, drawAlpha));
                rpi.RenderMesh(mesh);
            }

            if (began)
            {
                prog.Stop();
                rpi.GlEnableCullFace();  // restore the default culling for everything drawn after us
                rpi.GLDepthMask(true);
            }
        }

        /// <summary>Dome opacity for an entity: 0 if no active shield, else a gentle pulse that fades out over the
        /// final second so the bubble visibly "runs out".</summary>
        // CLIENT-clock deadlines per shielded entity. AbsorbUntil is written with the server's ElapsedMilliseconds -
        // a different counter than the client's - so comparing it against the client clock always read as "already
        // expired" and no dome ever rendered. Instead we detect a (re)applied shield by its AbsorbUntil VALUE changing
        // (used only as a version stamp, never compared to our clock) and arm a deadline from the synced
        // AbsorbDuration seconds on the client's own clock.
        private readonly System.Collections.Generic.Dictionary<long, (long serverUntil, long clientUntilMs)> deadlines = new();
        private readonly System.Collections.Generic.List<long> expiredIds = new(); // scratch for the per-scan prune

        private float ShieldAlpha(Entity e, long now)
        {
            var wa = e.WatchedAttributes;
            if (wa == null || wa.GetFloat(CombatFlags.Absorb, 0f) <= 0f) return 0f;

            long serverUntil = wa.GetLong(CombatFlags.AbsorbUntil, 0);
            if (!deadlines.TryGetValue(e.EntityId, out var d) || d.serverUntil != serverUntil)
            {
                float durSec = wa.GetFloat(CombatFlags.AbsorbDuration, 0f);
                if (durSec <= 0f) return 0f; // no duration synced (stale pre-fix shield) - nothing to time against
                d = (serverUntil, now + (long)(durSec * 1000f));
                deadlines[e.EntityId] = d;
            }
            long until = d.clientUntilMs;
            if (now > until) return 0f;

            float baseA = 0.16f + 0.05f * (float)Math.Sin(now / 260.0); // shimmer
            float secsLeft = (until - now) / 1000f;
            if (secsLeft < 1f) baseA *= Math.Max(0f, secsLeft); // fade in the last second
            return baseA;
        }

        // A unit UV-sphere (radius 1, centred at origin) as triangles: xyz + white rgba + glow flags, matching the
        // vertex format the Wireframe shader expects (same components LineMeshUtil produces for the block outline).
        private static MeshData GenSphere(int slices, int stacks)
        {
            int vcount = (stacks + 1) * (slices + 1);
            int icount = stacks * slices * 6;
            var m = new MeshData(vcount, icount, false, false, true, true);

            int vi = 0;
            for (int i = 0; i <= stacks; i++)
            {
                double theta = Math.PI * i / stacks;
                double sinT = Math.Sin(theta), cosT = Math.Cos(theta);
                for (int j = 0; j <= slices; j++)
                {
                    double phi = 2.0 * Math.PI * j / slices;
                    m.xyz[vi * 3] = (float)(sinT * Math.Cos(phi));
                    m.xyz[vi * 3 + 1] = (float)cosT;
                    m.xyz[vi * 3 + 2] = (float)(sinT * Math.Sin(phi));
                    m.Rgba[vi * 4] = 255; m.Rgba[vi * 4 + 1] = 255; m.Rgba[vi * 4 + 2] = 255; m.Rgba[vi * 4 + 3] = 255;
                    m.Flags[vi] = 256; // self-lit-ish, same flag the wireframe cube uses
                    vi++;
                }
            }
            m.VerticesCount = vcount;

            for (int i = 0; i < stacks; i++)
                for (int j = 0; j < slices; j++)
                {
                    int a = i * (slices + 1) + j;
                    int b = a + slices + 1;
                    m.AddIndex(a); m.AddIndex(b); m.AddIndex(a + 1);
                    m.AddIndex(a + 1); m.AddIndex(b); m.AddIndex(b + 1);
                }
            return m;
        }

        // An angular ice crystal: an icosahedron (20 flat facets) with each vertex pushed to a slightly different
        // radius, so the silhouette is jagged and irregular like a chunk of ice - not a smooth bubble. Same vertex
        // format as GenSphere (xyz + white rgba + glow flags) for the flat-colour Wireframe shader.
        private static MeshData GenCrystal()
        {
            float t = (float)((1.0 + Math.Sqrt(5.0)) / 2.0); // golden ratio
            float[][] v =
            {
                new[]{-1f, t, 0f}, new[]{1f, t, 0f}, new[]{-1f, -t, 0f}, new[]{1f, -t, 0f},
                new[]{0f, -1f, t}, new[]{0f, 1f, t}, new[]{0f, -1f, -t}, new[]{0f, 1f, -t},
                new[]{t, 0f, -1f}, new[]{t, 0f, 1f}, new[]{-t, 0f, -1f}, new[]{-t, 0f, 1f}
            };
            int[] idx =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            int vcount = 12, icount = idx.Length;
            var m = new MeshData(vcount, icount, false, false, true, true);
            for (int i = 0; i < vcount; i++)
            {
                float x = v[i][0], y = v[i][1], z = v[i][2];
                float len = (float)Math.Sqrt(x * x + y * y + z * z);
                // deterministic per-vertex radius 0.82..1.12 -> irregular, crystalline silhouette
                float jitter = 0.82f + 0.30f * (float)Math.Abs(Math.Sin(i * 2.3999632f));
                float s = jitter / len;
                m.xyz[i * 3] = x * s; m.xyz[i * 3 + 1] = y * s; m.xyz[i * 3 + 2] = z * s;
                m.Rgba[i * 4] = 255; m.Rgba[i * 4 + 1] = 255; m.Rgba[i * 4 + 2] = 255; m.Rgba[i * 4 + 3] = 255;
                m.Flags[i] = 256;
            }
            m.VerticesCount = vcount;
            for (int i = 0; i < icount; i++) m.AddIndex(idx[i]);
            return m;
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.OIT);
            sphere?.Dispose();
            crystal?.Dispose();
            sphere = null;
            crystal = null;
        }
    }
}
