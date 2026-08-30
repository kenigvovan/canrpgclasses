using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Loads icons into GL textures, cached by path, one instance for the whole mod. Tries an <c>.svg</c> first,
    /// then a <c>.png</c>; returns 0 when no asset exists. Rasterized SVGs are ours to dispose, PNGs belong to
    /// the engine.
    /// </summary>
    public class IconLoader : IDisposable
    {
        private readonly ICoreClientAPI capi;
        private readonly Dictionary<string, (int Id, LoadedTexture? Owned)> cache = new();

        /// <summary>PNG textures: the engine owns and frees these, we only keep the wrapper so
        /// <see cref="GetTex"/> can hand out width/height along with the id.</summary>
        private readonly Dictionary<string, LoadedTexture> engineOwned = new();

        public IconLoader(ICoreClientAPI capi) { this.capi = capi; }

        /// <summary>Asset path of a mod icon by its bare name (no extension), e.g. a spell's IconName.</summary>
        public static string PathFor(string iconName) => "canrpgclasses:textures/icons/" + iconName;

        /// <summary>Icon path for a thing that names its own icon but may fall back to its id - the rule every
        /// panel used to spell out for itself (spell, talent and class all follow it).</summary>
        public static string PathFor(string? iconName, string fallbackId)
            => PathFor(string.IsNullOrEmpty(iconName) ? fallbackId : iconName!);

        /// <summary>The loaded texture for an icon, or null if no asset exists. Preferred over <see cref="Get"/>:
        /// the vanilla GUI stack renders from a <see cref="LoadedTexture"/>, width and height included.</summary>
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
                        // The engine owns and frees PNG textures, so wrap the id without taking ownership -
                        // IgnoreUndisposed keeps its finalizer quiet since we deliberately never dispose it.
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
