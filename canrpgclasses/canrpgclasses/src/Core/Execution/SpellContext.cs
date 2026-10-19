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

        /// <summary>Finisher combo points, read before delivery and spent after; 0 for non-finishers.</summary>
        public int ComboPoints;

        public int ImpactSounds;
    }
}
