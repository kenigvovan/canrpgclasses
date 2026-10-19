using System.Text;
using canrpgclasses.Core.Portals;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace canrpgclasses
{
    /// <summary>/canrpgportal: admin points the Waypoint portal can lead to.</summary>
    public partial class canrpgclassesModSystem
    {
        private void RegisterPortalCommands(ICoreServerAPI api)
        {
            var p = api.ChatCommands.Parsers;
            api.ChatCommands.Create("canrpgportal")
                .WithDescription("Admin: Waypoint portal points")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("add")
                    .WithDescription("Add or move a point to where you stand; you exit it facing your current direction")
                    .RequiresPlayer()
                    .WithArgs(p.All("name"))
                    .HandleWith(OnPortalAdd)
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithDescription("Remove a point")
                    .WithArgs(p.All("name"))
                    .HandleWith(args => PortalAnchors.Remove(((string)args[0]).Trim())
                        ? TextCommandResult.Success("Removed.")
                        : TextCommandResult.Error("No such point."))
                .EndSubCommand()
                .BeginSubCommand("list")
                    .WithDescription("List the points")
                    .HandleWith(OnPortalList)
                .EndSubCommand();
        }

        private TextCommandResult OnPortalAdd(TextCommandCallingArgs args)
        {
            var e = args.Caller.Player.Entity;
            string name = ((string)args[0]).Trim();
            if (name.Length == 0 || name.Length > 32) return TextCommandResult.Error("Name must be 1-32 characters.");

            var v = e.Pos.GetViewVector();
            PortalAnchors.Set(new PortalAnchor
            {
                Name = name, Dim = e.Pos.Dimension, X = e.Pos.X, Y = e.Pos.Y, Z = e.Pos.Z,
                Yaw = PortalGeometry.SnapYaw((float)System.Math.Atan2(v.X, v.Z))
            });
            // The portal shows what the admin is looking at right now.
            if (args.Caller.Player is IServerPlayer sp) PortalShots.Request(sp, PortalShots.AdminPrefix + name);
            return TextCommandResult.Success($"Point '{name}' set at {(int)e.Pos.X}, {(int)e.Pos.Y}, {(int)e.Pos.Z}. Snapshot taken from your current view.");
        }

        private TextCommandResult OnPortalList(TextCommandCallingArgs args)
        {
            if (PortalAnchors.All.Count == 0) return TextCommandResult.Success("No points. Mode: " + PortalAnchors.Mode);
            var sb = new StringBuilder("Mode: ").Append(PortalAnchors.Mode);
            foreach (var a in PortalAnchors.All)
                sb.Append('\n').Append(a.Name).Append(": ").Append((int)a.X).Append(", ").Append((int)a.Y).Append(", ").Append((int)a.Z)
                  .Append(a.Dim != 0 ? " (dim " + a.Dim + ")" : "");
            return TextCommandResult.Success(sb.ToString());
        }
    }
}
