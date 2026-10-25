namespace canrpgclasses.Hunter
{
    /// <summary>The hunter wolf's command mode (set by the pet-order spells, read by its gated AI tasks).</summary>
    public enum PetMode
    {
        /// <summary>Joins the owner's fights and defends them, with no time limit on the chase. Never hunts on its own.</summary>
        Aggressive,
        /// <summary>Only defends: engages what attacks the owner or what it is sent at, for a short window.</summary>
        Defensive,
        /// <summary>Never attacks on its own; only an explicit "attack" order will engage it.</summary>
        Passive
    }
}
