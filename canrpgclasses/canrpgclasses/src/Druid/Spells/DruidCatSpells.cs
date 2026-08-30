using canrpgclasses.Core.Spells;

namespace canrpgclasses.Druid.Spells
{
    /// <summary>Base for a Cat-form ability. Bypasses the form-lock so a shifted druid can actually cast it; costs
    /// energy rather than mana.</summary>
    public abstract class CatAbilitySpell : Spell
    {
        protected CatAbilitySpell()
        {
            School = SpellSchool.Nature;
            Range = 4;
            Target.Type = TargetType.Aim;
            Target.Affinity = TargetAffinity.Enemy;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Instant;
            BypassesFormLock = true;
            RequiresForm = DruidEffectIds.FelineFormSpellId;
            // Shared cooldown group acting as a GCD for the cat kit: the abilities cost no mana, so without it they
            // would be castable every frame, and alternating two of them would bypass any per-spell cooldown.
            // Abilities with their own cooldown (Stalk) opt out by resetting the group to their own key.
            Cost.Cooldown.Group = "canrpgclasses:druid_cat_gcd";
        }
    }

    [SpellRegistration("canrpgclasses:flesh_tear")]
    public class FleshTearSpell : CatAbilitySpell
    {
        public FleshTearSpell()
        {
            var b = Balance;
            DisplayName = "Flesh Tear"; IconName = "spinning-blades"; Tier = 1;
            float coeff = b.F("coeff", 1.3f);
            float energy = b.F("energy", 40f);
            DescArgs = new object[] { coeff, (int)energy };
            ComboBuilder = true;
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                SparkFx = true,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });
            ConfigureSecondaryCost(DruidEnergy.PoolId, energy);
            ConfigureCost(defResource: 0f, defCooldown: 1.0f);
        }
    }

    [SpellRegistration("canrpgclasses:claw_slash")]
    public class ClawSlashSpell : CatAbilitySpell
    {
        public ClawSlashSpell()
        {
            var b = Balance;
            DisplayName = "Claw Slash"; IconName = "dripping-blade"; Tier = 1;
            float coeff = b.F("coeff", 0.8f);
            int duration = (int)b.F("duration", 9f);
            float energy = b.F("energy", 35f);
            DescArgs = new object[] { coeff, b.F("coeffPerTick", 0.25f), duration, (int)energy };
            ComboBuilder = true;
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = DruidEffectIds.ClawSlash,
                StatusEffectDuration = duration
            });
            ConfigureSecondaryCost(DruidEnergy.PoolId, energy);
            ConfigureCost(defResource: 0f, defCooldown: 1.0f);
        }
    }

    [SpellRegistration("canrpgclasses:deep_rend")]
    public class DeepRendSpell : CatAbilitySpell
    {
        public DeepRendSpell()
        {
            var b = Balance;
            DisplayName = "Deep Rend"; IconName = "neck-bite"; Tier = 2;
            int baseDur = (int)b.F("baseDuration", 4f);
            float perCombo = b.F("durationPerCombo", 2f);
            float energy = b.F("energy", 30f);
            DescArgs = new object[] { b.F("coeffPerTick", 0.3f), baseDur, (int)perCombo, (int)energy };
            ComboFinisher = true;
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.StatusEffect,
                StatusEffectId = DruidEffectIds.DeepRend,
                StatusEffectDuration = baseDur,
                DurationPerComboPoint = perCombo,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature) // application burst (no damage impact to carry it)
            });
            ConfigureSecondaryCost(DruidEnergy.PoolId, energy);
            ConfigureCost(defResource: 0f, defCooldown: 1.0f);
        }
    }

    [SpellRegistration("canrpgclasses:savage_bite")]
    public class SavageBiteSpell : CatAbilitySpell
    {
        public SavageBiteSpell()
        {
            var b = Balance;
            DisplayName = "Savage Bite"; IconName = "neck-bite"; Tier = 2;
            float coeff = b.F("coeff", 0.5f);
            float perCombo = b.F("coeffPerCombo", 0.7f);
            float energy = b.F("energy", 35f);
            DescArgs = new object[] { coeff, perCombo, (int)energy };
            ComboFinisher = true;
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.Damage,
                DamageSpellPowerCoefficient = coeff,
                DamagePerComboPoint = perCombo,
                SparkFx = true,
                Particles = ParticleSpec.ForSchool(SpellSchool.Nature)
            });
            ConfigureSecondaryCost(DruidEnergy.PoolId, energy);
            ConfigureCost(defResource: 0f, defCooldown: 1.0f);
        }
    }

    [SpellRegistration("canrpgclasses:stalk")]
    public class StalkSpell : CatAbilitySpell
    {
        public StalkSpell()
        {
            var b = Balance;
            DisplayName = "Stalk"; IconName = "cloudy-fork"; Tier = 2;
            Target.Type = TargetType.Caster; Target.Affinity = TargetAffinity.Ally;
            float dur = b.F("stealthDuration", 12f);
            // The actual invisibility: a break-on-attack invis effect (hides the model + shows the HUD buff). Without
            // this impact the stealth window opens but nothing hides you. Same effect the rogue's Stealth applies.
            Impacts.Add(SpellImpact.Invisibility(dur));
            OpensStealthWindowSeconds = dur;   // re-stealth timer + "attack reveals you, full lockout" gate
            RequiresOutOfCombat = true;        // an opener, not a mid-fight escape (like the rogue's Stealth)
            DescArgs = new object[] { (int)dur };
            Cost.Cooldown.Group = ""; // own cooldown (not the shared cat GCD), so Stalk's 8s doesn't lock the rotation
            ConfigureCost(defResource: 0f, defCooldown: b.F("cooldown", 8f)); // shorter than the stealth so re-prowling is seamless
        }
    }
}
