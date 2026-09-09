using Vintagestory.API.Client;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Exports and imports the client's whole hotbar setup as one file: bars with their placement and contents,
    /// the HUD blocks, the input mode and, optionally, the cast keys. Written to the game's ModConfig folder, so
    /// a profile can simply be sent to someone else.
    /// </summary>
    public static class ProfileIo
    {
        public const int Version = 1;

        private static string FileName(string name) => "canrpgclasses-profile-" + Sanitize(name) + ".json";

        private static string Sanitize(string name)
        {
            var chars = name.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            string clean = new string(chars);
            return clean.Length == 0 ? "profile" : clean;
        }

        /// <summary>Writes the current setup. Returns the file name it was stored under, or null on failure.</summary>
        public static string? Export(ICoreClientAPI capi, HudLayout layout, SpellHotbar hotbar, string name)
        {
            var profile = new ProfileFile
            {
                Version = Version,
                Hotbar = hotbar.Config,
                Hud = layout.File,
                Keys = InputProfiles.Capture(capi)
            };

            try
            {
                string file = FileName(name);
                capi.StoreModConfig(profile, file);
                return file;
            }
            catch { return null; }
        }

        /// <summary>Loads a profile. <paramref name="withKeys"/> also takes over its cast-key bindings.</summary>
        public static bool Import(ICoreClientAPI capi, HudLayout layout, SpellHotbar hotbar, string name, bool withKeys)
        {
            ProfileFile? profile;
            try { profile = capi.LoadModConfig<ProfileFile>(FileName(name)); }
            catch { return false; }

            if (profile == null || profile.Version > Version) return false;

            if (profile.Hud != null) layout.ReplaceFile(profile.Hud);
            if (profile.Hotbar != null) hotbar.ReplaceConfig(profile.Hotbar);
            if (withKeys && profile.Keys is { Count: > 0 }) InputProfiles.Apply(capi, profile.Keys);
            return true;
        }
    }

    public class ProfileFile
    {
        public int Version = ProfileIo.Version;
        public HotbarConfig? Hotbar;
        public HudLayoutFile? Hud;
        public System.Collections.Generic.List<KeyBindingSnapshot>? Keys;
    }
}
