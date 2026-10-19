namespace canrpgclasses.Core.Control
{
    /// <summary>Marks an add-on AI task that attacks (casting, melee, chasing to attack). Fear blocks these on mobs.</summary>
    public interface IOffensiveAiTask { }

    /// <summary>Marks an add-on AI task that moves the mob on purpose (chase, kite, follow). Fear and root block these on
    /// mobs; a flee task must not carry it, fear relies on fleeing.</summary>
    public interface IMovementAiTask { }
}
