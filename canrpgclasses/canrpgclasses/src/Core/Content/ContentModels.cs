using System.Collections.Generic;

namespace canrpgclasses.Core.Content
{
    /// <summary>
    /// The shape of a content file: plain deserialization targets, nothing used at runtime.
    /// <see cref="ContentLoader"/> turns them into real spell/talent/class instances.
    /// </summary>
    public class ContentFileModel
    {
        public List<ClassModel>? classes { get; set; }
        public List<SpellModel>? spells { get; set; }
        public List<TalentModel>? talents { get; set; }
    }

    public class ClassModel
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? role { get; set; }
        public string? description { get; set; }
        public string? icon { get; set; }

        /// <summary>Tree names, in order. Any number is allowed; the UI follows this array.</summary>
        public List<string>? trees { get; set; }

        public ResourceModel? resource { get; set; }
        public bool usesComboPoints { get; set; }

        /// <summary>Spells available from level 1, by full id.</summary>
        public List<string>? baseSpells { get; set; }

        public float hpPerLevel { get; set; }

        /// <summary>Flat stats granted just for being this class.</summary>
        public List<StatValueModel>? baseStats { get; set; }

        /// <summary>Stats granted per level above 1 - the general form of <see cref="hpPerLevel"/>, and how a class
        /// hands out attribute points as it levels.</summary>
        public List<StatValueModel>? statsPerLevel { get; set; }

        /// <summary>Multiplier on what one point of an attribute pays this class, e.g. intelligence 0.2 for a
        /// warrior. Overrides the class entry in <c>attributes.json</c> for the attributes it names.</summary>
        public List<StatValueModel>? attributeAffinity { get; set; }

        /// <summary>"Each point in tree N also grants X of a stat."</summary>
        public List<MasteryModel>? treeMasteries { get; set; }
    }

    public class ResourceModel
    {
        public string? id { get; set; }
        public float max { get; set; } = 100f;
        public float regenPerSec { get; set; }
        public bool startFull { get; set; } = true;
        /// <summary>HUD bar tint, 0..1. Omit for the default gold.</summary>
        public float[]? color { get; set; }
    }

    public class StatValueModel
    {
        public string? stat { get; set; }
        public float value { get; set; }
    }

    public class MasteryModel
    {
        public int tree { get; set; }
        public string? stat { get; set; }
        public float perPoint { get; set; }
    }

    public class TalentModel
    {
        public string? id { get; set; }
        public string? @class { get; set; }

        /// <summary>Position: which tree, which row (tier) and which column within it.</summary>
        public int tree { get; set; }
        public int tier { get; set; }
        public int column { get; set; }
        public int maxRank { get; set; } = 1;

        public string? name { get; set; }
        public string? description { get; set; }
        public string? icon { get; set; }
        public float[]? descArgs { get; set; }

        /// <summary>Id of a talent that must be maxed first.</summary>
        public string? requires { get; set; }
        /// <summary>Attribute points needed to spend on this talent, e.g. <c>{ "strength": 10 }</c>.</summary>
        public Dictionary<string, float>? requiresAttributes { get; set; }
        /// <summary>Taking rank 1 grants this spell.</summary>
        public string? grantsSpell { get; set; }

        /// <summary>Stats this talent sets, scaled by rank.</summary>
        public List<TalentStatModel>? stats { get; set; }
    }

    public class TalentStatModel
    {
        public string? stat { get; set; }
        public float perRank { get; set; }
    }

    public class SpellModel
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? description { get; set; }
        public string? icon { get; set; }

        /// <summary>One of the SpellSchool names, e.g. Fire, Frost, Holy, Nature, PhysicalMelee.</summary>
        public string? school { get; set; }
        public int tier { get; set; } = 1;
        public float range { get; set; }

        /// <summary>Instant, Charge or Channel.</summary>
        public string? castMode { get; set; }
        public float castSeconds { get; set; }
        /// <summary>Channel only: fire the impacts this many times over the cast.</summary>
        public int channelTicks { get; set; }

        public float resourceCost { get; set; }
        public float cooldown { get; set; }
        /// <summary>Shared cooldown key, so several spells lock each other out. Omit for the spell's own id.</summary>
        public string? cooldownGroup { get; set; }
        public bool triggersGlobalCooldown { get; set; } = true;

        public TargetModel? target { get; set; }
        public DeliveryModel? delivery { get; set; }
        public RequiresModel? requires { get; set; }

        public bool comboBuilder { get; set; }
        public bool comboFinisher { get; set; }
        public int comboPointsGenerated { get; set; } = 1;

        /// <summary>Values for the description's {0}, {1}… placeholders.</summary>
        public float[]? descArgs { get; set; }

        public List<ImpactModel>? impacts { get; set; }
    }

    public class TargetModel
    {
        /// <summary>None, Caster, Aim or Area.</summary>
        public string? type { get; set; }
        /// <summary>Enemy, Ally or Any.</summary>
        public string? affinity { get; set; }
        public bool includeCaster { get; set; }
    }

    public class DeliveryModel
    {
        /// <summary>Direct or Projectile.</summary>
        public string? type { get; set; }
        public float velocity { get; set; } = 0.8f;
        public int count { get; set; } = 1;
        public float spreadDegrees { get; set; }
        /// <summary>spellprojectile (spinning knife) or spellorb (glowing ball).</summary>
        public string? entity { get; set; }
    }

    public class RequiresModel
    {
        public bool bow { get; set; }
        public bool shield { get; set; }
        public bool outOfCombat { get; set; }
        /// <summary>Spell id of the form this ability needs, e.g. a cat-form ability.</summary>
        public string? form { get; set; }
        /// <summary>Attribute points needed to cast, e.g. <c>{ "intelligence": 15 }</c>.</summary>
        public Dictionary<string, float>? attributes { get; set; }
    }

    public class ImpactModel
    {
        /// <summary>One of the ImpactAction names: Damage, Heal, StatusEffect, Stun, Shield, Cleanse…</summary>
        public string? action { get; set; }
        public float chance { get; set; } = 1f;
        public bool affectCaster { get; set; }

        public float coefficient { get; set; } = 1f;
        public float perComboPoint { get; set; }
        public float knockback { get; set; } = 1f;
        public float missingHealthFraction { get; set; }

        public string? effectId { get; set; }
        public float seconds { get; set; }
        public float secondsPerComboPoint { get; set; }
        public int amplifier { get; set; } = 1;
        public int amplifierCap { get; set; } = 1;
        /// <summary>Set (refresh) or Add (stack up to the cap).</summary>
        public string? applyMode { get; set; }

        public float shieldCoefficient { get; set; } = 1f;
        public float shieldSeconds { get; set; } = 10f;

        public float teleportDistance { get; set; } = 1.5f;
        /// <summary>Forward or BehindTarget.</summary>
        public string? teleportMode { get; set; }

        public float resourceGain { get; set; }
        public float aggroRange { get; set; } = 8f;
        public int cleanseMax { get; set; }

        public float zoneSeconds { get; set; } = 6f;
        public float zoneTickSeconds { get; set; } = 1f;
        public float zoneRadius { get; set; }
        public bool zoneAtAimPoint { get; set; }

        /// <summary>"school" for the spell school's default burst, or omit for none.</summary>
        public string? particles { get; set; }
    }
}
