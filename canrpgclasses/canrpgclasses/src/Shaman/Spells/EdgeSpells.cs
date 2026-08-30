using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    /// <summary>The three weapon imbues: long self-buffs, one at a time, since casting one strips the others.</summary>
    public abstract class EdgeSpellBase : Spell
    {
        protected EdgeSpellBase(string effectId, SpellSchool school, string icon, string display)
        {
            DisplayName = display;
            IconName = icon;
            School = school;
            Tier = 2;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                AffectCaster = true,
                StatusEffectId = effectId,
                StatusEffectDuration = Balance.F("duration", 600f),
                Particles = ParticleSpec.ForSchool(school)
            });

            Cost.Cooldown.Group = "canrpgclasses:shaman_imbue"; // shared, to stop flickering
            ConfigureCost(defResource: 20f, defCooldown: 3f);   // mana
        }
    }

    [SpellRegistration("canrpgclasses:flame_edge")]
    public class FlameEdgeSpell : EdgeSpellBase
    {
        public FlameEdgeSpell() : base(ShamanImbueIds.FlameEdge, SpellSchool.Fire, "match-head", "Flame Edge")
            => DescArgs = new object[] { Balance.F("coeff", 0.35f) };
    }

    [SpellRegistration("canrpgclasses:frost_edge")]
    public class FrostEdgeSpell : EdgeSpellBase
    {
        public FrostEdgeSpell() : base(ShamanImbueIds.FrostEdge, SpellSchool.Frost, "icicles-aura", "Frost Edge")
            => DescArgs = new object[] { Balance.F("coeff", 0.20f), (int)Balance.F("slowDuration", 2f) };
    }

    [SpellRegistration("canrpgclasses:gale_edge")]
    public class GaleEdgeSpell : EdgeSpellBase
    {
        public GaleEdgeSpell() : base(ShamanImbueIds.GaleEdge, SpellSchool.Nature, "wind-hole", "Gale Edge")
            => DescArgs = new object[] { (int)System.Math.Round(Balance.F("chance", 0.20f) * 100f), Balance.F("coeff", 0.75f) };
    }
}
