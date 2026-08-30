namespace canrpgclasses.Hunter
{
    /// <summary>The hunter wolf's command mode (set by the pet-order spells, read by its gated AI tasks).</summary>
    public enum PetMode
    {
        /// <summary>Hunts freely: auto-aggros nearby foes and assists the owner.</summary>
        Aggressive,
        /// <summary>Holds fire until ordered: no wandering off to fight; engages only what the owner attacks/aims.</summary>
        Defensive,
        /// <summary>Never attacks on its own; only an explicit "attack" order will engage it.</summary>
        Passive
    }
}
