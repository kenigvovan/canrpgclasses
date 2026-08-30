using System;
using System.Collections.Generic;
using canparty.Gui;
using canrpgclasses.Core.Net;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Adds a thin class-resource bar per member to canparty's party frames. Only the data is ours: canparty asks
    /// <see cref="PartyHudExtensions.ProvideMemberBar"/> and draws the bar itself, knowing nothing about classes
    /// or resources.
    /// </summary>
    public class PartyResourceOverlay : IDisposable
    {
        private readonly Dictionary<string, (float Frac, float R, float G, float B)> data = new();
        private readonly Func<string, PartyMemberBar?> provider;

        public PartyResourceOverlay()
        {
            provider = ProvideBar;
            PartyHudExtensions.ProvideMemberBar += provider;
        }

        public void Apply(PartyResourceMsg msg)
        {
            data.Clear();
            if (msg?.Uids == null || msg.Fractions == null || msg.Colors == null) return;
            int n = Math.Min(msg.Uids.Length, Math.Min(msg.Fractions.Length, msg.Colors.Length));
            for (int i = 0; i < n; i++)
            {
                int c = msg.Colors[i];
                data[msg.Uids[i]] = (msg.Fractions[i],
                    ((c >> 16) & 0xFF) / 255f, ((c >> 8) & 0xFF) / 255f, (c & 0xFF) / 255f);
            }
        }

        private PartyMemberBar? ProvideBar(string uid)
        {
            if (!data.TryGetValue(uid, out var r) || r.Frac < 0f) return null;
            return new PartyMemberBar(r.Frac, r.R, r.G, r.B);
        }

        public void Dispose() => PartyHudExtensions.ProvideMemberBar -= provider;
    }
}
