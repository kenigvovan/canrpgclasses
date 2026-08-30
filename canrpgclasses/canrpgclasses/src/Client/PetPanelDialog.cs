using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using canrpgclasses.Core.Net;
using canrpgclasses.Core.Talents;
using canrpgclasses.Hunter;

namespace canrpgclasses.Client
{
    /// <summary>
    /// The hunter's pet panel: rename the wolf, watch its health and issue every pet order from buttons instead
    /// of the hotbar. Orders go out as ordinary cast requests. Health and mode are pushed into the existing
    /// elements on a tick; only losing or summoning a pet rebuilds the dialog.
    /// </summary>
    public class PetPanelDialog : CanrpgDialog
    {
        private const string ComposerKey = "canrpgpetpanel";

        private string nameBuf = "";
        private string lastSyncedName = "";
        private bool hadPet;
        private long listenerId;

        public PetPanelDialog(ICoreClientAPI capi) : base(capi)
        {
            listenerId = capi.Event.RegisterGameTickListener(_ => Refresh(), 500);
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            hadPet = Pet() != null;
            Compose();
        }

        private Entity? Pet()
        {
            var owner = capi.World?.Player?.Entity;
            if (owner == null) return null;
            long petId = owner.WatchedAttributes.GetLong(HunterPetSystem.OwnerPetIdKey, 0);
            if (petId == 0) return null;
            var pet = capi.World.GetEntityById(petId);
            return pet != null && pet.Alive ? pet : null;
        }

        private void Refresh()
        {
            if (!IsOpened()) return;

            var pet = Pet();
            if (pet != null != hadPet) { hadPet = pet != null; Compose(); return; }
            if (pet == null) return;

            var health = pet.WatchedAttributes.GetTreeAttribute("health");
            float cur = health?.GetFloat("currenthealth") ?? 0f;
            float max = health?.GetFloat("maxhealth") ?? 1f;
            SingleComposer.GetStatbar("pethealth")?.SetValues(cur, 0, max);

            var mode = (PetMode)pet.WatchedAttributes.GetInt(HunterPetSystem.PetModeKey, (int)PetMode.Aggressive);
            SingleComposer.GetToggleButton("aggressive")?.SetValue(mode == PetMode.Aggressive);
            SingleComposer.GetToggleButton("defensive")?.SetValue(mode == PetMode.Defensive);
            SingleComposer.GetToggleButton("passive")?.SetValue(mode == PetMode.Passive);
            SingleComposer.GetToggleButton("stay")?.SetValue(
                pet.WatchedAttributes.GetBool(HunterPetSystem.PetStayKey, false));

            // Adopt the owner's stored pet name only when THAT value changes (rename confirmed by the server or
            // another source) - not simply whenever the box is unfocused. Mirroring while unfocused had a frame
            // race: pressing Rename defocuses the input, and the mirror wiped the typed text one frame before the
            // button fired, so every rename sent "".
            var owner = capi.World?.Player?.Entity;
            string synced = owner?.WatchedAttributes.GetString(HunterPetSystem.PetNameKey, "") ?? "";
            if (synced != lastSyncedName)
            {
                lastSyncedName = synced;
                nameBuf = synced;
                SingleComposer.GetTextInput("petname")?.SetValue(synced);
            }
        }

