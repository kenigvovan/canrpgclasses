using canrpgclasses.Core.Spells;

namespace canrpgclasses.Core.Content
{
    /// <summary>A spell defined in JSON rather than C#. Everything a <see cref="Spell"/> holds is already data,
    /// so this only copies a validated <see cref="SpellModel"/> across; the executor can't tell the difference.
    ///
    /// The balance numbers come from the content file itself rather than <c>BalanceConfig</c>, so a JSON spell
    /// is self-contained - the trade-off is that the in-game balance editor can't retune it live.</summary>
    public class ContentSpell : Spell
    {
        public ContentSpell(string id, SpellModel m, ContentReport report)
        {
            Id = id;
            DisplayName = m.name ?? LangText.Humanize(id);
            if (!string.IsNullOrEmpty(m.description)) Description = m.description!;
            IconName = m.icon;

            School = report.Enum(m.school, SpellSchool.PhysicalMelee, id, "school");
            Tier = m.tier;
            Range = m.range;

            CastMode = report.Enum(m.castMode, CastMode.Instant, id, "castMode");
            CastDuration = m.castSeconds;
            ChannelTicks = m.channelTicks;
            TriggersGlobalCooldown = m.triggersGlobalCooldown;

            if (m.target != null)
            {
                Target.Type = report.Enum(m.target.type, TargetType.Caster, id, "target.type");
                Target.Affinity = report.Enum(m.target.affinity, TargetAffinity.Enemy, id, "target.affinity");
                Target.AreaIncludeCaster = m.target.includeCaster;
            }

            if (m.delivery != null)
            {
                Deliver.Type = report.Enum(m.delivery.type, DeliveryType.Direct, id, "delivery.type");
                Deliver.ProjectileVelocity = m.delivery.velocity;
                Deliver.ProjectileCount = m.delivery.count;
                Deliver.ProjectileSpreadDegrees = m.delivery.spreadDegrees;
                if (!string.IsNullOrEmpty(m.delivery.entity)) Deliver.ProjectileEntity = m.delivery.entity!;
            }

            if (m.requires != null)
            {
                RequiresBow = m.requires.bow;
                RequiresShield = m.requires.shield;
                RequiresOutOfCombat = m.requires.outOfCombat;
                RequiresForm = m.requires.form;
                if (m.requires.attributes is { Count: > 0 }) RequiresAttributes = m.requires.attributes;
                // An in-form ability has to be castable while the form locks everything else.
                if (!string.IsNullOrEmpty(m.requires.form)) BypassesFormLock = true;
            }

            ComboBuilder = m.comboBuilder;
            ComboFinisher = m.comboFinisher;
            ComboPointsGenerated = m.comboPointsGenerated;

            if (m.descArgs is { Length: > 0 })
            {
                var args = new object[m.descArgs.Length];
                for (int i = 0; i < args.Length; i++) args[i] = m.descArgs[i];
                DescArgs = args;
            }

            if (m.impacts != null)
                foreach (var im in m.impacts) Impacts.Add(BuildImpact(im, id, report));

            Cost.Resource = m.resourceCost;
            Cost.Cooldown.Duration = m.cooldown;
            Cost.Cooldown.Group = m.cooldownGroup;
        }

        private SpellImpact BuildImpact(ImpactModel m, string spellId, ContentReport report)
        {
            var imp = new SpellImpact
            {
                Action = report.Enum(m.action, ImpactAction.Damage, spellId, "impact.action"),
                Chance = m.chance,
                AffectCaster = m.affectCaster,

                DamageSpellPowerCoefficient = m.coefficient,
                HealSpellPowerCoefficient = m.coefficient,
                DamagePerComboPoint = m.perComboPoint,
                HealPerComboPoint = m.perComboPoint,
                Knockback = m.knockback,
                HealMissingHealthFraction = m.missingHealthFraction,
                DamageMissingHealthCoefficient = m.missingHealthFraction,

                StatusEffectId = m.effectId,
                StatusEffectDuration = m.seconds,
                DurationPerComboPoint = m.secondsPerComboPoint,
                StatusEffectAmplifier = m.amplifier,
                StatusEffectAmplifierCap = m.amplifierCap,
                StatusEffectApplyMode = report.Enum(m.applyMode, StatusApplyMode.Set, spellId, "impact.applyMode"),

                ShieldSpellPowerCoefficient = m.shieldCoefficient,
                ShieldDurationSeconds = m.shieldSeconds,

                TeleportDistance = m.teleportDistance,
                TeleportMode = report.Enum(m.teleportMode, TeleportMode.Forward, spellId, "impact.teleportMode"),

                ResourceGainAmount = m.resourceGain,
                AggroRange = m.aggroRange,
                CleanseMax = m.cleanseMax,

                ZoneDurationSeconds = m.zoneSeconds,
                ZoneTickSeconds = m.zoneTickSeconds,
                ZoneRadius = m.zoneRadius,
                ZoneAtAimPoint = m.zoneAtAimPoint
            };

            if (string.Equals(m.particles, "school", System.StringComparison.OrdinalIgnoreCase))
                imp.Particles = ParticleSpec.ForSchool(School);

            return imp;
        }
    }
}
