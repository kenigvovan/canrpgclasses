using canrpgclasses.Core.Portals;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Mage.Spells
{
    /// <summary>Gate: a short-lived portal from in front of the mage to the aimed spot.</summary>
    [SpellRegistration("canrpgclasses:mage_gate")]
    public class MageGateSpell : Spell
    {
        public MageGateSpell()
        {
            var b = Balance;
            DisplayName = "Gate";
            IconName = "magic-portal";
            School = SpellSchool.Arcane;
            Range = b.F("range", 30f);

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            float duration = b.F("duration", 15f);
            DescArgs = new object[] { (int)Range, (int)duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.OpenPortal,
                PortalKind = PortalKind.Gate,
                StatusEffectDuration = duration,
                PortalMaxHeightDiff = b.F("maxHeightDiff", 6f)
            });

            ConfigureCost(defResource: 30f, defCooldown: 45f);
        }
    }

    /// <summary>Anchor: marks the spot the Waypoint portal leads back to. Off unless the server enables waypoints.</summary>
    [SpellRegistration("canrpgclasses:portal_anchor")]
    public class PortalAnchorSpell : Spell
    {
        public PortalAnchorSpell()
        {
            DisplayName = "Portal Anchor";
            IconName = "position-marker";
            School = SpellSchool.Arcane;
            // Not needed when only admin points are allowed.
            Enabled = MageWaypointSpell.WaypointsEnabled && PortalAnchors.Mode != AnchorMode.Admin;
            TriggersGlobalCooldown = false;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.SetPortalAnchor,
                Particles = ParticleSpec.ForSchool(SpellSchool.Arcane)
            });

            ConfigureCost(defResource: 0f, defCooldown: 60f);
        }
    }

    /// <summary>Waypoint: a long cast that opens a portal to the own anchor or an admin point (waypoint.anchorMode).
    /// Off by default (waypoint.enabled).</summary>
    [SpellRegistration("canrpgclasses:waypoint")]
    public class MageWaypointSpell : Spell
    {
        public static bool WaypointsEnabled => canrpgclasses.Core.Config.BalanceConfig.Spell("waypoint").F("enabled", 0f) > 0.5f;

        public MageWaypointSpell()
        {
            var b = Balance;
            DisplayName = "Waypoint";
            IconName = "magic-gate";
            School = SpellSchool.Arcane;
            Enabled = WaypointsEnabled;
            PicksPortalDestination = true;

            Target.Type = TargetType.Caster;
            Deliver.Type = DeliveryType.Direct;
            CastMode = CastMode.Charge;
            CastDuration = b.F("cast", 10f);

            float duration = b.F("duration", 30f);
            DescArgs = new object[] { (int)CastDuration, (int)duration };

            Impacts.Add(new SpellImpact
            {
                Action = ImpactAction.OpenPortal,
                PortalKind = PortalKind.Waypoint,
                StatusEffectDuration = duration,
                PortalMaxDistance = b.F("maxDistance", 0f)
            });

            ConfigureCost(defResource: 60f, defCooldown: 1800f);
        }
    }
}
