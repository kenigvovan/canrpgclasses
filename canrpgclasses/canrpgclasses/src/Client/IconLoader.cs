using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Cached icon textures: tries <c>.svg</c>, then <c>.png</c>. Rasterized SVGs are ours to dispose, PNGs
    /// belong to the engine.
    /// </summary>
    public class IconLoader : IDisposable
    {
        private readonly ICoreClientAPI capi;
        private readonly Dictionary<string, (int Id, LoadedTexture? Owned)> cache = new();

        /// <summary>Engine-owned PNG textures, wrapped only so <see cref="GetTex"/> can report the size.</summary>
        private readonly Dictionary<string, LoadedTexture> engineOwned = new();

        public IconLoader(ICoreClientAPI capi) { this.capi = capi; }

        /// <summary>Asset path of a mod icon by its bare name (no extension), e.g. a spell's IconName.</summary>
        public static string PathFor(string iconName) => "canrpgclasses:textures/icons/" + iconName;

        /// <summary>Icon path from an explicit icon name, falling back to the id.</summary>
        public static string PathFor(string? iconName, string fallbackId)
            => PathFor(string.IsNullOrEmpty(iconName) ? fallbackId : iconName!);

        /// <summary>The icon texture, or null. Preferred over <see cref="Get"/> since vanilla GUI renders from a
        /// <see cref="LoadedTexture"/>.</summary>
        public LoadedTexture? GetTex(string basePath)
        {
            Load(basePath);
            return cache[basePath].Owned ?? (engineOwned.TryGetValue(basePath, out var lt) ? lt : null);
        }

        public int Get(string basePath)
        {
            Load(basePath);
            return cache[basePath].Id;
        }

        private void Load(string basePath)
        {
            if (cache.ContainsKey(basePath)) return;

            int tex = 0;
            LoadedTexture? owned = null;
            try
            {
                var svg = new AssetLocation(basePath + ".svg");
                if (capi.Assets.TryGet(svg) != null)
                {
                    owned = capi.Gui.LoadSvgWithPadding(svg, 64, 64, 2);
                    tex = owned?.TextureId ?? 0;
                }
            }
            catch { }

            if (tex == 0)
            {
                owned = null;
                try
                {
                    var png = new AssetLocation(basePath + ".png");
                    if (capi.Assets.TryGet(png) != null)
                    {
                        // Engine owns PNG textures; IgnoreUndisposed silences the finalizer warning.
                        var lt = new LoadedTexture(capi);
                        capi.Render.GetOrLoadTexture(png, ref lt);
                        tex = lt.TextureId;
                        if (tex != 0) { lt.IgnoreUndisposed = true; engineOwned[basePath] = lt; }
                    }
                }
                catch { }
            }

            cache[basePath] = (tex, owned);
        }

        public void Dispose()
        {
            foreach (var entry in cache.Values) entry.Owned?.Dispose();
            cache.Clear();
            engineOwned.Clear();
        }
    }
}
