using System;
using System.Collections.Generic;
using canparty.Gui;
using canrpgclasses.Core.Net;
using Vintagestory.API.Config;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Supplies class-resource bars and hunter-wolf rows for canparty's frames via
    /// <see cref="PartyHudExtensions.ProvideMemberBar"/> and <see cref="PartyHudExtensions.ProvideMemberSubRow"/>;
    /// canparty draws them.
    /// </summary>
    public class PartyResourceOverlay : IDisposable
    {
        private readonly Dictionary<string, (float Frac, float R, float G, float B)> data = new();
        private readonly Dictionary<string, (float Frac, string Name)> pets = new();
        private readonly Func<string, PartyMemberBar?> provider;
        private readonly Func<string, PartySubRow?> petProvider;

        public PartyResourceOverlay()
        {
            provider = ProvideBar;
            petProvider = ProvidePetRow;
            PartyHudExtensions.ProvideMemberBar += provider;
            PartyHudExtensions.ProvideMemberSubRow += petProvider;
        }

        public void Apply(PartyResourceMsg msg)
        {
            data.Clear();
            pets.Clear();
            if (msg?.Uids == null || msg.Fractions == null || msg.Colors == null) return;
            int n = Math.Min(msg.Uids.Length, Math.Min(msg.Fractions.Length, msg.Colors.Length));
            for (int i = 0; i < n; i++)
            {
                int c = msg.Colors[i];
                data[msg.Uids[i]] = (msg.Fractions[i],
                    ((c >> 16) & 0xFF) / 255f, ((c >> 8) & 0xFF) / 255f, (c & 0xFF) / 255f);

                if (msg.PetFractions != null && i < msg.PetFractions.Length && msg.PetFractions[i] >= 0f)
                {
                    string name = msg.PetNames != null && i < msg.PetNames.Length ? msg.PetNames[i] : "";
                    pets[msg.Uids[i]] = (msg.PetFractions[i], name);
                }
            }
        }

        private PartyMemberBar? ProvideBar(string uid)
        {
            if (!data.TryGetValue(uid, out var r) || r.Frac < 0f) return null;
            return new PartyMemberBar(r.Frac, r.R, r.G, r.B);
        }

        private PartySubRow? ProvidePetRow(string uid)
        {
            if (!pets.TryGetValue(uid, out var p)) return null;
            string label = string.IsNullOrEmpty(p.Name) ? Lang.Get("canrpgclasses:ui-pet-title") : p.Name;
            return new PartySubRow(label, p.Frac);
        }

        public void Dispose()
        {
            PartyHudExtensions.ProvideMemberBar -= provider;
            PartyHudExtensions.ProvideMemberSubRow -= petProvider;
        }
    }
}
