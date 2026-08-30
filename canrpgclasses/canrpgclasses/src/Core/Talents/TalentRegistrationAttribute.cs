using System;

namespace canrpgclasses.Core.Talents
{
    /// <summary>Marks a <see cref="Talent"/> subclass for discovery by <see cref="TalentRegistry"/>.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class TalentRegistrationAttribute : Attribute
    {
        public string TalentId { get; }
        public TalentRegistrationAttribute(string talentId) { TalentId = talentId; }
    }
}
