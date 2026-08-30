using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Client;
using canrpgclasses.Core.Content;
using canrpgclasses.Core.Net;
using canrpgclasses.Core.Spells;

namespace canrpgclasses.Client
{
    /// <summary>
    /// Admin-only spell editor: a spell is a delivery, a target rule and a list of impacts, all data, so one can
    /// be assembled or copied here. A new KIND of action can't - that is an executor branch in C#, so code spells
    /// are listed but locked and can only be copied.
    /// </summary>
    public class SpellEditorDialog : CanrpgDialog
    {
        private const string ComposerKey = "canrpgspelleditor";

        private const double ListWidth = 210;
        private const double PanelW = 430;
        private const double PanelHeight = 470;
        private const double RowH = 24;

        private enum Tab { Basics, Targeting, Impacts }

        private Tab tab = Tab.Basics;
        private SpellModel? draft;
        private string? editingId;
        private string? lockedSelection;
        private int selImpact = -1;
        private string status = "";
        private bool awaitingSync;
        private bool customEffect;

        public SpellEditorDialog(ICoreClientAPI capi) : base(capi) { }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Compose();
        }

        private static canrpgclassesModSystem? Mod => canrpgclassesModSystem.ClientInstance;

        private static List<Spell> AllSpells()
            => Mod?.Spells.All.Values.OrderBy(s => s.DisplayName).ToList() ?? new List<Spell>();

        private static bool IsAuthored(Spell s) => s is ContentSpell;

        private void Edit(string spellId)
        {
            var stored = ContentStore.Current?.spells?
                .FirstOrDefault(s => string.Equals(s.id, spellId, StringComparison.OrdinalIgnoreCase));
            draft = stored != null ? Clone(stored) : null;
            editingId = draft != null ? spellId : null;
            selImpact = draft?.impacts is { Count: > 0 } ? 0 : -1;
            status = "";
        }

        private static SpellModel Clone(SpellModel m)
            => Newtonsoft.Json.JsonConvert.DeserializeObject<SpellModel>(
                   Newtonsoft.Json.JsonConvert.SerializeObject(m)) ?? new SpellModel();

        /// <summary>Effect ids already used by registered spells. Effects live in effectshud, not in a registry we
        /// own, so what the existing content applies is the honest list of what exists.</summary>
        private static List<string> KnownEffects()
        {
            var ids = new List<string>();
            var mod = Mod;
            if (mod != null)
                foreach (var s in mod.Spells.All.Values)
                    foreach (var im in s.Impacts)
                        if (!string.IsNullOrEmpty(im.StatusEffectId) && !ids.Contains(im.StatusEffectId!))
                            ids.Add(im.StatusEffectId!);
            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return ids;
        }

