namespace canrpgclasses.Core.Effects
{
    /// <summary>
    /// An effect whose tick scales off the caster's spell power. <c>ApplyStatusEffect</c> hands an effect only a
    /// tier and a duration, so the executor snapshots the caster onto the instance just before applying it.
    /// </summary>
    public interface ISnapshotEffect
    {
        /// <summary>Records the caster's id, for damage attribution at tick time, and their spell power.</summary>
        void CaptureCaster(long casterEntityId, float casterSpellPower);
    }
}
