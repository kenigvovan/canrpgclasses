using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    [SpellRegistration("canrpgclasses:war_chant")]
    public class WarChantSpell : Spell
    {
        public WarChantSpell()
        {
            var b = Balance;
            DisplayName = "War Chant";
            IconName = "enrage";
            School = SpellSchool.Nature;
            Tier = 5;
            Range = b.F("radius", 10f);

            Target.Type = TargetType.Area;
            Target.Affinity = TargetAffinity.Ally;
            Target.AreaIncludeCaster = true;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;

            int duration = (int)b.F("duration", 20f);
            DescArgs = new object[]
            {
                (int)System.Math.Round(b.F("meleeDamage", 0.15f) * 100f),
                (int)System.Math.Round(b.F("walkSpeed", 0.15f) * 100f),
                duration
            };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = ShamanEffectIds.WarChant,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });

            ConfigureCost(defResource: 40f, defCooldown: 300f); // mana
        }
    }
}
