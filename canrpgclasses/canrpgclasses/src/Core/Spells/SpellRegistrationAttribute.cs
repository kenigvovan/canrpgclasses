using System;

namespace canrpgclasses.Core.Spells
{
    /// <summary>Marks a <see cref="Spell"/> subclass for discovery by <see cref="SpellRegistry"/>.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SpellRegistrationAttribute : Attribute
    {
        public string SpellId { get; }

        public SpellRegistrationAttribute(string spellId)
        {
            SpellId = spellId;
        }
    }
}
