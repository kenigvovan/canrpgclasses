using canrpgclasses.Core.Spells;

namespace canrpgclasses.Shaman.Spells
{
    [SpellRegistration("canrpgclasses:mending_totem")]
    public class MendingTotemSpell : Spell
    {
        public MendingTotemSpell()
        {
            var b = Balance;
            DisplayName = "Mending Totem";
            IconName = "drop";
            School = SpellSchool.Nature;
            Tier = 3;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;

            float duration = b.F("duration", 30f);
            float radius = b.F("radius", 8f);
            DescArgs = new object[]
            {
                canrpgclasses.Core.Config.BalanceConfig.Global("totemStreamCoeff", 0.3f), radius, (int)duration
            };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.PlaceTotem,
                TotemKind = ShamanTotemSystem.KindStream,
                StatusEffectDuration = duration,
                ZoneRadius = radius,
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 40f, defCooldown: 20f); // mana
        }
    }

    [SpellRegistration("canrpgclasses:stone_ward")]
    public class StoneWardSpell : Spell
    {
        public StoneWardSpell()
        {
            var b = Balance;
            DisplayName = "Stone Ward";
            IconName = "surrounded-shield";
            School = SpellSchool.Nature;
            Tier = 4;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally; // the ally under the crosshair, else yourself
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;

            int charges = (int)b.F("charges", 4f);
            DescArgs = new object[] { charges, b.F("coeff", 0.3f) };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = ShamanRestoIds.StoneWard,
                StatusEffectDuration = b.F("duration", 600f),
                StatusEffectAmplifier = charges, // the tier is the remaining charges
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 35f, defCooldown: 15f); // mana
        }
    }

    [SpellRegistration("canrpgclasses:spirit_purge")]
    public class SpiritPurgeSpell : Spell
    {
        public SpiritPurgeSpell()
        {
            DisplayName = "Spirit Purge";
            IconName = "swirl-string";
            School = SpellSchool.Nature;
            Tier = 3;
            Range = 20;

            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Ally;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Cleanse,
                CleanseMax = 0, // 0 = everything
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 25f, defCooldown: 8f); // mana
        }
    }

    [SpellRegistration("canrpgclasses:rising_tide")]
    public class RisingTideSpell : Spell
    {
        public RisingTideSpell()
        {
            var b = Balance;
            DisplayName = "Rising Tide";
            IconName = "wave-crest";
            School = SpellSchool.Nature;
            Tier = 5;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;

            int duration = (int)b.F("duration", 15f);
            DescArgs = new object[] { (int)System.Math.Round(b.F("healingPower", 0.30f) * 100f), duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                AffectCaster = true,
                StatusEffectId = ShamanRestoIds.RisingTide,
                StatusEffectDuration = duration,
                Particles = ParticleSpec.Heal()
            });

            ConfigureCost(defResource: 0f, defCooldown: 120f);
        }
    }
}
