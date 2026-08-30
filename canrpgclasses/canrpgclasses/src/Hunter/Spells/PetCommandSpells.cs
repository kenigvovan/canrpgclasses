using canrpgclasses.Core.Spells;

namespace canrpgclasses.Hunter.Spells
{
    // The hunter's pet-order spells: free, instant "casts" that command the wolf. Base spells (every hunter has
    // them from level 1), handled by HunterPetSystem's PetCommand impact. No-op with no wolf out.

    [SpellRegistration("canrpgclasses:pet_aggressive")]
    public class PetAggressiveSpell : Spell
    {
        public PetAggressiveSpell()
        {
            DisplayName = "Pet: Aggressive"; IconName = "enrage"; School = SpellSchool.PhysicalRanged; Tier = 0;
            Target.Type = TargetType.Caster; Deliver.Type = DeliveryType.Direct;
            Impacts.Add(new SpellImpact { Action = ImpactAction.PetCommand, PetCommand = "aggressive", AffectCaster = true });
            ConfigureCost(defResource: 0f, defCooldown: 1f);
        }
    }

    /// <summary>Won't hunt on its own - only engages what you attack or send it at.</summary>
    [SpellRegistration("canrpgclasses:pet_defensive")]
    public class PetDefensiveSpell : Spell
    {
        public PetDefensiveSpell()
        {
            DisplayName = "Pet: Defensive"; IconName = "cross-shield"; School = SpellSchool.PhysicalRanged; Tier = 0;
            Target.Type = TargetType.Caster; Deliver.Type = DeliveryType.Direct;
            Impacts.Add(new SpellImpact { Action = ImpactAction.PetCommand, PetCommand = "defensive", AffectCaster = true });
            ConfigureCost(defResource: 0f, defCooldown: 1f);
        }
    }

    /// <summary>Never attacks on its own; only an explicit Attack order engages it.</summary>
    [SpellRegistration("canrpgclasses:pet_passive")]
    public class PetPassiveSpell : Spell
    {
        public PetPassiveSpell()
        {
            DisplayName = "Pet: Passive"; IconName = "body-balance"; School = SpellSchool.PhysicalRanged; Tier = 0;
            Target.Type = TargetType.Caster; Deliver.Type = DeliveryType.Direct;
            Impacts.Add(new SpellImpact { Action = ImpactAction.PetCommand, PetCommand = "passive", AffectCaster = true });
            ConfigureCost(defResource: 0f, defCooldown: 1f);
        }
    }

    /// <summary>Takes a passive pet off passive so it obeys.</summary>
    [SpellRegistration("canrpgclasses:pet_attack")]
    public class PetAttackSpell : Spell
    {
        public PetAttackSpell()
        {
            DisplayName = "Pet: Attack"; IconName = "lion"; School = SpellSchool.PhysicalRanged; Tier = 0;
            Range = 20f;
            Target.Type = TargetType.Aim; Target.Affinity = TargetAffinity.Enemy; Deliver.Type = DeliveryType.Direct;
            Impacts.Add(new SpellImpact { Action = ImpactAction.PetCommand, PetCommand = "attack" });
            ConfigureCost(defResource: 0f, defCooldown: 1f);
        }
    }

    [SpellRegistration("canrpgclasses:pet_come")]
    public class PetComeSpell : Spell
    {
        public PetComeSpell()
        {
            DisplayName = "Pet: Come"; IconName = "griffin-symbol"; School = SpellSchool.PhysicalRanged; Tier = 0;
            Target.Type = TargetType.Caster; Deliver.Type = DeliveryType.Direct;
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.PetCommand, PetCommand = "come", AffectCaster = true,
                Particles = new ParticleSpec { ColorA = 200, ColorR = 200, ColorG = 200, ColorB = 255, VelocityY = 0.3f }
            });
            ConfigureCost(defResource: 0f, defCooldown: 1f);
        }
    }

    /// <summary>Toggles between holding ground and following.</summary>
    [SpellRegistration("canrpgclasses:pet_stay")]
    public class PetStaySpell : Spell
    {
        public PetStaySpell()
        {
            DisplayName = "Pet: Stay"; IconName = "iron-cross"; School = SpellSchool.PhysicalRanged; Tier = 0;
            Target.Type = TargetType.Caster; Deliver.Type = DeliveryType.Direct;
            Impacts.Add(new SpellImpact { Action = ImpactAction.PetCommand, PetCommand = "stay", AffectCaster = true });
            ConfigureCost(defResource: 0f, defCooldown: 1f);
        }
    }

    /// <summary>Despawns with no summon lockout, so you can resummon at will.</summary>
    [SpellRegistration("canrpgclasses:pet_dismiss")]
    public class PetDismissSpell : Spell
    {
        public PetDismissSpell()
        {
            DisplayName = "Pet: Dismiss"; IconName = "cloud-ring"; School = SpellSchool.PhysicalRanged; Tier = 0;
            Target.Type = TargetType.Caster; Deliver.Type = DeliveryType.Direct;
            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.PetCommand, PetCommand = "dismiss", AffectCaster = true,
                Particles = new ParticleSpec { ColorA = 160, ColorR = 120, ColorG = 120, ColorB = 140, VelocityY = 0.2f }
            });
            ConfigureCost(defResource: 0f, defCooldown: 1f);
        }
    }
}
