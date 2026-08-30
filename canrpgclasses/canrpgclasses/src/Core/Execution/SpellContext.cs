using System.Collections.Generic;
using canrpgclasses.Core.Spells;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace canrpgclasses.Core.Execution
{
    /// <summary>Mutable state carried through one spell execution (targeting → delivery → impacts).</summary>
    public class SpellContext
    {
        public EntityAgent Caster = null!;
        public Spell Spell = null!;
        public AimContext Aim = AimContext.None;
        public List<Entity> Targets = new List<Entity>();

        public float SpellPower = 1f;

        /// <summary>Combo points available to a finisher this cast (read before delivery, spent after).
        /// Finisher impacts scale by this; 0 for builders and non-combo spells.</summary>
        public int ComboPoints;
    }
}
