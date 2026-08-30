namespace canrpgclasses.Core.Resources
{
    /// <summary>
    /// A class's primary resource pool (energy/rage/mana) - the fuel a spell's <c>Cost.Resource</c> is spent
    /// from. The live value lives in WatchedAttributes via <see cref="ResourceState"/>.
    /// </summary>
    public class ResourcePoolDef
    {
        /// <summary>Stable id, also the WatchedAttributes/lang suffix, e.g. <c>energy</c>, <c>rage</c>, <c>mana</c>.</summary>
        public string Id = "energy";
        public float Max = 100f;
        /// <summary>Per-second change while idle. Positive regenerates (energy/mana); 0 or negative for rage-like pools.</summary>
        public float RegenPerSec;
        /// <summary>Whether the pool starts full (energy/mana) or empty (rage).</summary>
        public bool StartFull = true;

        // HUD bar tint (kept as plain floats so core/class code carries no UI dependency).
        public float ColorR = 0.95f;
        public float ColorG = 0.82f;
        public float ColorB = 0.20f;

        /// <summary>Localized name lang key: <c>canrpgclasses:resource-&lt;Id&gt;</c> (tooltip/HUD). Falls back to the id.</summary>
        public string DisplayName => canrpgclasses.Core.LangText.Get("resource-" + Id, null) ?? canrpgclasses.Core.LangText.Humanize(Id);
    }
}
