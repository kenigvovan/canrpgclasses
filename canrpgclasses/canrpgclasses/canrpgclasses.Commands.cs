using System.Text;
using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Progression;
using canrpgclasses.Core.Talents;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace canrpgclasses
{
    /// <summary>Server chat commands (all controlserver-gated debug/admin utilities).</summary>
    public partial class canrpgclassesModSystem
    {
        private void RegisterCommands(ICoreServerAPI api)
        {
            api.ChatCommands.Create("canrpgspells")
                .WithDescription("List registered RpgClasses spells")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnListSpells);

            api.ChatCommands.Create("canrpgreload")
                .WithDescription("Re-read the JSON content files and rebuild the registries, reporting any problems")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnReloadContent);

            api.ChatCommands.Create("canrpgcast")
                .WithDescription("Cast a spell by id (debug)")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .WithArgs(api.ChatCommands.Parsers.Word("spellId"))
                .HandleWith(OnCastCommand);

            api.ChatCommands.Create("canrpgxp")
                .WithDescription("Add XP to yourself (debug)")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .WithArgs(api.ChatCommands.Parsers.Int("amount"))
                .HandleWith(OnXpCommand);

            api.ChatCommands.Create("canrpglevel")
                .WithDescription("Set your level (debug)")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .WithArgs(api.ChatCommands.Parsers.Int("level"))
                .HandleWith(OnLevelCommand);

            api.ChatCommands.Create("canrpgtalent")
                .WithDescription("Spend a point on a talent by id (debug)")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .WithArgs(api.ChatCommands.Parsers.Word("talentId"))
                .HandleWith(OnTalentCommand);

            api.ChatCommands.Create("canrpgrespec")
                .WithDescription("Refund all talent points (debug)")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .HandleWith(OnRespecCommand);

            api.ChatCommands.Create("canrpgclass")
                .WithDescription("Admin: set a class by id (respecs talents). Optional: target an online player.")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .WithArgs(api.ChatCommands.Parsers.Word("classId"), api.ChatCommands.Parsers.OptionalWord("player"))
                .HandleWith(OnClassCommand);

            api.ChatCommands.Create("canrpgclassfree")
                .WithDescription("Admin: grant a free class re-pick (ignores the policy once). Optional: target an online player.")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("player"))
                .HandleWith(OnClassFreeCommand);

            // Player-usable: name your hunter pet. The name sticks on you and re-applies to every wolf you summon.
            api.ChatCommands.Create("canrpgpet")
                .WithDescription("Name your hunter pet (empty clears it)")
                .RequiresPlayer()
                .BeginSubCommand("name")
                    .WithArgs(api.ChatCommands.Parsers.All("name"))
                    .HandleWith(OnPetNameCommand)
                .EndSubCommand();

            api.ChatCommands.Create("canrpgdummy")
                .WithDescription("Spawn a training dummy (infinite HP). 'dr <percent>' sets the nearest dummy's damage ignore live; 'remove' deletes it.")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .HandleWith(OnDummySpawn)
                .BeginSubCommand("dr")
                    .WithDescription("Set the nearest dummy's damage ignore percent (0-99)")
                    .WithArgs(api.ChatCommands.Parsers.Int("percent"))
                    .HandleWith(OnDummyIgnore)
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithDescription("Remove the nearest training dummy")
                    .HandleWith(OnDummyRemove)
                .EndSubCommand();
        }

        private TextCommandResult OnDummySpawn(TextCommandCallingArgs args)
        {
            if (args.Caller.Player?.Entity is not Entity player) return TextCommandResult.Error("No player entity.");
            return Core.Debug.TrainingDummy.Spawn(player)
                ? TextCommandResult.Success("Training dummy spawned (0% damage ignore). Use '/canrpgdummy dr <percent>' to change it.")
                : TextCommandResult.Error("Could not spawn a dummy (strawdummy entity type not found).");
        }

        private TextCommandResult OnDummyIgnore(TextCommandCallingArgs args)
        {
            if (args.Caller.Player?.Entity is not Entity player) return TextCommandResult.Error("No player entity.");
            int percent = (int)args[0];
            int applied = Core.Debug.TrainingDummy.SetNearestIgnore(player, percent);
            return applied < 0
                ? TextCommandResult.Error("No training dummy within 16 blocks.")
                : TextCommandResult.Success($"Nearest dummy now ignores {applied}% of incoming damage.");
        }

        private TextCommandResult OnDummyRemove(TextCommandCallingArgs args)
        {
            if (args.Caller.Player?.Entity is not Entity player) return TextCommandResult.Error("No player entity.");
            return Core.Debug.TrainingDummy.RemoveNearest(player)
                ? TextCommandResult.Success("Training dummy removed.")
                : TextCommandResult.Error("No training dummy within 16 blocks.");
        }

        private TextCommandResult OnPetNameCommand(TextCommandCallingArgs args)
        {
            if (args.Caller.Player?.Entity is not EntityAgent owner) return TextCommandResult.Error("No player entity.");
            string name = (args[0] as string ?? "").Trim();
            Hunter.HunterPetSystem.SetPetName(owner, name);
            return TextCommandResult.Success(name.Length == 0 ? "Pet name cleared." : $"Pet named \"{name}\".");
        }

        // Resolves the command target: the named online player if given, otherwise the caller. Returns null if a name
        // was given but no such player is online.
        private Vintagestory.API.Common.Entities.Entity? ResolveTargetEntity(TextCommandCallingArgs args, int nameArgIndex, out string who)
        {
            string? name = args[nameArgIndex] as string;
            if (!string.IsNullOrEmpty(name) && ServerApi != null)
            {
                foreach (var p in ServerApi.World.AllOnlinePlayers)
                    if (p.PlayerName.Equals(name, System.StringComparison.OrdinalIgnoreCase)) { who = p.PlayerName; return p.Entity; }
                who = name;
                return null;
            }
            who = (args.Caller.Player as IServerPlayer)?.PlayerName ?? "you";
            return args.Caller.Player?.Entity;
        }

        private TextCommandResult OnTalentCommand(TextCommandCallingArgs args)
        {
            if (args.Caller.Player?.Entity is not Entity entity) return TextCommandResult.Error("No player entity.");
            var talents = entity.GetBehavior<EBTalents>();
            if (talents == null) return TextCommandResult.Error("No talents behavior on player.");
            string id = args[0] as string ?? "";
            return talents.TrySpend(id)
                ? TextCommandResult.Success($"Spent a point on {id} (rank {TalentState.Rank(entity, id)}).")
                : TextCommandResult.Error($"Cannot spend on {id} (locked / no points / maxed / unknown).");
        }

        private TextCommandResult OnRespecCommand(TextCommandCallingArgs args)
        {
            if (args.Caller.Player?.Entity is not Entity entity) return TextCommandResult.Error("No player entity.");
            entity.GetBehavior<EBTalents>()?.Respec();
            return TextCommandResult.Success("Talents reset.");
        }

        private TextCommandResult OnClassCommand(TextCommandCallingArgs args)
        {
            string id = args[0] as string ?? "";
            if (Classes.Get(id) == null) return TextCommandResult.Error($"Unknown class '{id}'.");
            var target = ResolveTargetEntity(args, 1, out string who);
            if (target == null) return TextCommandResult.Error($"Player '{who}' is not online.");
            ClassChange.Commit(target, id); // admin bypasses the re-pick policy
            return TextCommandResult.Success($"Class of {who} set to {id} (talents reset).");
        }

        private TextCommandResult OnClassFreeCommand(TextCommandCallingArgs args)
        {
            var target = ResolveTargetEntity(args, 0, out string who);
            if (target == null) return TextCommandResult.Error($"Player '{who}' is not online.");
            ClassChange.GrantFreeRepick(target);
            return TextCommandResult.Success($"Granted {who} a free class re-pick.");
        }

        private TextCommandResult OnXpCommand(TextCommandCallingArgs args)
        {
            if (args.Caller.Player?.Entity is not Entity entity) return TextCommandResult.Error("No player entity.");
            var prog = entity.GetBehavior<EBProgression>();
            if (prog == null) return TextCommandResult.Error("No progression behavior on player.");
            prog.AddXp((int)args[0]);
            return TextCommandResult.Success($"Level {prog.Level}, XP {prog.Xp}, talent points {prog.TotalTalentPoints}.");
        }

        private TextCommandResult OnLevelCommand(TextCommandCallingArgs args)
        {
            if (args.Caller.Player?.Entity is not Entity entity) return TextCommandResult.Error("No player entity.");
            var prog = entity.GetBehavior<EBProgression>();
            if (prog == null) return TextCommandResult.Error("No progression behavior on player.");
            prog.SetLevel((int)args[0]);
            return TextCommandResult.Success($"Level set to {prog.Level} (talent points {prog.TotalTalentPoints}).");
        }

        private TextCommandResult OnListSpells(TextCommandCallingArgs args)
        {
            if (Spells.Count == 0) return TextCommandResult.Success("No spells registered.");

            var sb = new StringBuilder();
            sb.AppendLine($"{Spells.Count} spell(s):");
            foreach (var spell in Spells.All.Values)
            {
                sb.AppendLine($"  {spell.Id}  [{spell.School}, {spell.Type}, tier {spell.Tier}]");
            }
            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>Re-reads the content files and rebuilds the registries, then hands the author their mistakes
        /// in chat. Assets themselves are only read at startup by the engine, so this picks up edits to files that
        /// were already loaded - for a brand-new file the world still has to be reloaded.</summary>
        private TextCommandResult OnReloadContent(TextCommandCallingArgs args)
        {
            RebuildRegistries(Api?.Logger);

            var report = LastContentReport;
            if (report == null || !report.HasProblems)
                return TextCommandResult.Success($"Reloaded. {Classes.Count} class(es), {Spells.Count} spell(s), {Talents.Count} talent(s).");

            var sb = new StringBuilder();
            sb.AppendLine($"Reloaded with {report.Problems.Count} problem(s):");
            int shown = 0;
            foreach (var p in report.Problems)
            {
                if (shown++ == 10) { sb.AppendLine($"  ... and {report.Problems.Count - 10} more, see the server log"); break; }
                sb.AppendLine("  " + p);
            }
            return TextCommandResult.Success(sb.ToString());
        }

        private TextCommandResult OnCastCommand(TextCommandCallingArgs args)
        {
            if (args.Caller.Player?.Entity is not Entity entity) return TextCommandResult.Error("No player entity.");
            string spellId = args[0] as string ?? "";
            bool ok = AttemptCast(entity, spellId, AimContext.None);
            return ok
                ? TextCommandResult.Success("Casting " + spellId)
                : TextCommandResult.Error("Cast failed (unknown spell or on cooldown).");
        }
    }
}