        private void Compose()
        {
            var owner = capi.World?.Player?.Entity;
            bool isHunter = owner != null && TalentState.CurrentClass(owner) == "hunter";
            var pet = Pet();

            const double w = 320, rowH = 26, gap = 6;
            double y = 0;

            var compo = capi.Gui.CreateCompo(ComposerKey,
                    ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
                .AddShadedDialogBG(ElementBounds.Fixed(0, 0, w + 40, isHunter ? 260 : 120), true, 5.0, 0.75f)
                .AddDialogTitleBar(Lang.Get("canrpgclasses:ui-pet-title"), () => TryClose())
                .BeginChildElements(ElementBounds.Fixed(20, 30, w, 220));

            if (!isHunter)
            {
                compo.AddStaticText(Lang.Get("canrpgclasses:ui-pet-nothunter"), CairoFont.WhiteSmallText(),
                    ElementBounds.Fixed(0, 0, w, rowH));
            }
            else
            {
                compo.AddStaticText(Lang.Get("canrpgclasses:ui-pet-name"), CairoFont.WhiteSmallText(),
                    ElementBounds.Fixed(0, y, w, rowH));
                y += rowH;
                compo.AddTextInput(ElementBounds.Fixed(0, y, 200, rowH), v => nameBuf = v,
                    CairoFont.WhiteSmallText(), "petname");
                compo.AddSmallButton(Lang.Get("canrpgclasses:ui-pet-rename"), Rename,
                    ElementBounds.Fixed(208, y, 112, rowH));
                y += rowH + gap * 2;

                if (pet == null)
                {
                    compo.AddStaticText(Lang.Get("canrpgclasses:ui-pet-none"), CairoFont.WhiteSmallText(),
                        ElementBounds.Fixed(0, y, w, rowH));
                    y += rowH + gap;
                    compo.AddSmallButton(Lang.Get("canrpgclasses:ui-pet-summon"), () => Cast("summon_pet"),
                        ElementBounds.Fixed(0, y, 160, rowH));
                }
                else
                {
                    compo.AddStatbar(ElementBounds.Fixed(0, y, w, 14), GuiStyle.HealthBarColor, "pethealth");
                    y += 14 + gap * 2;

                    compo.AddStaticText(Lang.Get("canrpgclasses:ui-pet-mode"), CairoFont.WhiteDetailText(),
                        ElementBounds.Fixed(0, y, w, rowH));
                    y += rowH;
                    compo.AddToggleButton(Lang.Get("canrpgclasses:ui-pet-aggressive"), CairoFont.SmallButtonText(),
                        _ => Cast("pet_aggressive"), ElementBounds.Fixed(0, y, 100, rowH), "aggressive");
                    compo.AddToggleButton(Lang.Get("canrpgclasses:ui-pet-defensive"), CairoFont.SmallButtonText(),
                        _ => Cast("pet_defensive"), ElementBounds.Fixed(108, y, 100, rowH), "defensive");
                    compo.AddToggleButton(Lang.Get("canrpgclasses:ui-pet-passive"), CairoFont.SmallButtonText(),
                        _ => Cast("pet_passive"), ElementBounds.Fixed(216, y, 100, rowH), "passive");
                    y += rowH + gap;

                    compo.AddToggleButton(Lang.Get("canrpgclasses:ui-pet-stay"), CairoFont.SmallButtonText(),
                        _ => Cast("pet_stay"), ElementBounds.Fixed(0, y, 100, rowH), "stay");
                    compo.AddSmallButton(Lang.Get("canrpgclasses:ui-pet-come"), () => Cast("pet_come"),
                        ElementBounds.Fixed(108, y, 100, rowH));
                    compo.AddSmallButton(Lang.Get("canrpgclasses:ui-pet-dismiss"), () => Cast("pet_dismiss"),
                        ElementBounds.Fixed(216, y, 100, rowH));
                }
            }

            ClearComposers();
            SingleComposer = compo.EndChildElements().Compose();

            SingleComposer.GetTextInput("petname")?.SetValue(nameBuf);
            lastSyncedName = ""; // force the next Refresh to re-adopt the synced name into the new element
            Refresh();
        }

        private bool Rename()
        {
            canrpgclassesModSystem.ClientInstance?.ClientChannel?.SendPacket(
                new PetNamePacket { Name = nameBuf.Trim() });
            return true;
        }

        private bool Cast(string localId)
        {
            canrpgclassesModSystem.ClientInstance?.ClientChannel?.SendPacket(
                new SpellRequestPacket { SpellId = "canrpgclasses:" + localId });
            return true;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (listenerId != 0) { capi.Event.UnregisterGameTickListener(listenerId); listenerId = 0; }
        }
    }
}
