using System.Collections.Generic;
using canrpgclasses.Core.Execution;
using canrpgclasses.Core.Spells;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace canrpgclasses.Core.Portals
{
    /// <summary>server → summoned player: accept a portal to the caster?</summary>
    [ProtoContract]
    public class SummonOfferPacket
    {
        [ProtoMember(1)] public long Id;
        [ProtoMember(2)] public string From = "";
        [ProtoMember(3)] public int Seconds;
    }

    /// <summary>client → server: the answer to a <see cref="SummonOfferPacket"/>.</summary>
    [ProtoContract]
    public class SummonAnswerPacket
    {
        [ProtoMember(1)] public long Id;
        [ProtoMember(2)] public bool Accept;
    }

    /// <summary>The SummonAlly impact: after the cast, the picked party member is asked; on yes a portal opens in
    /// front of them leading to the caster. Only the two of them can pass.</summary>
    public static class SummonRitual
    {
        public const string ShotKey = "s";
        private const int SummonTintA = 0xC8A032, SummonTintB = 0xFFF0B4;

        private sealed class Offer
        {
            public long Id;
            public EntityAgent Caster = null!;
            public string TargetUid = "";
            public long ExpiresMs;
            public float PortalSeconds;
            public byte[]? Shot;
        }

        private static readonly Dictionary<long, Offer> offers = new();
        private static long nextId;
        private static bool registered;

        public static void Init()
        {
            if (registered) return;
            registered = true;
            SpellExecutor.RegisterImpact(ImpactAction.SummonAlly, Apply);
            SpellExecutor.RegisterPrecheck(ImpactAction.SummonAlly, (c, s, imp, aim) => Check(c, aim.Choice, out _));
        }

        public static void Stop() => offers.Clear();

        private static string? Check(EntityAgent caster, string? uid, out IServerPlayer target)
        {
            target = null!;
            if (string.IsNullOrEmpty(uid)) return PortalSpells.Msg("summon-pick");
            if (caster.World.PlayerByUid(uid) is not IServerPlayer p || p.Entity == null || p.ConnectionState != EnumClientState.Playing)
                return PortalSpells.Msg("summon-offline");
            if (p.Entity == caster) return PortalSpells.Msg("summon-pick");
            if (!SpellExecutor.IsAlly(caster, p.Entity)) return PortalSpells.Msg("summon-not-party");
            if (p.Entity.Pos.Dimension != caster.Pos.Dimension) return PortalSpells.Msg("portal-other-dim");
            if (!p.Entity.Alive) return PortalSpells.Msg("summon-offline");
            target = p;
            return null;
        }

        private static void Apply(SpellContext ctx, Entity _, SpellImpact imp)
        {
            var caster = ctx.Caster;
            if (Check(caster, ctx.Aim?.Choice, out var target) is string refusal) { PortalSpells.Tell(caster, refusal); return; }

            // One pending offer per caster.
            foreach (var o in new List<Offer>(offers.Values))
                if (o.Caster == caster) offers.Remove(o.Id);

            int answerSeconds = (int)Cfg("answerSeconds", 30f);
            var offer = new Offer
            {
                Id = ++nextId,
                Caster = caster,
                TargetUid = target.PlayerUID,
                ExpiresMs = caster.World.ElapsedMilliseconds + answerSeconds * 1000L,
                PortalSeconds = imp.StatusEffectDuration > 0 ? imp.StatusEffectDuration : 20f
            };
            offers[offer.Id] = offer;

            string casterName = (caster as EntityPlayer)?.Player?.PlayerName ?? "?";
            canrpgclassesModSystem.For(caster.Api)?.ServerChannel?.SendPacket(
                new SummonOfferPacket { Id = offer.Id, From = casterName, Seconds = answerSeconds }, target);
            PortalSpells.Tell(caster, PortalSpells.Msg("summon-waiting", target.PlayerName));

            // The portal on the summoned player's side shows the caster's view.
            if ((caster as EntityPlayer)?.Player is IServerPlayer sp) PortalShots.Request(sp, ShotKey);
        }

        private static float Cfg(string key, float def) => Config.BalanceConfig.Spell("summon_ritual").F(key, def);

        public static void SetShot(string casterUid, byte[] shot)
        {
            foreach (var o in offers.Values)
                if ((o.Caster as EntityPlayer)?.PlayerUID == casterUid) o.Shot = shot;
        }

        public static void OnAnswer(IServerPlayer from, SummonAnswerPacket p)
        {
            if (!offers.TryGetValue(p.Id, out var offer) || offer.TargetUid != from.PlayerUID) return;
            offers.Remove(p.Id);
            var caster = offer.Caster;

            if (!p.Accept)
            {
                PortalSpells.Tell(caster, PortalSpells.Msg("summon-declined", from.PlayerName));
                return;
            }
            if (caster.World.ElapsedMilliseconds > offer.ExpiresMs || !caster.Alive || caster.State == EnumEntityState.Despawned)
            {
                PortalSpells.Tell(from.Entity, PortalSpells.Msg("summon-expired"));
                return;
            }
            var target = from.Entity;
            if (target == null || !target.Alive || target.Pos.Dimension != caster.Pos.Dimension) return;

            // Caster's end: in front of them, walking out the way they look (matches the snapshot).
            float casterYaw = PortalGeometry.SnapYaw(PortalSpells.ViewYaw(caster));
            var cf = PortalGeometry.Forward(casterYaw);
            var casterEnd = PortalManager.FindStandSpot(caster, caster.Pos.XYZ.AddCopy(cf.X * 1.5, 0, cf.Z * 1.5), new Vec3d(-cf.X, 0, -cf.Z));

            float targetYaw = PortalGeometry.SnapYaw(PortalSpells.ViewYaw(target));
            var tf = PortalGeometry.Forward(targetYaw);
            var targetEnd = PortalManager.FindStandSpot(target, target.Pos.XYZ.AddCopy(tf.X * 1.5, 0, tf.Z * 1.5), new Vec3d(-tf.X, 0, -tf.Z));

            if (casterEnd == null || targetEnd == null)
            {
                PortalSpells.Tell(caster, PortalSpells.Msg("portal-no-room"));
                PortalSpells.Tell(target, PortalSpells.Msg("portal-no-room"));
                return;
            }

            var allowed = new HashSet<string> { from.PlayerUID };
            if (caster is EntityPlayer cp) allowed.Add(cp.PlayerUID);
            // End A is the summoned player's; coming back you face where you came from.
            PortalManager.Open(caster, PortalKind.Summon, targetEnd, targetYaw + GameMath.PI, casterEnd, casterYaw,
                offer.PortalSeconds, SummonTintA, SummonTintB, offer.Shot, allowed);
        }
    }
}
