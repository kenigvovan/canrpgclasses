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
    /// <summary>Network part of the mod system: message-type registration, the server-side packet handlers
    /// (cast requests, talents, class select, admin/balance edits) and the client-side ones (cast bars,
    /// cooldowns, chat, damage numbers), plus the periodic party-resource push.</summary>
    public partial class canrpgclassesModSystem
    {
        // Anti-spam throttle for OnSpellRequest, keyed by player UID. One entry per player, overwritten on every
        // accepted request; evicted on disconnect (OnPlayerDisconnectCleanup) so it can't grow with every player
        // who EVER joined over a long-lived server's life.
        private readonly System.Collections.Generic.Dictionary<string, long> lastSpellRequestMs = new();

        // Drops per-player transient server state on disconnect (hooked to PlayerDisconnect in StartServerSide).
        private void OnPlayerDisconnectCleanup(IServerPlayer player)
        {
            if (player?.PlayerUID != null) lastSpellRequestMs.Remove(player.PlayerUID);
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
        }

        // ---- Server: rename the sender's hunter pet (persists on the owner, re-applied on each summon) ----
        private void OnPetName(IServerPlayer fromPlayer, PetNamePacket packet)
        {
            if (fromPlayer?.Entity is not EntityAgent owner) return;
            string name = (packet?.Name ?? "").Trim();
            if (name.Length > 32) name = name.Substring(0, 32);
            Hunter.HunterPetSystem.SetPetName(owner, name);
        }

        private void OnSpellRequest(IServerPlayer fromPlayer, SpellRequestPacket packet)
        {
            var entity = fromPlayer?.Entity;
            if (entity == null) return;

            // Cheap anti-spam throttle: a flooding/broken client would otherwise still hit full cast validation
            // (access/resource/cooldown checks) on every packet for nothing - every cast is rejected anyway,
            // but there's no reason to do that work more than ~20x/sec per player.
            long now = ServerApi!.World.ElapsedMilliseconds;
            if (lastSpellRequestMs.TryGetValue(fromPlayer!.PlayerUID, out long last) && now - last < SpellRequestMinIntervalMs) return;
            lastSpellRequestMs[fromPlayer.PlayerUID] = now;

            // Server authority: a client can put any id in the packet, so verify the player is actually allowed to
            // cast it - it must be one of their class/talent spells OR the spell bound to the item in their hand.
            string id = packet.SpellId ?? "";
            if (!string.IsNullOrEmpty(id) && !id.Contains(':')) id = ModId + ":" + id;
            if (!IsCastAllowed(entity, id)) return;

            AttemptCast(entity, id, BuildAim(entity, packet));
        }

        /// <summary>Server-authoritative gate against a forged cast request: the player may only cast spells from
        /// their class/talents or the one bound to the item in hand. Single rule shared with the client UI via
        /// <see cref="Core.Spells.SpellAccess"/>. Resource/cooldown/etc. are enforced separately in TryCast.</summary>
        private bool IsCastAllowed(Entity entity, string fullSpellId)
            => Core.Spells.SpellAccess.CanCast(entity, this, fullSpellId);

        private static AimContext BuildAim(Entity caster, SpellRequestPacket packet)
        {
            var aim = new AimContext();
            if (packet.TargetEntityId != 0) aim.TargetEntity = caster.World.GetEntityById(packet.TargetEntityId);
            if (packet.HasAimPos) aim.AimPosition = new Vec3d(packet.AimX, packet.AimY, packet.AimZ);
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

        // Admin panel actions. Server-authoritative privilege check - never trust the client to gate this.
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

        // Bind (or clear) the spell on the player's active held item. Server writes it into the stack attributes;
        // the change syncs back to the client, and the right-click patch reads it to cast. One spell per item.
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

            // Server authority: a forged packet could name ANY registered spell (including another class's kit, or
            // one this player never spent a talent point on) - PoolAllows only restricts by the ITEM's own pool tag,
            // which most items don't set, so without this check binding would silently bypass class/talent gating
            // entirely. Only a spell this player's current class already has (base kit or talent-granted) may be bound.
            var entity = fromPlayer!.Entity;
            var cls = Classes.Get(TalentState.CurrentClass(entity));
            bool owned = (cls != null && System.Array.IndexOf(cls.BaseSpells, id) >= 0)
                || TalentState.GrantedSpells(entity, Talents).Contains(id);
            if (!owned) return;

            Core.Items.BehaviorSpellContainer.SetBoundSpell(stack, id);
            slot!.MarkDirty();
        }

        // Admin balance editor: apply one live-edited number, persist it, rebuild the registries so it takes
        // effect immediately server-side, then push the updated config to every connected client.
        private void OnBalanceEdit(IServerPlayer fromPlayer, BalanceEditPacket packet)
        {
            if (fromPlayer == null || !fromPlayer.HasPrivilege(Privilege.controlserver)) return;
            if (ServerApi == null || packet == null || string.IsNullOrEmpty(packet.Key)) return;
            if (!System.Enum.IsDefined(typeof(BalanceConfig.BalanceCategory), packet.Category)) return; // reject a malformed/forged category instead of letting BalanceConfig.Set throw

            var cat = (BalanceConfig.BalanceCategory)packet.Category;
            BalanceConfig.Set(cat, packet.Id ?? "", packet.Key, packet.Value);
            BalanceConfig.SaveOverrides(ServerApi);

            RebuildRegistries(null);

            BroadcastBalanceConfig();
        }

        // Talent-tree editor: replace one class tree with the set the editor sent, persist it, rebuild so it
        // takes effect at once, then push the new content to everyone.
        //
        // The packet is admin-gated, but it still arrives as arbitrary bytes and its contents end up in the
        // registries every player reads - so nothing here trusts it. The privilege is only the first gate:
        // the payload is size-capped before it is parsed, and ContentEditGuard vets every talent (see there for
        // what it refuses and why). A rejected edit changes nothing and is reported back to the sender.
        private void OnTalentTreeEdit(IServerPlayer fromPlayer, TalentTreeEditPacket packet)
        {
            if (fromPlayer == null || !fromPlayer.HasPrivilege(Privilege.controlserver)) return;
            if (ServerApi == null || packet == null) return;

            // Cap before parsing: deserializing an arbitrarily large payload is the cheapest way to hurt us.
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

        // Class editor: add, replace or delete one authored class. Same distrust as OnTalentTreeEdit - the
        // privilege is the first gate, ContentEditGuard.RejectClass is the one that reads the payload.
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

        // Spell editor: add, replace or delete one authored spell. Same distrust as the other content handlers.
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

        /// <summary>Why this class may not be deleted, or null when it may. A class someone is currently playing
        /// is the one case worth blocking: their character would be left naming a class nobody defines.</summary>
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

        // Persist, rebuild, broadcast, re-apply stat talents - the tail every content edit shares.
        private void ApplyContentEdit(IServerPlayer fromPlayer, string okMessage)
        {
            Core.Content.ContentStore.Save(ServerApi!);
            // Logged, not silent: a content edit that registers nothing is the one failure an admin can't see
            // from the game, and ContentLoader reports its counts through this logger.
            RebuildRegistries(ServerApi!.Logger);
            BroadcastContent();

            foreach (var p in ServerApi!.World.AllOnlinePlayers)
                (p as IServerPlayer)?.Entity?.GetBehavior<EBTalents>()?.ReapplyStatTalents();

            ReplyEdit(fromPlayer, true, LastContentReport?.FirstProblem ?? okMessage);
        }

        private void ReplyEdit(IServerPlayer player, bool ok, string message)
            => ServerChannel?.SendPacket(new ContentEditResultPacket { Ok = ok, Message = message ?? "" }, player);

        // Pushes the in-game authored content to every online player, so their tree and spellbook draw what the
        // server enforces. Same single-serialize fan-out as BroadcastBalanceConfig.
        private void BroadcastContent()
        {
            if (ServerApi == null || ServerChannel == null) return;
            var msg = new ContentSyncPacket { Json = Core.Content.ContentStore.Serialize() };
            var online = new System.Collections.Generic.List<IServerPlayer>();
            foreach (var p in ServerApi.World.AllOnlinePlayers) if (p is IServerPlayer sp) online.Add(sp);
            ServerChannel.SendPacket(msg, online.ToArray());
        }

        // Pushes the freshly-merged balance numbers to every online player (single serialize, fanned out -
        // same pattern as PushPartyResources) so their spellbook/talent tree/UI reflect the admin's edit at once.
        private void BroadcastBalanceConfig()
        {
            if (ServerApi == null || ServerChannel == null) return;
            var msg = new BalanceConfigPacket { Json = BalanceConfig.Serialize() };
            var online = new System.Collections.Generic.List<IServerPlayer>();
            foreach (var p in ServerApi.World.AllOnlinePlayers) if (p is IServerPlayer sp) online.Add(sp);
            ServerChannel.SendPacket(msg, online.ToArray());
        }

        // Server → client: hand the joining player our merged balance numbers so their UI/prediction match us.
        private void OnPlayerNowPlaying(IServerPlayer player)
        {
            if (player == null) return;
            ServerChannel?.SendPacket(new BalanceConfigPacket { Json = BalanceConfig.Serialize() }, player);
            // The vanilla-class restrictions live in the server's mod-config folder, so the client has no copy -
            // hand it one, or its class picker would offer classes the server is going to refuse.
            ServerChannel?.SendPacket(new ClassRestrictionsPacket { Json = Core.Classes.ClassRestrictions.Serialize() }, player);
            // Same for content authored in-game: it lives in the server's mod-config folder, so without this the
            // client would draw the shipped trees while the server enforces the edited ones.
            ServerChannel?.SendPacket(new ContentSyncPacket { Json = Core.Content.ContentStore.Serialize() }, player);
            // Push the player's persisted (relog-surviving) cooldowns so their HUD shows them - otherwise the icon
            // reads as ready while the server still enforces the cooldown (cast rejected with a chat message).
            player.Entity?.GetBehavior<Core.EB.EBSpellCooldowns>()?.SyncAll();
            // Model-swap form across relog (Spirit Wolf / druid forms): re-apply the swapped model once the client
            // is fully attached - the whole why/how lives in PlayerModelSwap.ScheduleReapplyOnJoin.
            if (ServerApi != null) Core.Integration.PlayerModelSwap.ScheduleReapplyOnJoin(ServerApi, player);
        }

        // Server → party clients: each online member's class-resource fraction (+ pool colour), for canparty's frames.
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
                }
                if (uids.Count == 0) continue;
                var msg = new PartyResourceMsg { Uids = uids.ToArray(), Fractions = fracs.ToArray(), Colors = colors.ToArray() };
                // Single SendPacket call: the channel serializes msg once and fans it out to all recipients,
                // instead of re-serializing per member (was O(n²) per party per tick).
                ServerChannel.SendPacket(msg, online.ToArray());
            }
        }

        // ---- Client packet handlers ----

        // Client: adopt the server's balance numbers, then rebuild the registries so spell/talent constructors
        // re-read them (DescArgs, costs, affinity values) and re-apply stat talents for local prediction.
        private void OnBalanceConfig(BalanceConfigPacket packet)
        {
            if (packet == null || string.IsNullOrEmpty(packet.Json)) return;
            BalanceConfig.ApplySerialized(packet.Json);

            RebuildRegistries(null);

            ClientApi?.World?.Player?.Entity?.GetBehavior<EBTalents>()?.ReapplyStatTalents();
        }

        // Client: adopt the server's authored content and rebuild, so the talent tree and spellbook show the
        // same talents the server will accept points for.
        private void OnContentSync(ContentSyncPacket packet)
        {
            if (packet == null) return;
            Core.Content.ContentStore.Adopt(packet.Json, ClientApi?.Logger);

            RebuildRegistries(null);

            ClientApi?.World?.Player?.Entity?.GetBehavior<EBTalents>()?.ReapplyStatTalents();

            // An open editor is looking at the registries that just changed - tell it, instead of leaving it to
            // guess when the round trip landed.
            NotifyContentChanged();
        }

        // Client: the outcome of an edit this player sent, shown in chat so a refused talent doesn't just vanish.
        private void OnContentEditResult(ContentEditResultPacket packet)
        {
            if (packet == null || ClientApi == null) return;
            if (packet.Ok && string.IsNullOrEmpty(packet.Message)) return;
            ClientApi.ShowChatMessage(packet.Ok
                ? "Saved, but: " + packet.Message
                : "Not saved: " + packet.Message);
        }

        // Client: adopt the server's vanilla-class restrictions, so the class picker greys out what would be
        // refused. Purely cosmetic - the server re-checks every pick.
        private void OnClassRestrictions(ClassRestrictionsPacket packet)
        {
            if (packet == null) return;
            Core.Classes.ClassRestrictions.ApplySerialized(packet.Json ?? "");
        }

        private void OnCastSync(SpellCastSyncPacket packet)
        {
            // Cosmetic body animation on use (e.g. Sweeping Blades -> "falx"). Resolved from the caster's player shape by
            // the spell's AnimationCode. Must run before the cast-bar early-return so instant skills animate too; it
            // does not gate the impacts (those already landed server-side).
            if (Spells.TryGet(packet.SpellId, out var castSpell) && !string.IsNullOrEmpty(castSpell.AnimationCode))
            {
                var caster = ClientApi?.World?.GetEntityById(packet.CasterEntityId);
                caster?.AnimManager?.StartAnimation(new AnimationMetaData
                {
                    Animation = castSpell.AnimationCode,
                    Code = castSpell.AnimationCode,
                    AnimationSpeed = 1f,
                    BlendMode = EnumAnimationBlendMode.AddAverage
                }.Init());
            }

            // Timed casts only (Charge cast bar OR a Channel like Ice Storm/Mystic Barrage/Mana Draw) - instant casts
            // (Duration 0, incl. proc'd instant casts) have nothing to show or interrupt.
            if ((packet.CastMode != (int)CastMode.Charge && packet.CastMode != (int)CastMode.Channel) || packet.Duration <= 0f) return;

            string name = Spells.TryGet(packet.SpellId, out var s) ? s.DisplayName : packet.SpellId;
            bool isLocal = ClientApi?.World?.Player?.Entity?.EntityId == packet.CasterEntityId;
            if (isLocal)
                hotbarHud?.ShowCastBar(name, packet.Duration); // own cast → hotbar cast bar
            else
                combatOverlay?.AddWorldCast(packet.CasterEntityId, name, packet.Duration); // other caster → world cast bar
        }

        private void OnCastCancel(SpellCastCancelPacket packet)
        {
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

        // The owner camera is rendered from MouseYaw; ViewYawForcer snaps it for a short window. Used by
        // shadow_step (TeleportTo's own yaw is unreliable for the owner camera due to async chunk-load).
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