        private void Compose()
        {
            double y = 34;
            double panelX = ListWidth + 30;

            var tabs = new[]
            {
                new GuiTab { Name = "Spell", DataInt = 0 },
                new GuiTab { Name = "Targeting", DataInt = 1 },
                new GuiTab { Name = "Impacts", DataInt = 2 },
            };

            var tabBounds = ElementBounds.Fixed(panelX, y - 30, PanelW, 28);
            var listClip = ElementBounds.Fixed(0, y, ListWidth, PanelHeight - 70);
            var listContainer = listClip.ForkContainingChild(0, 0, 0, -3);
            var listScroll = listClip.CopyOffsetedSibling(ListWidth + 3, 0, 0, 0).WithFixedWidth(20);
            var newBtn = ElementBounds.Fixed(0, y + PanelHeight - 62, ListWidth, RowH);

            var bgBounds = ElementBounds.Fixed(0, 0, ListWidth + PanelW + 90, y + PanelHeight + 40);
            var headerRule = ElementBounds.Fixed(panelX, y - 2, PanelW, 2);
            var panelPlate = ElementBounds.Fixed(panelX - 10, y + 4, PanelW + 20, PanelHeight - 66);

            ClearComposers();
            var compo = capi.Gui.CreateCompo(ComposerKey,
                    ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
                .AddShadedDialogBG(bgBounds, true, 5.0, 0.75f)
                .AddDialogTitleBar("RpgClasses Spell Editor", () => TryClose())
                .BeginChildElements(ElementBounds.Fixed(20, 30, ListWidth + PanelW + 50, bgBounds.fixedHeight))
                    .AddHorizontalTabs(tabs, tabBounds, OnTabClicked, CairoFont.WhiteSmallText(),
                        CairoFont.WhiteSmallText().WithColor(GuiStyle.ActiveButtonTextColor), "tabs")
                    .AddStaticCustomDraw(headerRule, EditorStyle.Rule)
                    .AddInset(panelPlate, 3, 0.85f)
                    .AddInset(listClip.FlatCopy().FixedGrow(3), 3, 0.85f)
                    .BeginClip(listClip)
                        .AddContainer(listContainer, "spelllist")
                    .EndClip()
                    .AddVerticalScrollbar(v => { listContainer.fixedY = 3 - v; listContainer.CalcWorldBounds(); },
                        listScroll, "spellscroll")
                    .AddSmallButton("New spell", NewSpell, newBtn);

            FillSpellList(compo);

            if (draft == null)
                AddNoDraftPanel(compo, panelX, y + 12);
            else
                switch (tab)
                {
                    case Tab.Basics: AddBasics(compo, panelX, y + 12); break;
                    case Tab.Targeting: AddTargeting(compo, panelX, y + 12); break;
                    default: AddImpacts(compo, panelX, y + 12); break;
                }

            SingleComposer = compo.EndChildElements().Compose();

            SingleComposer.GetHorizontalTabs("tabs").activeElement = (int)tab;
            listContainer.CalcWorldBounds();
            listClip.CalcWorldBounds();
            SingleComposer.GetScrollbar("spellscroll")
                .SetHeights((float)listClip.fixedHeight, (float)listContainer.fixedHeight);

            FillValues();
        }

        private void FillSpellList(GuiComposer compo)
        {
            var container = compo.GetContainer("spelllist");
            container.Clear();

            var rowBounds = ElementBounds.Fixed(0, 0, ListWidth - 12, RowH);
            foreach (var spell in AllSpells())
            {
                string id = spell.Id;
                bool authored = IsAuthored(spell);
                var font = authored ? CairoFont.WhiteDetailText() : EditorStyle.FieldLabel();
                container.Add(new GuiElementTextButton(capi,
                    authored ? spell.DisplayName : spell.DisplayName + "  (code)",
                    font, CairoFont.WhiteDetailText().WithColor(GuiStyle.ActiveButtonTextColor),
                    () => SelectSpell(id), rowBounds, EnumButtonStyle.Small));
                rowBounds = rowBounds.BelowCopy(0, 2);
            }
        }

        private void AddNoDraftPanel(GuiComposer compo, double x, double y)
        {
            double row = y;
            compo.AddStaticText("No spell loaded", EditorStyle.Heading(), ElementBounds.Fixed(x, row, PanelW, 22));
            row += 26;
            compo.AddStaticText("Press New spell, or click one on the left - the ones marked (code) are defined "
                    + "in C# and can be copied, numbers and impacts included.",
                EditorStyle.Body(), ElementBounds.Fixed(x, row, PanelW, 60));
            row += 66;

            if (status.Length > 0)
            {
                compo.AddStaticText(status, EditorStyle.Status(status.Contains("didn't") || status.Contains("Not ")),
                    ElementBounds.Fixed(x, row, PanelW, 44));
                row += 50;
            }

            if (lockedSelection != null)
                compo.AddSmallButton($"Copy '{lockedSelection}' into a new spell", CloneLocked,
                    ElementBounds.Fixed(x, row, PanelW, RowH));
        }

        private void AddBasics(GuiComposer compo, double x, double y)
        {
            var m = draft!;
            double row = y;

            Label(compo, x, ref row, "Id (permanent - hotbars and talents store it)");
            compo.AddTextInput(Field(x, row, PanelW), v => m.id = v.Trim(), CairoFont.WhiteDetailText(), "f_id");
            row += RowH + 8;

            Label(compo, x, ref row, "Name");
            compo.AddTextInput(Field(x, row, PanelW), v => m.name = v, CairoFont.WhiteDetailText(), "f_name");
            row += RowH + 8;

            Label(compo, x, ref row, "Description");
            compo.AddTextInput(Field(x, row, PanelW), v => m.description = v, CairoFont.WhiteDetailText(), "f_desc");
            row += RowH + 8;

            Label(compo, x, ref row, "Icon (optional) / school");
            compo.AddTextInput(Field(x, row, 190), v => m.icon = Blank(v), CairoFont.WhiteDetailText(), "f_icon");
            AddEnum<SpellSchool>(compo, Field(x + 198, row, 200), "f_school", m.school,
                v => { m.school = v; Compose(); });
            row += RowH + 8;

            Label(compo, x, ref row, "Cast: mode / seconds / channel ticks");
            AddEnum<CastMode>(compo, Field(x, row, 130), "f_castmode", m.castMode, v => { m.castMode = v; Compose(); });
            compo.AddNumberInput(Field(x + 138, row, 100), v => m.castSeconds = ParseFloat(v, m.castSeconds),
                    CairoFont.WhiteDetailText(), "f_castsec")
                .AddNumberInput(Field(x + 246, row, 100), v => m.channelTicks = ParseInt(v, m.channelTicks),
                    CairoFont.WhiteDetailText(), "f_ticks");
            row += RowH + 8;

            Label(compo, x, ref row, "Resource cost / cooldown seconds / range");
            compo.AddNumberInput(Field(x, row, 130), v => m.resourceCost = ParseFloat(v, m.resourceCost),
                    CairoFont.WhiteDetailText(), "f_cost")
                .AddNumberInput(Field(x + 138, row, 100), v => m.cooldown = ParseFloat(v, m.cooldown),
                    CairoFont.WhiteDetailText(), "f_cd")
                .AddNumberInput(Field(x + 246, row, 100), v => m.range = ParseFloat(v, m.range),
                    CairoFont.WhiteDetailText(), "f_range");
            row += RowH + 8;

            Label(compo, x, ref row, "Shared cooldown group (optional) / tier");
            compo.AddTextInput(Field(x, row, 190), v => m.cooldownGroup = Blank(v), CairoFont.WhiteDetailText(), "f_cdgroup")
                .AddNumberInput(Field(x + 198, row, 90), v => m.tier = ParseInt(v, m.tier),
                    CairoFont.WhiteDetailText(), "f_tier");
            row += RowH + 8;

            compo.AddSmallButton(m.triggersGlobalCooldown ? "Triggers global cooldown: yes" : "Triggers global cooldown: no",
                () => { m.triggersGlobalCooldown = !m.triggersGlobalCooldown; Compose(); return true; },
                ElementBounds.Fixed(x, row, PanelW, RowH));
            row += RowH + 12;

            AddFooter(compo, x, row);
        }

        // ---- tab: target, delivery, requirements, combo ----

        private void AddTargeting(GuiComposer compo, double x, double y)
        {
            var m = draft!;
            double row = y;

            m.target ??= new TargetModel();
            m.delivery ??= new DeliveryModel();
            m.requires ??= new RequiresModel();

            Section(compo, x, ref row, "Target");
            Label(compo, x, ref row, "Type / affinity");
            AddEnum<TargetType>(compo, Field(x, row, 190), "f_ttype", m.target.type,
                v => { m.target.type = v; Compose(); });
            AddEnum<TargetAffinity>(compo, Field(x + 198, row, 190), "f_taff", m.target.affinity,
                v => { m.target.affinity = v; Compose(); });
            row += RowH + 6;
            compo.AddSmallButton(m.target.includeCaster ? "Area includes the caster: yes" : "Area includes the caster: no",
                () => { m.target.includeCaster = !m.target.includeCaster; Compose(); return true; },
                ElementBounds.Fixed(x, row, PanelW, RowH));
            row += RowH + 12;

            Section(compo, x, ref row, "Delivery");
            Label(compo, x, ref row, "Type / velocity / count / spread");
            AddEnum<DeliveryType>(compo, Field(x, row, 130), "f_dtype", m.delivery.type,
                v => { m.delivery.type = v; Compose(); });
            compo.AddNumberInput(Field(x + 138, row, 84), v => m.delivery.velocity = ParseFloat(v, m.delivery.velocity),
                    CairoFont.WhiteDetailText(), "f_dvel")
                .AddNumberInput(Field(x + 230, row, 84), v => m.delivery.count = ParseInt(v, m.delivery.count),
                    CairoFont.WhiteDetailText(), "f_dcount")
                .AddNumberInput(Field(x + 322, row, 84), v => m.delivery.spreadDegrees = ParseFloat(v, m.delivery.spreadDegrees),
                    CairoFont.WhiteDetailText(), "f_dspread");
            row += RowH + 6;
            Label(compo, x, ref row, "Projectile entity (spellprojectile / spellorb)");
            compo.AddTextInput(Field(x, row, PanelW), v => m.delivery.entity = Blank(v),
                CairoFont.WhiteDetailText(), "f_dentity");
            row += RowH + 12;

            Section(compo, x, ref row, "Requires");
            compo.AddSmallButton(m.requires.bow ? "Bow: yes" : "Bow: no",
                    () => { m.requires.bow = !m.requires.bow; Compose(); return true; },
                    ElementBounds.Fixed(x, row, 138, RowH))
                .AddSmallButton(m.requires.shield ? "Shield: yes" : "Shield: no",
                    () => { m.requires.shield = !m.requires.shield; Compose(); return true; },
                    ElementBounds.Fixed(x + 146, row, 138, RowH))
                .AddSmallButton(m.requires.outOfCombat ? "Out of combat: yes" : "Out of combat: no",
                    () => { m.requires.outOfCombat = !m.requires.outOfCombat; Compose(); return true; },
                    ElementBounds.Fixed(x + 292, row, 138, RowH));
            row += RowH + 6;
            Label(compo, x, ref row, "Required form (spell id of a form, optional)");
            compo.AddTextInput(Field(x, row, PanelW), v => m.requires.form = Blank(v),
                CairoFont.WhiteDetailText(), "f_form");
            row += RowH + 12;

            Section(compo, x, ref row, "Combo points");
            compo.AddSmallButton(m.comboBuilder ? "Builder: yes" : "Builder: no",
                    () => { m.comboBuilder = !m.comboBuilder; Compose(); return true; },
                    ElementBounds.Fixed(x, row, 138, RowH))
                .AddSmallButton(m.comboFinisher ? "Finisher: yes" : "Finisher: no",
                    () => { m.comboFinisher = !m.comboFinisher; Compose(); return true; },
                    ElementBounds.Fixed(x + 146, row, 138, RowH))
                .AddNumberInput(Field(x + 292, row, 138),
                    v => m.comboPointsGenerated = ParseInt(v, m.comboPointsGenerated),
                    CairoFont.WhiteDetailText(), "f_combopoints");
            row += RowH + 12;

            AddFooter(compo, x, row);
        }

        private void AddImpacts(GuiComposer compo, double x, double y)
        {
            var m = draft!;
            var list = m.impacts ??= new List<ImpactModel>();
            double row = y;

            Section(compo, x, ref row, $"Impacts ({list.Count})");
            Label(compo, x, ref row, "What the spell does when it lands - each one in order.");

            for (int i = 0; i < list.Count; i++)
            {
                int idx = i;
                bool sel = i == selImpact;
                compo.AddSmallButton($"{i + 1}. {Describe(list[i])}", () => { selImpact = idx; Compose(); return true; },
                        ElementBounds.Fixed(x, row, PanelW - 88, RowH),
                        sel ? EnumButtonStyle.MainMenu : EnumButtonStyle.Small)
                    .AddSmallButton("Remove", () => RemoveImpact(idx), ElementBounds.Fixed(x + PanelW - 82, row, 82, RowH));
                row += RowH + 4;
            }

            compo.AddSmallButton("Add impact", AddImpact, ElementBounds.Fixed(x, row, 140, RowH));
            row += RowH + 10;

            if (selImpact >= 0 && selImpact < list.Count)
                AddImpactFields(compo, x, ref row, list[selImpact]);

            AddFooter(compo, x, row);
        }

        /// <summary>Only the fields the chosen action reads. An impact model carries every field any action could
        /// want, and showing all of them at once would bury the two that matter for this one.</summary>
        private void AddImpactFields(GuiComposer compo, double x, ref double row, ImpactModel im)
        {
            var action = ParseEnum(im.action, ImpactAction.Damage);

            Label(compo, x, ref row, "Action / chance 0..1");
            AddEnum<ImpactAction>(compo, Field(x, row, 240), "f_iaction", im.action,
                v => { im.action = v; Compose(); });
            compo.AddNumberInput(Field(x + 248, row, 100), v => im.chance = ParseFloat(v, im.chance),
                CairoFont.WhiteDetailText(), "f_ichance");
            row += RowH + 8;

            if (Scales(action))
            {
                Label(compo, x, ref row, "Spell-power coefficient / per combo point / knockback");
                compo.AddNumberInput(Field(x, row, 130), v => im.coefficient = ParseFloat(v, im.coefficient),
                        CairoFont.WhiteDetailText(), "f_icoef")
                    .AddNumberInput(Field(x + 138, row, 100), v => im.perComboPoint = ParseFloat(v, im.perComboPoint),
                        CairoFont.WhiteDetailText(), "f_icombo")
                    .AddNumberInput(Field(x + 246, row, 100), v => im.knockback = ParseFloat(v, im.knockback),
                        CairoFont.WhiteDetailText(), "f_iknock");
                row += RowH + 8;
            }

            if (action == ImpactAction.StatusEffect || action == ImpactAction.CoatWeapon
                || action == ImpactAction.ToggleAura || action == ImpactAction.GrantInstantCast)
            {
                Label(compo, x, ref row, "Effect id");
                var effects = KnownEffects();
                var codes = effects.Append(StatCatalog.Custom).ToArray();
                if (!customEffect && im.effectId != null && !effects.Contains(im.effectId)) customEffect = true;
                int sel = customEffect ? codes.Length - 1 : Math.Max(0, Array.IndexOf(codes, im.effectId ?? ""));
                compo.AddDropDown(codes, effects.Append("Type an id...").ToArray(), sel,
                    (code, _) =>
                    {
                        customEffect = code == StatCatalog.Custom;
                        if (!customEffect) im.effectId = code;
                        Compose();
                    }, Field(x, row, PanelW), "f_ieffect");
                row += RowH + 6;
                if (customEffect)
                {
                    compo.AddTextInput(Field(x, row, PanelW), v => im.effectId = Blank(v),
                        CairoFont.WhiteDetailText(), "f_ieffectid");
                    row += RowH + 6;
                }

                Label(compo, x, ref row, "Seconds / per combo point / amplifier / cap");
                compo.AddNumberInput(Field(x, row, 100), v => im.seconds = ParseFloat(v, im.seconds),
                        CairoFont.WhiteDetailText(), "f_isec")
                    .AddNumberInput(Field(x + 108, row, 100),
                        v => im.secondsPerComboPoint = ParseFloat(v, im.secondsPerComboPoint),
                        CairoFont.WhiteDetailText(), "f_isecombo")
                    .AddNumberInput(Field(x + 216, row, 100), v => im.amplifier = ParseInt(v, im.amplifier),
                        CairoFont.WhiteDetailText(), "f_iamp")
                    .AddNumberInput(Field(x + 324, row, 100), v => im.amplifierCap = ParseInt(v, im.amplifierCap),
                        CairoFont.WhiteDetailText(), "f_iampcap");
                row += RowH + 6;

                Label(compo, x, ref row, "Apply mode");
                AddEnum<StatusApplyMode>(compo, Field(x, row, 200), "f_iapply", im.applyMode,
                    v => { im.applyMode = v; Compose(); });
                row += RowH + 8;
            }
            else if (IsControl(action))
            {
                Label(compo, x, ref row, "Seconds");
                compo.AddNumberInput(Field(x, row, 120), v => im.seconds = ParseFloat(v, im.seconds),
                    CairoFont.WhiteDetailText(), "f_isec");
                row += RowH + 8;
            }

            if (action == ImpactAction.Shield)
            {
                Label(compo, x, ref row, "Shield coefficient / seconds");
                compo.AddNumberInput(Field(x, row, 130), v => im.shieldCoefficient = ParseFloat(v, im.shieldCoefficient),
                        CairoFont.WhiteDetailText(), "f_ishield")
                    .AddNumberInput(Field(x + 138, row, 100), v => im.shieldSeconds = ParseFloat(v, im.shieldSeconds),
                        CairoFont.WhiteDetailText(), "f_ishieldsec");
                row += RowH + 8;
            }

            if (action == ImpactAction.Teleport || action == ImpactAction.Dash)
            {
                Label(compo, x, ref row, "Distance / mode");
                compo.AddNumberInput(Field(x, row, 100), v => im.teleportDistance = ParseFloat(v, im.teleportDistance),
                    CairoFont.WhiteDetailText(), "f_itpdist");
                AddEnum<TeleportMode>(compo, Field(x + 108, row, 200), "f_itpmode", im.teleportMode,
                    v => { im.teleportMode = v; Compose(); });
                row += RowH + 8;
            }

            if (action == ImpactAction.DamageZone)
            {
                Label(compo, x, ref row, "Zone: seconds / tick / radius");
                compo.AddNumberInput(Field(x, row, 100), v => im.zoneSeconds = ParseFloat(v, im.zoneSeconds),
                        CairoFont.WhiteDetailText(), "f_izsec")
                    .AddNumberInput(Field(x + 108, row, 100), v => im.zoneTickSeconds = ParseFloat(v, im.zoneTickSeconds),
                        CairoFont.WhiteDetailText(), "f_iztick")
                    .AddNumberInput(Field(x + 216, row, 100), v => im.zoneRadius = ParseFloat(v, im.zoneRadius),
                        CairoFont.WhiteDetailText(), "f_izrad");
                row += RowH + 6;
                compo.AddSmallButton(im.zoneAtAimPoint ? "Placed at the aim point" : "Placed on the caster",
                    () => { im.zoneAtAimPoint = !im.zoneAtAimPoint; Compose(); return true; },
                    ElementBounds.Fixed(x, row, PanelW, RowH));
                row += RowH + 8;
            }

            if (action == ImpactAction.GainResource)
            {
                Label(compo, x, ref row, "Resource gained");
                compo.AddNumberInput(Field(x, row, 120), v => im.resourceGain = ParseFloat(v, im.resourceGain),
                    CairoFont.WhiteDetailText(), "f_igain");
                row += RowH + 8;
            }

            if (action == ImpactAction.Aggro || action == ImpactAction.Reveal)
            {
                Label(compo, x, ref row, "Radius");
                compo.AddNumberInput(Field(x, row, 120), v => im.aggroRange = ParseFloat(v, im.aggroRange),
                    CairoFont.WhiteDetailText(), "f_iaggro");
                row += RowH + 8;
            }

            if (action == ImpactAction.Cleanse)
            {
                Label(compo, x, ref row, "Effects removed (0 = all)");
                compo.AddNumberInput(Field(x, row, 120), v => im.cleanseMax = ParseInt(v, im.cleanseMax),
                    CairoFont.WhiteDetailText(), "f_icleanse");
                row += RowH + 8;
            }

            compo.AddSmallButton(im.affectCaster ? "Applies to: the caster" : "Applies to: the target",
                    () => { im.affectCaster = !im.affectCaster; Compose(); return true; },
                    ElementBounds.Fixed(x, row, 210, RowH))
                .AddSmallButton(string.Equals(im.particles, "school", StringComparison.OrdinalIgnoreCase)
                        ? "Particles: school burst" : "Particles: none",
                    () =>
                    {
                        im.particles = string.Equals(im.particles, "school", StringComparison.OrdinalIgnoreCase)
                            ? null : "school";
                        Compose();
                        return true;
                    }, ElementBounds.Fixed(x + 218, row, 210, RowH));
            row += RowH + 10;
        }

        /// <summary>Actions whose size comes from the spell-power coefficient.</summary>
        private static bool Scales(ImpactAction a)
            => a is ImpactAction.Damage or ImpactAction.Heal or ImpactAction.DamageZone
                  or ImpactAction.EmpowerNextMelee or ImpactAction.EmpowerNextShot;

        /// <summary>Control actions: one duration and nothing else to configure.</summary>
        private static bool IsControl(ImpactAction a)
            => a is ImpactAction.Stun or ImpactAction.Fear or ImpactAction.Silence
                  or ImpactAction.Root or ImpactAction.Transmute or ImpactAction.Disarm or ImpactAction.Evasion;

        private static string Describe(ImpactModel im)
        {
            var action = ParseEnum(im.action, ImpactAction.Damage);
            if (action == ImpactAction.StatusEffect && !string.IsNullOrEmpty(im.effectId))
                return $"{action}: {im.effectId}";
            if (Scales(action)) return $"{action} x{im.coefficient.ToString(CultureInfo.InvariantCulture)}";
            if (IsControl(action)) return $"{action} {im.seconds.ToString(CultureInfo.InvariantCulture)}s";
            return action.ToString();
        }

        private static ElementBounds Field(double x, double y, double w) => ElementBounds.Fixed(x, y, w, RowH);

        private static void Label(GuiComposer compo, double x, ref double row, string text)
        {
            compo.AddStaticText(text, EditorStyle.FieldLabel(), ElementBounds.Fixed(x, row, PanelW, 18));
            row += 20;
        }

        private static void Section(GuiComposer compo, double x, ref double row, string text)
        {
            compo.AddStaticText(text, EditorStyle.Heading(), ElementBounds.Fixed(x, row, PanelW, 22));
            row += 24;
        }

        /// <summary>A dropdown over an enum's names - the model stores the name, which is what the content files
        /// use and what <see cref="ContentReport.Enum{T}"/> parses.</summary>
        private static void AddEnum<T>(GuiComposer compo, ElementBounds bounds, string key, string? current,
                                       Action<string> onPicked) where T : struct, Enum
        {
            var names = Enum.GetNames(typeof(T));
            int sel = Math.Max(0, Array.FindIndex(names, n => string.Equals(n, current, StringComparison.OrdinalIgnoreCase)));
            compo.AddDropDown(names, names, sel, (code, _) => onPicked(code), bounds, key);
        }

        private static T ParseEnum<T>(string? value, T fallback) where T : struct
            => Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

        private void AddFooter(GuiComposer compo, double x, double row)
        {
            compo.AddStaticCustomDraw(ElementBounds.Fixed(x, row - 8, PanelW, 2), EditorStyle.Rule)
                .AddSmallButton("Save spell", SaveSpell, ElementBounds.Fixed(x, row, 200, RowH));
            if (editingId != null)
                compo.AddSmallButton("Delete spell", DeleteSpell, ElementBounds.Fixed(x + 208, row, 200, RowH));
            if (status.Length > 0)
                compo.AddStaticText(status, EditorStyle.Status(status.Contains("didn't") || status.Contains("Not ")),
                    ElementBounds.Fixed(x, row + RowH + 6, PanelW, 44));
        }

        private void FillValues()
        {
            var m = draft;
            if (m == null) return;

            if (tab == Tab.Basics)
            {
                SingleComposer.GetTextInput("f_id").SetValue(m.id ?? "");
                SingleComposer.GetTextInput("f_name").SetValue(m.name ?? "");
                SingleComposer.GetTextInput("f_desc").SetValue(m.description ?? "");
                SingleComposer.GetTextInput("f_icon").SetValue(m.icon ?? "");
                SingleComposer.GetNumberInput("f_castsec").SetValue(Num(m.castSeconds));
                SingleComposer.GetNumberInput("f_ticks").SetValue(m.channelTicks.ToString());
                SingleComposer.GetNumberInput("f_cost").SetValue(Num(m.resourceCost));
                SingleComposer.GetNumberInput("f_cd").SetValue(Num(m.cooldown));
                SingleComposer.GetNumberInput("f_range").SetValue(Num(m.range));
                SingleComposer.GetTextInput("f_cdgroup").SetValue(m.cooldownGroup ?? "");
                SingleComposer.GetNumberInput("f_tier").SetValue(m.tier.ToString());
            }
            else if (tab == Tab.Targeting)
            {
                SingleComposer.GetNumberInput("f_dvel").SetValue(Num(m.delivery!.velocity));
                SingleComposer.GetNumberInput("f_dcount").SetValue(m.delivery.count.ToString());
                SingleComposer.GetNumberInput("f_dspread").SetValue(Num(m.delivery.spreadDegrees));
                SingleComposer.GetTextInput("f_dentity").SetValue(m.delivery.entity ?? "");
                SingleComposer.GetTextInput("f_form").SetValue(m.requires!.form ?? "");
                SingleComposer.GetNumberInput("f_combopoints").SetValue(m.comboPointsGenerated.ToString());
            }
            else if (selImpact >= 0 && m.impacts is { Count: > 0 } && selImpact < m.impacts.Count)
            {
                var im = m.impacts[selImpact];
                var action = ParseEnum(im.action, ImpactAction.Damage);

                SingleComposer.GetNumberInput("f_ichance").SetValue(Num(im.chance));
                if (Scales(action))
                {
                    SingleComposer.GetNumberInput("f_icoef").SetValue(Num(im.coefficient));
                    SingleComposer.GetNumberInput("f_icombo").SetValue(Num(im.perComboPoint));
                    SingleComposer.GetNumberInput("f_iknock").SetValue(Num(im.knockback));
                }
                if (action is ImpactAction.StatusEffect or ImpactAction.CoatWeapon
                           or ImpactAction.ToggleAura or ImpactAction.GrantInstantCast)
                {
                    if (customEffect) SingleComposer.GetTextInput("f_ieffectid").SetValue(im.effectId ?? "");
                    SingleComposer.GetNumberInput("f_isec").SetValue(Num(im.seconds));
                    SingleComposer.GetNumberInput("f_isecombo").SetValue(Num(im.secondsPerComboPoint));
                    SingleComposer.GetNumberInput("f_iamp").SetValue(im.amplifier.ToString());
                    SingleComposer.GetNumberInput("f_iampcap").SetValue(im.amplifierCap.ToString());
                }
                else if (IsControl(action))
                {
                    SingleComposer.GetNumberInput("f_isec").SetValue(Num(im.seconds));
                }
                if (action == ImpactAction.Shield)
                {
                    SingleComposer.GetNumberInput("f_ishield").SetValue(Num(im.shieldCoefficient));
                    SingleComposer.GetNumberInput("f_ishieldsec").SetValue(Num(im.shieldSeconds));
                }
                if (action is ImpactAction.Teleport or ImpactAction.Dash)
                    SingleComposer.GetNumberInput("f_itpdist").SetValue(Num(im.teleportDistance));
                if (action == ImpactAction.DamageZone)
                {
                    SingleComposer.GetNumberInput("f_izsec").SetValue(Num(im.zoneSeconds));
                    SingleComposer.GetNumberInput("f_iztick").SetValue(Num(im.zoneTickSeconds));
                    SingleComposer.GetNumberInput("f_izrad").SetValue(Num(im.zoneRadius));
                }
                if (action == ImpactAction.GainResource)
                    SingleComposer.GetNumberInput("f_igain").SetValue(Num(im.resourceGain));
                if (action is ImpactAction.Aggro or ImpactAction.Reveal)
                    SingleComposer.GetNumberInput("f_iaggro").SetValue(Num(im.aggroRange));
                if (action == ImpactAction.Cleanse)
                    SingleComposer.GetNumberInput("f_icleanse").SetValue(im.cleanseMax.ToString());
            }
        }

        private static string Num(float f) => f.ToString(CultureInfo.InvariantCulture);
        private static string? Blank(string v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        private static float ParseFloat(string text, float fallback)
            => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

        private static int ParseInt(string text, int fallback) => int.TryParse(text, out int v) ? v : fallback;

        private void OnTabClicked(int index)
        {
            tab = (Tab)index;
            Compose();
        }

        private bool SelectSpell(string id)
        {
            var spell = Mod?.Spells.Get(id);
            if (spell != null && !IsAuthored(spell))
            {
                draft = null;
                editingId = null;
                lockedSelection = id;
                status = $"'{id}' is defined in code, so it can't be edited here - its behaviour is C#. "
                       + "Copy it to build your own version of it.";
                Compose();
                return true;
            }

            lockedSelection = null;
            Edit(id);
            Compose();
            return true;
        }

        private bool NewSpell()
        {
            draft = new SpellModel
            {
                id = NextId(),
                name = "New spell",
                school = SpellSchool.PhysicalMelee.ToString(),
                castMode = CastMode.Instant.ToString(),
                range = 8f,
                cooldown = 6f,
                target = new TargetModel { type = TargetType.Aim.ToString(), affinity = TargetAffinity.Enemy.ToString() },
                delivery = new DeliveryModel { type = DeliveryType.Direct.ToString() },
                impacts = new List<ImpactModel> { new() { action = ImpactAction.Damage.ToString(), coefficient = 1f } }
            };
            editingId = null;
            lockedSelection = null;
            selImpact = 0;
            status = "";
            tab = Tab.Basics;
            Compose();
            return true;
        }

        /// <summary>Copies a registered spell - code spell included - into a fresh draft. Impacts come across as
        /// data, which is what makes "the same skill under another name with other numbers" a copy, not a rewrite.
        /// </summary>
        private bool CloneLocked()
        {
            var s = lockedSelection == null ? null : Mod?.Spells.Get(lockedSelection);
            if (s == null) return true;

            draft = new SpellModel
            {
                id = NextId(),
                name = s.DisplayName + " (copy)",
                description = s.Description,
                icon = s.IconName,
                school = s.School.ToString(),
                tier = s.Tier,
                range = s.Range,
                castMode = s.CastMode.ToString(),
                castSeconds = s.CastDuration,
                channelTicks = s.ChannelTicks,
                triggersGlobalCooldown = s.TriggersGlobalCooldown,
                resourceCost = s.Cost.Resource,
                cooldown = s.Cost.Cooldown.Duration,
                cooldownGroup = s.Cost.Cooldown.Group,
                comboBuilder = s.ComboBuilder,
                comboFinisher = s.ComboFinisher,
                comboPointsGenerated = s.ComboPointsGenerated,
                target = new TargetModel
                {
                    type = s.Target.Type.ToString(),
                    affinity = s.Target.Affinity.ToString(),
                    includeCaster = s.Target.AreaIncludeCaster
                },
                delivery = new DeliveryModel
                {
                    type = s.Deliver.Type.ToString(),
                    velocity = s.Deliver.ProjectileVelocity,
                    count = s.Deliver.ProjectileCount,
                    spreadDegrees = s.Deliver.ProjectileSpreadDegrees,
                    entity = s.Deliver.ProjectileEntity
                },
                requires = new RequiresModel
                {
                    bow = s.RequiresBow,
                    shield = s.RequiresShield,
                    outOfCombat = s.RequiresOutOfCombat,
                    form = s.RequiresForm
                },
                impacts = s.Impacts.Select(ToModel).ToList()
            };
            editingId = null;
            lockedSelection = null;
            selImpact = draft.impacts.Count > 0 ? 0 : -1;
            status = "Copied. Rename it, retune the numbers, then save.";
            tab = Tab.Basics;
            Compose();
            return true;
        }

        private static ImpactModel ToModel(SpellImpact i) => new()
        {
            action = i.Action.ToString(),
            chance = i.Chance,
            affectCaster = i.AffectCaster,
            coefficient = i.Action == ImpactAction.Heal ? i.HealSpellPowerCoefficient : i.DamageSpellPowerCoefficient,
            perComboPoint = i.Action == ImpactAction.Heal ? i.HealPerComboPoint : i.DamagePerComboPoint,
            knockback = i.Knockback,
            missingHealthFraction = i.Action == ImpactAction.Heal
                ? i.HealMissingHealthFraction : i.DamageMissingHealthCoefficient,
            effectId = i.StatusEffectId,
            seconds = i.StatusEffectDuration,
            secondsPerComboPoint = i.DurationPerComboPoint,
            amplifier = i.StatusEffectAmplifier,
            amplifierCap = i.StatusEffectAmplifierCap,
            applyMode = i.StatusEffectApplyMode.ToString(),
            shieldCoefficient = i.ShieldSpellPowerCoefficient,
            shieldSeconds = i.ShieldDurationSeconds,
            teleportDistance = i.TeleportDistance,
            teleportMode = i.TeleportMode.ToString(),
            resourceGain = i.ResourceGainAmount,
            aggroRange = i.AggroRange,
            cleanseMax = i.CleanseMax,
            zoneSeconds = i.ZoneDurationSeconds,
            zoneTickSeconds = i.ZoneTickSeconds,
            zoneRadius = i.ZoneRadius,
            zoneAtAimPoint = i.ZoneAtAimPoint,
            particles = i.Particles != null ? "school" : null
        };

        private string NextId()
        {
            for (int i = 1; i < 500; i++)
            {
                string candidate = $"{canrpgclassesModSystem.ModId}:custom{i}";
                if (Mod?.Spells.Get(candidate) == null) return candidate;
            }
            return canrpgclassesModSystem.ModId + ":custom";
        }

        private bool AddImpact()
        {
            var list = draft!.impacts ??= new List<ImpactModel>();
            if (list.Count >= ContentEditGuard.MaxImpacts)
            {
                status = $"A spell can have at most {ContentEditGuard.MaxImpacts} impacts.";
                Compose();
                return true;
            }
            list.Add(new ImpactModel { action = ImpactAction.Damage.ToString(), coefficient = 1f });
            selImpact = list.Count - 1;
            Compose();
            return true;
        }

        private bool RemoveImpact(int index)
        {
            var list = draft!.impacts;
            if (list == null || index >= list.Count) return true;
            list.RemoveAt(index);
            selImpact = Math.Min(selImpact, list.Count - 1);
            Compose();
            return true;
        }

        private bool SaveSpell()
        {
            var mod = Mod;
            if (mod?.ClientChannel == null || draft == null) return true;

            string? refused = ContentEditGuard.RejectSpell(draft, mod.Spells);
            if (refused != null) { status = "Not sent: " + refused; Compose(); return true; }

            // A renamed spell is a new record; drop the old one so both don't sit in the store.
            if (editingId != null && !string.Equals(editingId, draft.id, StringComparison.OrdinalIgnoreCase))
                mod.ClientChannel.SendPacket(new SpellEditPacket { Delete = true, SpellId = editingId });

            mod.ClientChannel.SendPacket(new SpellEditPacket
            {
                SpellJson = Newtonsoft.Json.JsonConvert.SerializeObject(draft)
            });

            editingId = draft.id;
            status = "Sent.";
            awaitingSync = true;
            Compose();
            return true;
        }

        private bool DeleteSpell()
        {
            var mod = Mod;
            if (mod?.ClientChannel == null || editingId == null) return true;

            mod.ClientChannel.SendPacket(new SpellEditPacket { Delete = true, SpellId = editingId });
            draft = null;
            editingId = null;
            status = "Sent.";
            awaitingSync = true;
            Compose();
            return true;
        }

        /// <summary>The server applied a content edit and the registries were rebuilt.</summary>
        public void OnContentChanged()
        {
            if (!IsOpened()) return;
            if (!awaitingSync) { Compose(); return; }
            awaitingSync = false;

            string? expected = editingId;
            if (expected == null)
                status = "";
            else if (Mod?.Spells.Get(expected) is ContentSpell)
            {
                Edit(expected);
                status = $"Saved. '{expected}' can now be given by a talent or a class.";
            }
            else
            {
                status = $"The server didn't register '{expected}' - see the chat line above and the server log.";
            }

            Compose();
        }
    }
}
