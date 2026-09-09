using System.Collections.Generic;
using Vintagestory.API.Client;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Keeps one cast-key mapping per input mode. Leaving a mode snapshots its keys, entering one restores its own
    /// snapshot (or that mode's defaults the first time), so switching back and forth never loses a rebind.
    /// </summary>
    public static class InputProfiles
    {
        /// <summary>Shipped keys: Z R F V X, then Shift + Z R F V.</summary>
        public static List<KeyBindingSnapshot> KeyDefaults()
        {
            var keys = new[] { GlKeys.Z, GlKeys.R, GlKeys.F, GlKeys.V, GlKeys.X, GlKeys.Z, GlKeys.R, GlKeys.F, GlKeys.V };
            return Build(keys, i => i >= 5);
        }

        public static List<KeyBindingSnapshot> NumberRowDefaults()
        {
            var keys = new[]
            {
                GlKeys.Number1, GlKeys.Number2, GlKeys.Number3, GlKeys.Number4, GlKeys.Number5,
                GlKeys.Number6, GlKeys.Number7, GlKeys.Number8, GlKeys.Number9
            };
            return Build(keys, _ => false);
        }

        /// <summary>Presets only cover bar 1 - the other bars ship without keys and stay as the player left them.</summary>
        private static List<KeyBindingSnapshot> Build(GlKeys[] keys, System.Func<int, bool> shift)
        {
            var list = new List<KeyBindingSnapshot>();
            for (int slot = 0; slot < SpellHotbar.MaxSlots && slot < keys.Length; slot++)
                list.Add(new KeyBindingSnapshot
                {
                    Code = SpellHotbar.HotkeyCodes[0][slot],
                    KeyCode = (int)keys[slot],
                    Shift = shift(slot)
                });
            return list;
        }

        /// <summary>Every bar's cast keys as they are bound right now.</summary>
        public static List<KeyBindingSnapshot> Capture(ICoreClientAPI capi)
        {
            var list = new List<KeyBindingSnapshot>();
            foreach (var bar in SpellHotbar.HotkeyCodes)
                foreach (var code in bar)
                {
                    var hk = capi.Input.GetHotKeyByCode(code);
                    if (hk?.CurrentMapping == null) continue;
                    list.Add(new KeyBindingSnapshot
                    {
                        Code = code,
                        KeyCode = hk.CurrentMapping.KeyCode,
                        Shift = hk.CurrentMapping.Shift,
                        Ctrl = hk.CurrentMapping.Ctrl,
                        Alt = hk.CurrentMapping.Alt
                    });
                }
            return list;
        }

        public static void Apply(ICoreClientAPI capi, List<KeyBindingSnapshot> bindings)
        {
            foreach (var b in bindings)
            {
                var hk = capi.Input.GetHotKeyByCode(b.Code);
                if (hk == null) continue;

                var comb = new KeyCombination { KeyCode = b.KeyCode, Shift = b.Shift, Ctrl = b.Ctrl, Alt = b.Alt };
                hk.CurrentMapping = comb;
                Vintagestory.Client.NoObf.ClientSettings.Inst.SetKeyMapping(b.Code, comb);
            }
        }

        /// <summary>Switches modes, snapshotting the keys of the one being left and restoring those of the one
        /// being entered. Mouse mode leaves the keys alone - they simply stop firing.</summary>
        public static void Switch(ICoreClientAPI capi, HudLayout layout, InputMode target)
        {
            if (layout.Mode == target) return;

            layout.StoreProfile(layout.Mode, Capture(capi));
            layout.Mode = target;

            var saved = layout.Profile(target);
            if (saved != null) Apply(capi, saved);
            else if (target == InputMode.NumberRow) Apply(capi, NumberRowDefaults());
            else if (target == InputMode.Keys) Apply(capi, KeyDefaults());
        }

        /// <summary>Applies a preset to the current mode and remembers it as that mode's mapping.</summary>
        public static void SetForCurrentMode(ICoreClientAPI capi, HudLayout layout, List<KeyBindingSnapshot> bindings)
        {
            Apply(capi, bindings);
            layout.StoreProfile(layout.Mode, bindings);
        }
    }
}
