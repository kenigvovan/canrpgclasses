namespace canrpgclasses.Core.Effects
{
    /// <summary>Effect scaled by caster spell power. <c>ApplyStatusEffect</c> only passes tier and duration, so the
    /// executor snapshots the caster onto the instance before applying it.</summary>
    public interface ISnapshotEffect
    {
        void CaptureCaster(long casterEntityId, float casterSpellPower);
    }
}
