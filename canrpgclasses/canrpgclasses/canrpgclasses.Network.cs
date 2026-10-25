using canrpgclasses.Core;
using canrpgclasses.Core.Classes;
using canrpgclasses.Core.Config;
using canrpgclasses.Core.EB;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Net;
using canrpgclasses.Core.Progression;
using canrpgclasses.Core.Resources;
using canrpgclasses.Core.Spells;
using canrpgclasses.Core.Talents;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace canrpgclasses
{
    /// <summary>Network part of the mod system: message registration and packet handlers for both sides.</summary>
    public partial class canrpgclassesModSystem
    {
        // OnSpellRequest throttle by player UID; evicted on disconnect so it doesn't grow forever.
        private readonly System.Collections.Generic.Dictionary<string, long> lastSpellRequestMs = new();

        private void OnPlayerDisconnectCleanup(IServerPlayer player)
        {
            if (player?.PlayerUID == null) return;
            lastSpellRequestMs.Remove(player.PlayerUID);
            Hunter.HunterPetSystem.OnOwnerLeave(player.Entity);
            Core.Portals.PortalManager.Forget(player.PlayerUID);
            Core.Portals.PortalShots.Forget(player.PlayerUID);
        }
        private const long SpellRequestMinIntervalMs = 50;

        // Both sides must register the same message types in the same order.
        private static void RegisterMessageTypes(INetworkChannel channel)
        {
            channel.RegisterMessageType(typeof(SpellRequestPacket));
            channel.RegisterMessageType(typeof(SpellCastSyncPacket));
            channel.RegisterMessageType(typeof(SpellCooldownPacket));
            channel.RegisterMessageType(typeof(SpellCooldownSyncPacket));
            channel.RegisterMessageType(typeof(SpellMessagePacket));
            channel.RegisterMessageType(typeof(SpellCastCancelPacket));
            channel.RegisterMessageType(typeof(SetViewYawPacket));
            channel.RegisterMessageType(typeof(DamageNumberPacket));
            channel.RegisterMessageType(typeof(TalentSpendPacket));
            channel.RegisterMessageType(typeof(TalentRespecPacket));
            channel.RegisterMessageType(typeof(TalentLoadoutPacket));
            channel.RegisterMessageType(typeof(AdminProgressionPacket));
            channel.RegisterMessageType(typeof(BalanceConfigPacket));
            channel.RegisterMessageType(typeof(BalanceEditPacket));
            channel.RegisterMessageType(typeof(PartyResourceMsg));
            channel.RegisterMessageType(typeof(SelectClassPacket));
            channel.RegisterMessageType(typeof(SelectClassResultPacket));
            channel.RegisterMessageType(typeof(ClassRestrictionsPacket));
            channel.RegisterMessageType(typeof(BindSpellPacket));
            channel.RegisterMessageType(typeof(PetNamePacket));
            channel.RegisterMessageType(typeof(SkillFxPacket));
            channel.RegisterMessageType(typeof(ContentSyncPacket));
            channel.RegisterMessageType(typeof(TalentTreeEditPacket));
            channel.RegisterMessageType(typeof(ClassEditPacket));
            channel.RegisterMessageType(typeof(SpellEditPacket));
            channel.RegisterMessageType(typeof(ContentEditResultPacket));
            channel.RegisterMessageType(typeof(PortalStatePacket));
            channel.RegisterMessageType(typeof(Core.Portals.PortalAnchorsPacket));
            channel.RegisterMessageType(typeof(Core.Portals.PortalShotRequestPacket));
            channel.RegisterMessageType(typeof(Core.Portals.PortalShotPacket));
            channel.RegisterMessageType(typeof(Core.Portals.SummonOfferPacket));
            channel.RegisterMessageType(typeof(Core.Portals.SummonAnswerPacket));
            channel.RegisterMessageType(typeof(SpellSoundPacket));
            channel.RegisterMessageType(typeof(PetStablePacket));
        }

        // ---- Server handlers ----
        private void OnPetName(IServerPlayer fromPlayer, PetNamePacket packet)
        {
            if (fromPlayer?.Entity is not EntityAgent owner) return;
            string name = (packet?.Name ?? "").Trim();
            if (name.Length > 32) name = name.Substring(0, 32);
            Hunter.HunterPetSystem.SetPetName(owner, name);
        }

        private void OnPetStable(IServerPlayer fromPlayer, PetStablePacket packet)
        {
            if (fromPlayer?.Entity is not EntityAgent owner || packet == null) return;
            Hunter.HunterPetSystem.StableAction(owner, packet.Action ?? "", packet.Slot);
        }

        private void OnSpellRequest(IServerPlayer fromPlayer, SpellRequestPacket packet)
        {
            var entity = fromPlayer?.Entity;
            if (entity == null) return;

            // Skip full cast validation for clients flooding faster than ~20 req/s.
            long now = ServerApi!.World.ElapsedMilliseconds;
            if (lastSpellRequestMs.TryGetValue(fromPlayer!.PlayerUID, out long last) && now - last < SpellRequestMinIntervalMs) return;
            lastSpellRequestMs[fromPlayer.PlayerUID] = now;

            string id = packet.SpellId ?? "";
            if (!string.IsNullOrEmpty(id) && !id.Contains(':')) id = ModId + ":" + id;
            if (!IsCastAllowed(entity, id)) return;

            AttemptCast(entity, id, BuildAim(entity, packet));
        }

        /// <summary>Gate against forged cast requests, shared with the client UI via <see cref="Core.Spells.SpellAccess"/>.
        /// Costs and cooldowns are enforced in TryCast.</summary>
        private bool IsCastAllowed(Entity entity, string fullSpellId)
            => Core.Spells.SpellAccess.CanCast(entity, this, fullSpellId);

        private static AimContext BuildAim(Entity caster, SpellRequestPacket packet)
        {
            var aim = new AimContext();
            if (packet.TargetEntityId != 0) aim.TargetEntity = caster.World.GetEntityById(packet.TargetEntityId);
            if (packet.HasAimPos) aim.AimPosition = new Vec3d(packet.AimX, packet.AimY, packet.AimZ);
            if (!string.IsNullOrEmpty(packet.Choice) && packet.Choice!.Length <= 64) aim.Choice = packet.Choice;
            return aim;
        }

        /// <summary>Static convenience used by the network handler and debug commands.</summary>
        public static bool AttemptCast(Entity caster, string spellId, AimContext aim)
        {
            var beh = caster?.GetBehavior<EBSpellCaster>();
            if (beh == null) return false;
            if (!string.IsNullOrEmpty(spellId) && !spellId.Contains(':')) spellId = ModId + ":" + spellId;
            return beh.TryCast(spellId, aim);
        }

        private void OnTalentSpend(IServerPlayer fromPlayer, TalentSpendPacket packet)
        {
            fromPlayer?.Entity?.GetBehavior<EBTalents>()?.TrySpend(packet.TalentId);
        }

        private void OnTalentRespec(IServerPlayer fromPlayer, TalentRespecPacket packet)
        {
            fromPlayer?.Entity?.GetBehavior<EBTalents>()?.Respec();
        }

        // Apply a saved talent build (respec + re-spend). Server validates every point.
        private void OnTalentLoadout(IServerPlayer fromPlayer, TalentLoadoutPacket packet)
        {
            fromPlayer?.Entity?.GetBehavior<EBTalents>()?.ApplyLoadout(packet?.Ids, packet?.Ranks);
        }

        // Admin panel actions; privilege is checked here, never trusted from the client.
        private void OnAdminProgression(IServerPlayer fromPlayer, AdminProgressionPacket packet)
        {
            if (fromPlayer == null || !fromPlayer.HasPrivilege(Privilege.controlserver)) return;
            if (ServerApi == null || string.IsNullOrEmpty(packet.TargetUid)) return;

            var target = ServerApi.World.PlayerByUid(packet.TargetUid) as IServerPlayer;
            var prog = target?.Entity?.GetBehavior<EBProgression>();
            if (prog == null) return;

            switch ((AdminAction)packet.Action)
            {
                case AdminAction.AddXp: prog.AddXp(packet.Value); break;
                case AdminAction.SetLevel: prog.SetLevel((int)packet.Value); break;
                case AdminAction.AddLevel: prog.SetLevel(prog.Level + (int)packet.Value); break;
            }
        }

        // Player-initiated class select/change (subject to the re-pick policy in ClassChange).
        private void OnSelectClass(IServerPlayer fromPlayer, SelectClassPacket packet)
        {
            if (fromPlayer?.Entity == null || string.IsNullOrEmpty(packet?.ClassId)) return;
            bool ok = ClassChange.TryChange(fromPlayer, packet.ClassId, adminOverride: false, out string reason);
            ServerChannel?.SendPacket(new SelectClassResultPacket
            {
                Success = ok,
                MessageKey = "canrpgclasses:classchange-" + (ok ? "ok" : reason),
                ClassId = TalentState.CurrentClass(fromPlayer.Entity)
            }, fromPlayer);
        }

        // Bind (or clear) the spell on the active held item; one spell per item.
        private void OnBindSpell(IServerPlayer fromPlayer, BindSpellPacket packet)
        {
            var slot = fromPlayer?.InventoryManager?.ActiveHotbarSlot;
            var stack = slot?.Itemstack;
            if (stack == null) return;

            string id = packet?.SpellId ?? "";
            if (string.IsNullOrEmpty(id))
            {
                Core.Items.BehaviorSpellContainer.SetBoundSpell(stack, null);
                slot!.MarkDirty();
                return;
            }
            if (!id.Contains(':')) id = ModId + ":" + id;
            if (!Spells.TryGet(id, out _)) return; // only real spells can be bound

            // PoolAllows only checks the item's pool tag, which most items lack, so also require the player to own
            // the spell; otherwise a forged packet could bind any spell.
            var entity = fromPlayer!.Entity;
            var cls = Classes.Get(TalentState.CurrentClass(entity));
            bool owned = (cls != null && System.Array.IndexOf(cls.BaseSpells, id) >= 0)
                || TalentState.GrantedSpells(entity, Talents).Contains(id);
            if (!owned) return;

            Core.Items.BehaviorSpellContainer.SetBoundSpell(stack, id);
            slot!.MarkDirty();
        }

        // Admin balance edit: apply, persist, rebuild, broadcast.
        private void OnBalanceEdit(IServerPlayer fromPlayer, BalanceEditPacket packet)
        {
            if (fromPlayer == null || !fromPlayer.HasPrivilege(Privilege.controlserver)) return;
            if (ServerApi == null || packet == null || string.IsNullOrEmpty(packet.Key)) return;
            if (!System.Enum.IsDefined(typeof(BalanceConfig.BalanceCategory), packet.Category)) return; // else BalanceConfig.Set throws

            var cat = (BalanceConfig.BalanceCategory)packet.Category;
            BalanceConfig.Set(cat, packet.Id ?? "", packet.Key, packet.Value);
            BalanceConfig.SaveOverrides(ServerApi);

            RebuildRegistries(null);

            BroadcastBalanceConfig();
        }

        // Talent-tree edit. Admin-gated, but the payload still lands in registries every player reads, so it is
        // size-capped before parsing and vetted by ContentEditGuard.
        private void OnTalentTreeEdit(IServerPlayer fromPlayer, TalentTreeEditPacket packet)
        {
            if (fromPlayer == null || !fromPlayer.HasPrivilege(Privilege.controlserver)) return;
            if (ServerApi == null || packet == null) return;

            if (packet.TalentsJson == null || packet.TalentsJson.Length > Core.Content.ContentEditGuard.MaxJsonBytes)
            {
                ReplyEdit(fromPlayer, false, "the edit is too large");
                return;
            }

            System.Collections.Generic.List<Core.Content.TalentModel>? incoming;
            try
            {
                incoming = Newtonsoft.Json.JsonConvert
                    .DeserializeObject<System.Collections.Generic.List<Core.Content.TalentModel>>(packet.TalentsJson);
            }
            catch (System.Exception e)
            {
                ReplyEdit(fromPlayer, false, "the edit isn't valid JSON - " + e.Message);
                return;
            }
            incoming ??= new System.Collections.Generic.List<Core.Content.TalentModel>();

            string classId = packet.ClassId ?? "";
            string? refused = Core.Content.ContentEditGuard.Reject(classId, packet.TreeIndex, incoming, Classes, Talents);
            if (refused != null)
            {
                ServerApi.Logger.Notification("[canrpgclasses] refused a tree edit from {0}: {1}",
                    fromPlayer.PlayerName, refused);
                ReplyEdit(fromPlayer, false, refused);
                return;
            }

            Core.Content.ContentStore.ReplaceTree(classId, packet.TreeIndex, incoming);
            ApplyContentEdit(fromPlayer, "");
        }

        // Class editor: add, replace or delete one authored class. Same checks as OnTalentTreeEdit.
        private void OnClassEdit(IServerPlayer fromPlayer, ClassEditPacket packet)
        {
            if (fromPlayer == null || !fromPlayer.HasPrivilege(Privilege.controlserver)) return;
            if (ServerApi == null || packet == null) return;

            if (packet.Delete)
            {
                string? refusedDelete = RefuseClassDeletion(packet.ClassId ?? "");
                if (refusedDelete != null) { ReplyEdit(fromPlayer, false, refusedDelete); return; }

                Core.Content.ContentStore.RemoveClass(packet.ClassId!);
                ApplyContentEdit(fromPlayer, "Deleted.");
                return;
            }

            if (packet.ClassJson == null || packet.ClassJson.Length > Core.Content.ContentEditGuard.MaxJsonBytes)
            {
                ReplyEdit(fromPlayer, false, "the edit is too large");
                return;
            }

            Core.Content.ClassModel? model;
            try { model = Newtonsoft.Json.JsonConvert.DeserializeObject<Core.Content.ClassModel>(packet.ClassJson); }
            catch (System.Exception e)
            {
                ReplyEdit(fromPlayer, false, "the edit isn't valid JSON - " + e.Message);
                return;
            }
            if (model == null) { ReplyEdit(fromPlayer, false, "the edit is empty"); return; }

            string? refused = Core.Content.ContentEditGuard.RejectClass(model, Classes, Spells, Talents);
            if (refused != null)
            {
                ServerApi.Logger.Notification("[canrpgclasses] refused a class edit from {0}: {1}",
                    fromPlayer.PlayerName, refused);
                ReplyEdit(fromPlayer, false, refused);
                return;
            }

            Core.Content.ContentStore.ReplaceClass(model);
            ApplyContentEdit(fromPlayer, "");
        }

        // Spell editor: add, replace or delete one authored spell. Same checks as OnTalentTreeEdit.
        private void OnSpellEdit(IServerPlayer fromPlayer, SpellEditPacket packet)
        {
            if (fromPlayer == null || !fromPlayer.HasPrivilege(Privilege.controlserver)) return;
            if (ServerApi == null || packet == null) return;

            if (packet.Delete)
            {
                if (Spells.Get(packet.SpellId ?? "") is not Core.Content.ContentSpell)
                {
                    ReplyEdit(fromPlayer, false, $"'{packet.SpellId}' isn't an authored spell, so there is nothing to delete");
                    return;
                }
                Core.Content.ContentStore.RemoveSpell(packet.SpellId!);
                ApplyContentEdit(fromPlayer, "Deleted.");
                return;
            }

            if (packet.SpellJson == null || packet.SpellJson.Length > Core.Content.ContentEditGuard.MaxJsonBytes)
            {
                ReplyEdit(fromPlayer, false, "the edit is too large");
                return;
            }

            Core.Content.SpellModel? model;
            try { model = Newtonsoft.Json.JsonConvert.DeserializeObject<Core.Content.SpellModel>(packet.SpellJson); }
            catch (System.Exception e)
            {
                ReplyEdit(fromPlayer, false, "the edit isn't valid JSON - " + e.Message);
                return;
            }
            if (model == null) { ReplyEdit(fromPlayer, false, "the edit is empty"); return; }

            string? refused = Core.Content.ContentEditGuard.RejectSpell(model, Spells);
            if (refused != null)
            {
                ServerApi.Logger.Notification("[canrpgclasses] refused a spell edit from {0}: {1}",
                    fromPlayer.PlayerName, refused);
                ReplyEdit(fromPlayer, false, refused);
                return;
            }

            Core.Content.ContentStore.ReplaceSpell(model);
            ApplyContentEdit(fromPlayer, "");
        }

        /// <summary>Refusal reason or null. Blocks deleting a class someone online is playing.</summary>
        private string? RefuseClassDeletion(string classId)
        {
            if (string.IsNullOrEmpty(classId)) return "no class named";
            if (Classes.Get(classId) is not Core.Content.ContentClassDef)
                return $"'{classId}' isn't an authored class, so there is nothing to delete";

            foreach (var p in ServerApi!.World.AllOnlinePlayers)
            {
                var e = (p as IServerPlayer)?.Entity;
                if (e == null) continue;
                if (string.Equals(Core.Talents.TalentState.CurrentClass(e), classId, System.StringComparison.OrdinalIgnoreCase))
                    return $"{p.PlayerName} is playing this class right now";
            }
            return null;
        }

        // Persist, rebuild, broadcast, re-apply stat talents.
        private void ApplyContentEdit(IServerPlayer fromPlayer, string okMessage)
        {
            Core.Content.ContentStore.Save(ServerApi!);
            // With a logger, so ContentLoader's counts reach the server log.
            RebuildRegistries(ServerApi!.Logger);
            BroadcastContent();

            foreach (var p in ServerApi!.World.AllOnlinePlayers)
                (p as IServerPlayer)?.Entity?.GetBehavior<EBTalents>()?.ReapplyStatTalents();

            ReplyEdit(fromPlayer, true, LastContentReport?.FirstProblem ?? okMessage);
        }

        private void ReplyEdit(IServerPlayer player, bool ok, string message)
            => ServerChannel?.SendPacket(new ContentEditResultPacket { Ok = ok, Message = message ?? "" }, player);

        // One SendPacket to all recipients serializes once.
        private void BroadcastContent()
        {
            if (ServerApi == null || ServerChannel == null) return;
            var msg = new ContentSyncPacket { Json = Core.Content.ContentStore.Serialize() };
            var online = new System.Collections.Generic.List<IServerPlayer>();
            foreach (var p in ServerApi.World.AllOnlinePlayers) if (p is IServerPlayer sp) online.Add(sp);
            ServerChannel.SendPacket(msg, online.ToArray());
        }

        private void BroadcastBalanceConfig()
        {
            if (ServerApi == null || ServerChannel == null) return;
            var msg = new BalanceConfigPacket
            {
                Json = BalanceConfig.Serialize(),
                Attributes = Core.Attributes.RpgAttributes.Serialize()
            };
            var online = new System.Collections.Generic.List<IServerPlayer>();
            foreach (var p in ServerApi.World.AllOnlinePlayers) if (p is IServerPlayer sp) online.Add(sp);
            ServerChannel.SendPacket(msg, online.ToArray());
        }

        // Join sync: server-owned config and content the client has no copy of.
        private void OnPlayerNowPlaying(IServerPlayer player)
        {
            if (player == null) return;
            ServerChannel?.SendPacket(new BalanceConfigPacket
            {
                Json = BalanceConfig.Serialize(),
                Attributes = Core.Attributes.RpgAttributes.Serialize()
            }, player);
            ServerChannel?.SendPacket(new ClassRestrictionsPacket { Json = Core.Classes.ClassRestrictions.Serialize() }, player);
            Core.Portals.PortalAnchors.SendTo(player);
            ServerChannel?.SendPacket(new ContentSyncPacket { Json = Core.Content.ContentStore.Serialize() }, player);
            // Persisted cooldowns, or the HUD shows them as ready after a relog.
            player.Entity?.GetBehavior<Core.EB.EBSpellCooldowns>()?.SyncAll();
            if (ServerApi != null) Core.Integration.PlayerModelSwap.ScheduleReapplyOnJoin(ServerApi, player);
            if (ServerApi != null) Hunter.HunterPetSystem.ScheduleRestoreOnJoin(ServerApi, player);
        }

        private void PushPartyResources()
        {
            var sapi = ServerApi;
            var mgr = sapi?.ModLoader.GetModSystem<canparty.canpartyModSystem>()?.Manager;
            var parties = mgr?.GetAllParties();
            if (sapi == null || ServerChannel == null || parties == null) return;

            foreach (var party in parties)
            {
                var uids = new System.Collections.Generic.List<string>();
                var fracs = new System.Collections.Generic.List<float>();
                var colors = new System.Collections.Generic.List<int>();
                var petFracs = new System.Collections.Generic.List<float>();
                var petNames = new System.Collections.Generic.List<string>();
                var online = new System.Collections.Generic.List<IServerPlayer>();
                foreach (var uid in party.playersGuids)
                {
                    if (sapi.World.PlayerByUid(uid) is not IServerPlayer plr
                        || plr.ConnectionState != EnumClientState.Playing || plr.Entity == null) continue;
                    online.Add(plr);
                    var pool = Core.Resources.ResourceState.PrimaryPool(plr.Entity);
                    if (pool == null) continue; // member has no class/resource → no bar
                    float max = Core.Resources.ResourceState.EffectiveMax(plr.Entity, pool);
                    float frac = max > 0f ? Core.Resources.ResourceState.Get(plr.Entity, pool) / max : 0f;
                    int color = (((int)(pool.ColorR * 255f)) << 16) | (((int)(pool.ColorG * 255f)) << 8) | (int)(pool.ColorB * 255f);
                    uids.Add(uid); fracs.Add(frac); colors.Add(color);

                    var (petFrac, petName) = Hunter.HunterPetSystem.PetStatus(plr.Entity);
                    petFracs.Add(petFrac); petNames.Add(petName);
                }
                if (uids.Count == 0) continue;
                var msg = new PartyResourceMsg
                {
                    Uids = uids.ToArray(), Fractions = fracs.ToArray(), Colors = colors.ToArray(),
                    PetFractions = petFracs.ToArray(), PetNames = petNames.ToArray()
                };
                ServerChannel.SendPacket(msg, online.ToArray());
            }
        }

        // ---- Client packet handlers ----

        // Rebuild so constructors re-read the server's numbers.
        private void OnBalanceConfig(BalanceConfigPacket packet)
        {
            if (packet == null || string.IsNullOrEmpty(packet.Json)) return;
            BalanceConfig.ApplySerialized(packet.Json);
            Core.Attributes.RpgAttributes.ApplySerialized(packet.Attributes, ClientApi?.Logger);

            RebuildRegistries(null);

            ClientApi?.World?.Player?.Entity?.GetBehavior<EBTalents>()?.ReapplyStatTalents();
        }

        private void OnContentSync(ContentSyncPacket packet)
        {
            if (packet == null) return;
            Core.Content.ContentStore.Adopt(packet.Json, ClientApi?.Logger);

            RebuildRegistries(null);

            ClientApi?.World?.Player?.Entity?.GetBehavior<EBTalents>()?.ReapplyStatTalents();

            // Tell any open editor the round trip landed.
            NotifyContentChanged();
        }

        // Edit outcome in chat, so a refused edit doesn't just vanish.
        private void OnContentEditResult(ContentEditResultPacket packet)
        {
            if (packet == null || ClientApi == null) return;
            if (packet.Ok && string.IsNullOrEmpty(packet.Message)) return;
            ClientApi.ShowChatMessage(packet.Ok
                ? "Saved, but: " + packet.Message
                : "Not saved: " + packet.Message);
        }

        private void OnClassRestrictions(ClassRestrictionsPacket packet)
        {
            if (packet == null) return;
            Core.Classes.ClassRestrictions.ApplySerialized(packet.Json ?? "");
        }

        private void OnCastSync(SpellCastSyncPacket packet)
        {
            // Cosmetic body animation; before the cast-bar early return so instant skills animate too.
            if (Spells.TryGet(packet.SpellId, out var castSpell) && !string.IsNullOrEmpty(castSpell.AnimationCode))
            {
                var caster = ClientApi?.World?.GetEntityById(packet.CasterEntityId);
                // NPC bodies without player animations opt out.
                if (caster?.WatchedAttributes.GetBool(Core.RpgCombatApi.SuppressSpellAnimationKey) == true) caster = null;
                caster?.AnimManager?.StartAnimation(new AnimationMetaData
                {
                    Animation = castSpell.AnimationCode,
                    Code = castSpell.AnimationCode,
                    AnimationSpeed = 1f,
                    BlendMode = EnumAnimationBlendMode.AddAverage
                }.Init());
            }

            // Timed casts only (Charge or Channel); instant ones have nothing to show.
            if ((packet.CastMode != (int)CastMode.Charge && packet.CastMode != (int)CastMode.Channel) || packet.Duration <= 0f) return;

            if (castSpell != null) castSounds?.Start(packet.CasterEntityId, castSpell, packet.Duration);
            string name = Spells.TryGet(packet.SpellId, out var s) ? s.DisplayName : packet.SpellId;
            bool isLocal = ClientApi?.World?.Player?.Entity?.EntityId == packet.CasterEntityId;
            if (isLocal)
                hotbarHud?.ShowCastBar(name, packet.Duration); // own cast → hotbar cast bar
            else
                combatOverlay?.AddWorldCast(packet.CasterEntityId, name, packet.Duration); // other caster → world cast bar
        }

        private void OnCastCancel(SpellCastCancelPacket packet)
        {
            castSounds?.Stop(packet.CasterEntityId);
            if (ClientApi?.World?.Player?.Entity?.EntityId == packet.CasterEntityId)
                hotbarHud?.HideCastBar();
            else
                combatOverlay?.RemoveWorldCast(packet.CasterEntityId);
        }

        private void OnCooldown(SpellCooldownPacket packet)
        {
            var cd = LocalCooldowns();
            if (cd == null) return;
            // Remaining<=0 = a reset (Regroup): clear the stale entry instead of no-op'ing in SetCooldown.
            if (packet.Remaining <= 0f) cd.ClearCooldown(packet.Key, sync: false);
            else cd.SetCooldown(packet.Key, packet.Remaining, sync: false, total: packet.Duration);
        }

        private void OnCooldownSync(SpellCooldownSyncPacket packet)
        {
            var cd = LocalCooldowns();
            if (cd == null) return;
            foreach (var c in packet.Cooldowns) cd.SetCooldown(c.Key, c.Remaining, sync: false, total: c.Duration);
        }

        private void OnSpellMessage(SpellMessagePacket packet)
        {
            ClientApi?.ShowChatMessage(packet.Message);
        }

        private void OnPartyResource(PartyResourceMsg packet) => partyResourceOverlay?.Apply(packet);

        private void OnSelectClassResult(SelectClassResultPacket packet)
        {
            ClientApi?.ShowChatMessage(Lang.Get(packet.MessageKey));
            if (packet.Success) classSelectGui?.Close();
        }

        private void OnDamageNumber(DamageNumberPacket packet)
        {
            combatOverlay?.AddDamageNumber(new Vintagestory.API.MathTools.Vec3d(packet.X, packet.Y, packet.Z), packet.Amount, packet.IsHeal);
        }

        // The owner camera follows MouseYaw, not TeleportTo's yaw (async chunk load), so force it briefly.
        private void OnSetViewYaw(SetViewYawPacket packet)
        {
            viewYawForcer?.Trigger(packet.Yaw, 400);
        }

        private EBSpellCooldowns? LocalCooldowns()
        {
            return ClientApi?.World?.Player?.Entity?.GetBehavior<EBSpellCooldowns>();
        }
    }
}
