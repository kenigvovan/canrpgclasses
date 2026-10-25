using canrpgclasses.Core.Spells;

namespace canrpgclasses.Druid.Spells
{
    [SpellRegistration("canrpgclasses:entangling_roots")]
    public class EntanglingRootsSpell : Spell
    {
        public EntanglingRootsSpell()
        {
            var b = Balance;
            DisplayName = "Entangling Roots";
            IconName = "evil-tree";
            School = SpellSchool.Nature;
            Tier = 1;
            Range = b.F("range", 24f);

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;

            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 1.5f);

            int duration = (int)b.F("duration", 6f);
            DescArgs = new object[] { duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Root,
                StatusEffectDuration = duration,
                ControlFxEntity = "canrpgclasses:druidroots",
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });

            ConfigureCost(defResource: 25f, defCooldown: 20f); // mana
        }
    }
}
