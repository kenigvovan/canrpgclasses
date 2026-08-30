using System;
using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace canrpgclasses.Client.Render
{
    /// <summary>
    /// 2D primitives for the mod's HUD and world overlay: tinted textured quads in raw framebuffer pixels, with
    /// the non-rectangle shapes baked once with Cairo. No per-frame Cairo work and no shader. Every texture here
    /// is owned by this class and must be released.
    /// </summary>
    public class Hud2D : IDisposable
    {
        /// <summary>Frames in the baked cooldown sweep. 64 steps is finer than the eye can follow on a slot-sized
        /// icon, and the whole set is a few hundred KB of VRAM.</summary>
        private const int SweepFrames = 64;

        /// <summary>Edge length of every baked shape texture. They are only ever scaled down (a hotbar slot is
        /// 24-96px), so this is plenty and keeps the bake cheap.</summary>
        private const int BakeSize = 64;

        /// <summary>Text textures live until their string stops being drawn. Damage numbers keep minting new
        /// strings, so the cache is bounded and swept - see <see cref="SweepTextCache"/>.</summary>
        private const int MaxTextCache = 256;
        private const long TextEntryTtlMs = 60_000;

        private readonly ICoreClientAPI capi;

        private LoadedTexture? whiteTex;
        private LoadedTexture? circleTex;
        private LoadedTexture? ringTex;
        private LoadedTexture?[]? sweepTex;

        private readonly Dictionary<(string Text, int FontSize, string FontName), CachedText> textCache = new();
        private long lastTextSweepMs;

        private class CachedText
        {
            public LoadedTexture Texture = null!;
            public long LastUsedMs;
        }

        public Hud2D(ICoreClientAPI capi)
        {
            this.capi = capi;
        }

        public void Rect(float x, float y, float w, float h, Vec4f color)
        {
            if (w <= 0 || h <= 0) return;
            capi.Render.Render2DTexture(White(), x, y, w, h, 50f, color);
        }

        public void Frame(float x, float y, float w, float h, float thickness, Vec4f color)
        {
            if (w <= 0 || h <= 0 || thickness <= 0) return;
            float t = Math.Min(thickness, Math.Min(w, h) / 2f);
            Rect(x, y, w, t, color);                 // top
            Rect(x, y + h - t, w, t, color);         // bottom
            Rect(x, y + t, t, h - 2 * t, color);     // left
            Rect(x + w - t, y + t, t, h - 2 * t, color); // right
        }

        /// <summary>Horizontal or vertical line of the given thickness, centred on the segment.</summary>
        public void Line(float x1, float y1, float x2, float y2, float thickness, Vec4f color)
        {
            float half = thickness / 2f;
            if (Math.Abs(y1 - y2) < 0.01f)
            {
                float left = Math.Min(x1, x2), right = Math.Max(x1, x2);
                Rect(left, y1 - half, right - left, thickness, color);
            }
            else
            {
                float top = Math.Min(y1, y2), bottom = Math.Max(y1, y2);
                Rect(x1 - half, top, thickness, bottom - top, color);
            }
        }

        public void Circle(float cx, float cy, float radius, Vec4f color)
        {
            circleTex ??= Bake(ctx =>
            {
                ctx.Arc(BakeSize / 2.0, BakeSize / 2.0, BakeSize / 2.0 - 1.0, 0, Math.PI * 2);
                ctx.Fill();
            });
            float d = radius * 2f;
            capi.Render.Render2DTexture(circleTex.TextureId, cx - radius, cy - radius, d, d, 50f, color);
        }

        /// <summary>Circle outline centred on (cx, cy). Stroke width scales with the circle, matching the old
        /// fixed 1.5px look at the pip sizes the HUD actually uses.</summary>
        public void Ring(float cx, float cy, float radius, Vec4f color)
        {
            ringTex ??= Bake(ctx =>
            {
                ctx.LineWidth = 4.0;
                ctx.Arc(BakeSize / 2.0, BakeSize / 2.0, BakeSize / 2.0 - 2.0, 0, Math.PI * 2);
                ctx.Stroke();
            });
            float d = radius * 2f;
            capi.Render.Render2DTexture(ringTex.TextureId, cx - radius, cy - radius, d, d, 50f, color);
        }

        /// <summary>The cooldown pie: covers the part of the slot that is still on cooldown, receding clockwise
        /// from 12 o'clock as the cooldown elapses. <paramref name="elapsed"/> is 0 (just started) to 1 (done).
        ///
        /// The 64 possible wedges are baked once, so a sweep costs one tinted quad rather than a per-frame
        /// triangle fan.</summary>
        public void CooldownSweep(float x, float y, float size, float elapsed, Vec4f color)
        {
            sweepTex ??= BakeSweep();
            int frame = (int)(Math.Clamp(elapsed, 0f, 1f) * (SweepFrames - 1));
            var tex = sweepTex[frame];
            if (tex != null) capi.Render.Render2DTexture(tex.TextureId, x, y, size, size, 50f, color);
        }

        public void Icon(LoadedTexture? tex, float x, float y, float w, float h, Vec4f? color = null)
        {
            if (tex == null || tex.TextureId == 0) return;
            capi.Render.Render2DTexture(tex.TextureId, x, y, w, h, 50f, color);
        }

        /// <summary>Draws text at (x, y) - the top-left corner. The glyph texture is cached white and tinted at
        /// draw time, so the same string in a different colour costs nothing extra.
        ///
        /// <paramref name="shadowed"/> repeats the draw four times in black around the text, which is what keeps
        /// HUD numbers readable against the world.</summary>
        public void Text(string text, CairoFont font, float x, float y, Vec4f color, bool shadowed = true)
        {
            if (string.IsNullOrEmpty(text)) return;
            var tex = TextTexture(text, font);
            if (tex == null) return;

            if (shadowed)
            {
                var shadow = new Vec4f(0f, 0f, 0f, 0.85f * color.W);
                capi.Render.Render2DTexture(tex.TextureId, x - 1, y, tex.Width, tex.Height, 49f, shadow);
                capi.Render.Render2DTexture(tex.TextureId, x + 1, y, tex.Width, tex.Height, 49f, shadow);
                capi.Render.Render2DTexture(tex.TextureId, x, y - 1, tex.Width, tex.Height, 49f, shadow);
                capi.Render.Render2DTexture(tex.TextureId, x, y + 1, tex.Width, tex.Height, 49f, shadow);
            }
            capi.Render.Render2DTexture(tex.TextureId, x, y, tex.Width, tex.Height, 50f, color);
        }

        public void TextCentered(string text, CairoFont font, float cx, float cy, Vec4f color, bool shadowed = true)
        {
            if (string.IsNullOrEmpty(text)) return;
            var tex = TextTexture(text, font);
            if (tex == null) return;
            Text(text, font, cx - tex.Width / 2f, cy - tex.Height / 2f, color, shadowed);
        }

        public Vec2f TextSize(string text, CairoFont font)
        {
            var tex = TextTexture(text, font);
            return tex == null ? new Vec2f(0, 0) : new Vec2f(tex.Width, tex.Height);
        }

        private LoadedTexture? TextTexture(string text, CairoFont font)
        {
            var key = (text, (int)font.UnscaledFontsize, font.Fontname ?? "");
            if (textCache.TryGetValue(key, out var hit))
            {
                hit.LastUsedMs = capi.ElapsedMilliseconds;
                return hit.Texture;
            }

            LoadedTexture tex;
            try
            {
                var white = font.Clone().WithColor(new double[] { 1, 1, 1, 1 });
                tex = capi.Gui.TextTexture.GenUnscaledTextTexture(text, white);
            }
            catch { return null; }

            textCache[key] = new CachedText { Texture = tex, LastUsedMs = capi.ElapsedMilliseconds };
            SweepTextCache();
            return tex;
        }

        /// <summary>Drops text textures that have gone unused. Without this, floating damage numbers would grow
        /// the cache without bound - every distinct amount is its own string.</summary>
        private void SweepTextCache()
        {
            long now = capi.ElapsedMilliseconds;
            if (textCache.Count <= MaxTextCache && now - lastTextSweepMs < 10_000) return;
            lastTextSweepMs = now;

            var stale = new List<(string, int, string)>();
            foreach (var kv in textCache)
                if (now - kv.Value.LastUsedMs > TextEntryTtlMs) stale.Add(kv.Key);

            // Over the cap even after dropping the idle ones: evict oldest-first until back under it.
            if (textCache.Count - stale.Count > MaxTextCache)
            {
                var byAge = new List<KeyValuePair<(string, int, string), CachedText>>(textCache);
                byAge.Sort((a, b) => a.Value.LastUsedMs.CompareTo(b.Value.LastUsedMs));
                foreach (var kv in byAge)
                {
                    if (textCache.Count - stale.Count <= MaxTextCache) break;
                    if (!stale.Contains(kv.Key)) stale.Add(kv.Key);
                }
            }

            foreach (var key in stale)
            {
                if (!textCache.TryGetValue(key, out var entry)) continue;
                entry.Texture.Dispose();
                textCache.Remove(key);
            }
        }

        /// <summary>Clips subsequent draws to a screen rectangle (pixels). Must be paired with
        /// <see cref="Unclip"/>.</summary>
        public void Clip(float x, float y, float w, float h)
        {
            float scale = RuntimeEnv.GUIScale;
            var bounds = ElementBounds.Fixed(x / scale, y / scale, w / scale, h / scale)
                .WithParent(capi.Gui.WindowBounds);
            bounds.CalcWorldBounds();
            capi.Render.PushScissor(bounds, true);
        }

        public void Unclip() => capi.Render.PopScissor();

        /// <summary>The white source every filled rectangle is stretched and tinted from.
        ///
        /// Painted with Cairo rather than uploaded as a raw pixel array: LoadTextureFromRgba handed back id 0 for
        /// a 1x1 upload, and the shader then sampled an unbound texture - every rectangle came out black, which
        /// only looked right where the colour happened to be black anyway.</summary>
        private int White()
        {
            whiteTex ??= capi.Gui.Icons.GenTexture(4, 4, (ctx, _) =>
            {
                ctx.SetSourceRGBA(1, 1, 1, 1);
                ctx.Paint();
            });
            return whiteTex.TextureId;
        }

        private LoadedTexture Bake(Action<Context> draw)
            => capi.Gui.Icons.GenTexture(BakeSize, BakeSize, (ctx, _) =>
            {
                ctx.SetSourceRGBA(1, 1, 1, 1);
                draw(ctx);
            });

        private LoadedTexture?[] BakeSweep()
        {
            var frames = new LoadedTexture?[SweepFrames];
            double c = BakeSize / 2.0;
            // Radius reaches past the corners so the wedge covers the whole square; Cairo clips to the surface.
            double r = BakeSize * 0.72;
            for (int i = 0; i < SweepFrames; i++)
            {
                double elapsed = i / (double)(SweepFrames - 1);
                if (elapsed >= 1.0) { frames[i] = null; continue; } // fully elapsed: nothing left to cover
                double from = -Math.PI / 2 + elapsed * Math.PI * 2;
                double to = -Math.PI / 2 + Math.PI * 2;
                frames[i] = Bake(ctx =>
                {
                    ctx.MoveTo(c, c);
                    ctx.Arc(c, c, r, from, to);
                    ctx.ClosePath();
                    ctx.Fill();
                });
            }
            return frames;
        }

        public void Dispose()
        {
            whiteTex?.Dispose(); whiteTex = null;
            circleTex?.Dispose(); circleTex = null;
            ringTex?.Dispose(); ringTex = null;
            if (sweepTex != null)
            {
                foreach (var t in sweepTex) t?.Dispose();
                sweepTex = null;
            }
            foreach (var entry in textCache.Values) entry.Texture.Dispose();
            textCache.Clear();
        }
    }
}
