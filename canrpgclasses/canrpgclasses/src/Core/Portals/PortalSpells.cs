using System;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Net;
using canrpgclasses.Core.Spells;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace canrpgclasses.Core.Portals
{
    /// <summary>The OpenPortal / SetPortalAnchor impacts. Placement is validated before the cast is committed
    /// (precheck) and again when it lands, so a refused portal costs nothing.</summary>
    public static class PortalSpells
    {
        public const string AnchorAttr = "canrpgPortalAnchor";
        private const int DefaultTintA = 0x5A2DC8, DefaultTintB = 0xC89BFF;
        private const double MinGateDistance = 4;
        private const double InFront = 1.5;

        private static bool registered;

        public static void Init()
        {
            if (registered) return;
            registered = true;
            SpellExecutor.RegisterImpact(ImpactAction.OpenPortal, ApplyOpen);
            SpellExecutor.RegisterPrecheck(ImpactAction.OpenPortal, (c, s, imp, aim) => Plan(c, s, imp, aim, out _));
            SpellExecutor.RegisterImpact(ImpactAction.SetPortalAnchor, ApplySetAnchor);
            SpellExecutor.RegisterPrecheck(ImpactAction.SetPortalAnchor,
                (c, s, imp, aim) => CanUse(c, c.Pos.XYZ) ? null : Msg("portal-claimed"));
        }

        /// <summary>ShotA: the destination snapshot shown in end A, if any.</summary>
        private readonly record struct Ends(Vec3d A, float YawA, Vec3d B, float YawB, byte[]? ShotA = null);

        private static void ApplyOpen(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var caster = ctx.Caster;
            if (Plan(caster, ctx.Spell, imp, ctx.Aim ?? AimContext.None, out var ends) is string refusal)
            {
                Tell(caster, refusal);
                return;
            }
            float seconds = imp.StatusEffectDuration > 0 ? imp.StatusEffectDuration : 15f;
            PortalManager.Open(caster, imp.PortalKind, ends.A, ends.YawA, ends.B, ends.YawB, seconds,
                imp.PortalTintA != 0 ? imp.PortalTintA : DefaultTintA,
                imp.PortalTintB != 0 ? imp.PortalTintB : DefaultTintB, ends.ShotA);
        }

        private static void ApplySetAnchor(SpellContext ctx, Entity target, SpellImpact imp)
        {
            var caster = ctx.Caster;
            var pos = caster.Pos;
            var tree = new TreeAttribute();
            tree.SetInt("dim", pos.Dimension);
            tree.SetDouble("x", pos.X);
            tree.SetDouble("y", pos.Y);
            tree.SetDouble("z", pos.Z);
            tree.SetFloat("yaw", PortalGeometry.SnapYaw(ViewYaw(caster)));
            caster.WatchedAttributes[AnchorAttr] = tree;
            caster.WatchedAttributes.MarkPathDirty(AnchorAttr);
            // The old snapshot shows the old place; take a new one from the current view.
            caster.Attributes.RemoveAttribute(PortalShots.PersonalShotAttr);
            if ((caster as EntityPlayer)?.Player is IServerPlayer sp) PortalShots.Request(sp, PortalShots.PersonalKey);
            imp.Particles?.Emit(caster.World, caster);
            Tell(caster, Msg("portal-anchor-set"));
        }

        /// <summary>Where both ends go, or a localized refusal.</summary>
        private static string? Plan(EntityAgent caster, Spell spell, SpellImpact imp, AimContext aim, out Ends ends)
        {
            ends = default;
            float yaw = PortalGeometry.SnapYaw(ViewYaw(caster));
            var fwd = PortalGeometry.Forward(yaw);
            var back = new Vec3d(-fwd.X, 0, -fwd.Z);
            float backYaw = yaw + GameMath.PI;

            var a = PortalManager.FindStandSpot(caster, caster.Pos.XYZ.AddCopy(fwd.X * InFront, 0, fwd.Z * InFront), back);
            if (a == null) return Msg("portal-no-room");

            if (imp.PortalKind == PortalKind.Waypoint)
            {
                if (ResolveAnchor(caster, aim.Choice, out var anchor) is string noAnchor) return noAnchor;
                if (anchor.Dim != caster.Pos.Dimension) return Msg("portal-other-dim");
                var at = new Vec3d(anchor.X, anchor.Y, anchor.Z);
                double d2 = at.HorizontalSquareDistanceTo(a);
                if (d2 < MinGateDistance * MinGateDistance) return Msg("portal-too-close");
                if (imp.PortalMaxDistance > 0 && d2 > imp.PortalMaxDistance * imp.PortalMaxDistance) return Msg("portal-too-far");
                // Admin points are trusted; a personal anchor is re-checked, the claim may be newer than it.
                if (anchor.Personal && !CanUse(caster, at)) return Msg("portal-claimed");
                var shot = anchor.Personal ? caster.Attributes.GetBytes(PortalShots.PersonalShotAttr) : anchor.Shot;
                ends = new Ends(a, backYaw, at, anchor.Yaw, shot);
                return null;
            }

            float range = spell.Range > 0 ? spell.Range : 30f;
            var hit = ZoneManager.AimGroundPoint(caster, range, aim);
            var b = PortalManager.FindStandSpot(caster, hit, back);
            if (b == null) return Msg("portal-no-room");
            if (b.HorizontalSquareDistanceTo(a) < MinGateDistance * MinGateDistance) return Msg("portal-too-close");
            if (imp.PortalMaxHeightDiff > 0 && Math.Abs(b.Y - a.Y) > imp.PortalMaxHeightDiff) return Msg("portal-too-high");
            if (!InSight(caster, b)) return Msg("portal-no-sight");
            if (!CanUse(caster, b)) return Msg("portal-claimed");

            ends = new Ends(a, backYaw, b, yaw);
            return null;
        }

        /// <summary>The Waypoint destination per <see cref="PortalAnchors.Mode"/>: empty choice = own anchor.</summary>
        private static string? ResolveAnchor(EntityAgent caster, string? choice, out PortalAnchor anchor)
        {
            anchor = null!;
            var mode = PortalAnchors.Mode;
            bool personal = mode == AnchorMode.Personal || (mode == AnchorMode.Both && string.IsNullOrEmpty(choice));

            if (personal)
            {
                if (caster.WatchedAttributes.GetTreeAttribute(AnchorAttr) is not ITreeAttribute t) return Msg("portal-no-anchor");
                anchor = new PortalAnchor
                {
                    Dim = t.GetInt("dim"), X = t.GetDouble("x"), Y = t.GetDouble("y"), Z = t.GetDouble("z"),
                    Yaw = t.GetFloat("yaw"), Personal = true
                };
                return null;
            }

            if (string.IsNullOrEmpty(choice)) return Msg("portal-pick");
            if (PortalAnchors.Find(choice!) is not PortalAnchor found) return Msg("portal-unknown-point");
            anchor = found;
            return null;
        }

        internal static float ViewYaw(EntityAgent caster)
        {
            var v = caster.Pos.GetViewVector();
            return (float)Math.Atan2(v.X, v.Z);
        }

        // Only solid blocks block the view: grass and flowers don't.
        private static bool InSight(EntityAgent caster, Vec3d foot)
        {
            var eye = caster.Pos.XYZ.Add(caster.LocalEyePos);
            var to = foot.AddCopy(0, 1.2, 0);
            BlockSelection bsel = null!;
            EntitySelection esel = null!;
            caster.World.RayTraceForSelection(eye, to, ref bsel, ref esel,
                (_, block) => block.CollisionBoxes != null && block.CollisionBoxes.Length > 0, _ => false);
            return bsel == null || bsel.FullPosition.DistanceTo(eye) >= eye.DistanceTo(to) - 0.3;
        }

        // A portal must not lead into a claim the caster may not use.
        private static bool CanUse(EntityAgent caster, Vec3d at)
        {
            if ((caster as EntityPlayer)?.Player is not IPlayer player) return true;
            var pos = at.AsBlockPos;
            pos.dimension = caster.Pos.Dimension;
            return caster.World.Claims.TestAccess(player, pos, EnumBlockAccessFlags.Use) == EnumWorldAccessResponse.Granted;
        }

        internal static string Msg(string key, params object[] args) => Lang.Get("canrpgclasses:msg-" + key, args);

        internal static void Tell(EntityAgent caster, string message)
        {
            if ((caster as EntityPlayer)?.Player is IServerPlayer sp)
                canrpgclassesModSystem.For(caster.Api)?.ServerChannel?.SendPacket(new SpellMessagePacket { Message = message }, sp);
        }
    }
}
