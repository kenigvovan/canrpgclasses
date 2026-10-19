# План: maxLevel 50, очко раз в N уровней, черты (traits) на вехах

## Context
Игроки быстро упираются в кап 25 (локусты, разломы). `maxLevel` уже настраивается в `config/core.json`, но:
- «очко через уровень» невозможно: `talentPointsPerLevel` приводится к int;
- прирост атрибутов, HP и HP пета за уровень линеен, поэтому на 50-м уровне персонаж был бы вдвое сильнее;
- черт за уровни нет.

Решения:
1. Скейлинг выбирается в конфиге: линейный или нормализованный.
2. Новый global `levelsPerTalentPoint`.
3. Черты: на вехе игрок выбирает 1 из 2-3, систему можно выключить в конфиге.

## Фаза 1. Конфиг и скейлинг

**`assets/canrpgclasses/config/core.json`**
- Новые ключи: `levelsPerTalentPoint: 1`, `levelScalingReferenceMax: 0` (0 = линейно, 25 = на капе сила как сейчас на 25), `traitsEnabled: 1`.
- Ключи прописать явно, иначе их не будет в админ-редакторе Globals.
- В `_comment` коротко описать эти ключи и `xpCurveBase`/`xpCurveExponent`.

**`src/Core/Progression/EBProgression.cs`**
- `LevelsPerTalentPoint = Max(1, (int)Global(...))`.
- `EffectiveLevel = Min(Level, MaxLevel)`.
- `PointsAt(level) = (Max(0, level-1) / LevelsPerTalentPoint) * PointsPerLevel`; `TotalTalentPoints = PointsAt(EffectiveLevel)`.
- `static float ScaledLevels(int level)`: `lv = Max(0, Min(level, MaxLevel) - 1)`. При `ref <= 1` вернуть `lv`, иначе `lv * (ref-1)/(MaxLevel-1)`.

**Все места с `level - 1` перевести на `ScaledLevels`:**
- `RpgAttributes.ClassAttributes.Granted`: параметр становится float;
- `AttributeStats.cs:22`;
- `EBTalents.cs:187` (`HpPerLevel`, `StatsPerLevel`);
- `HunterPetSystem.cs:102`;
- `EBRpgStats.cs:29`;
- `src/Client/TalentBonusSummary.cs` (~173, ~196), чтобы UI совпадал с сервером;
- `StunPatches.InnateMagicResist` уже нормализован, его не трогать.

**Прочее**
- Обновить комментарий `TalentState.PointsPerTier`.
- **Перерасход очков** (понижен кап или вырос `levelsPerTalentPoint`): новый `EBTalents.EnforcePointBudget()` на сервере. При `AvailablePoints < 0` делает `Respec()` и пишет в чат. Вызывается в `RecalcLevel`, `SetLevel` и при входе игрока.

## Фаза 2. Черты: данные и сервер

**`assets/canrpgclasses/config/traits.json`**
- `traits{id: {icon, stats:[{stat,value}], gear?:{weaponTools, weaponCodeContains, armorCodeContains, armorAbsent}}}`.
- `default{"5":[...], "10":[...]}` плюс переопределения `classes{cls:{"15":[...]}}`.
- Для каждой вехи берётся список класса, если он есть, иначе default. Вехи выше `MaxLevel` недостижимы.
- В `BalanceConfig.Load` (`BalanceConfig.cs:141`) пропускать `traits*`, как `attributes`.

**`src/Core/Traits/`**
- `TraitDef`:
  - поля: Id, Icon, Stats, `GearAffinity? Condition`;
  - lang `trait-<id>` и `traitdesc-<id>`;
  - источник стата `canrpgtrait_<id>`.
- `TraitRegistry`:
  - Load, Get, `Options(cls, milestone)`, `Milestones(cls)`, `StalePairs`;
  - загружать по образцу `RpgAttributes.Load`, вызывать в `canrpgclasses.cs` (~433) и в reload-команде;
  - `gear` мапить в существующий `GearAffinity`.
- `TraitState` (общий для клиента и сервера, как `TalentState`):
  - `Enabled`;
  - `Picked`/`ActivePicks`: активен, только если Enabled, веха ≤ EffectiveLevel и id всё ещё в Options;
  - `OpenMilestones`;
  - `CanPick(e, milestone, id, out reason)`.

**Хранение**
- `AttrKeys.Traits(classId)`: дерево «веха → id», по классам, как Level.
- Не в `RanksKey`: `SpentPoints` суммирует эту таблицу, и выбор черты съел бы очки.

**`EBTalents`**
- В `Initialize`: listener на `AttrKeys.Traits(*)`, который вызывает `ApplyStatTalents`.
- `TryPickTrait`: `CanPick`, set, `MarkPathDirty`, `ApplyStatTalents`, затем `EBGearAffinity.Refresh`.
- `RespecTraits`: вызывать из `OnTalentRespec` и `/canrpgrespec`. Не из `Respec()`, иначе смена класса и загрузка билда стирали бы черты.
- В `ApplyStatTalentsCore` перед `ClearForeignClassStats`: безусловные черты set, если пик активен, иначе remove; затем снять `StalePairs`. Выключение, смена класса и понижение уровня так снимают статы, а выборы сохраняются.

**Условные черты (броня/оружие)**
- В `EBGearAffinity.Reevaluate`: `match = picked && Condition.Matches(...)`, по образцу `ApplyAffinities`.

**Сеть**
- `TraitPickPacket{Milestone, TraitId}` в `TalentPackets.cs`.
- Регистрация в конце `RegisterMessageTypes`: порядок должен совпадать на обеих сторонах.
- Обработчик `OnTraitPick` вызывает `TryPickTrait`.

**Уведомления и отладка**
- В `RecalcLevel`/`SetLevel`: если в диапазоне (old, new] есть веха, сообщение `msg-trait-available`.
- Debug-команда `/canrpgtrait <level> <id>`.

## Фаза 3. Клиентский UI
- Новый `src/Client/TraitPickDialog.cs`:
  - строка на каждую веху: «Уровень N» и кнопки опций;
  - выбранная опция подсвечена, недоступные серые с reason;
  - клик отправляет пакет;
  - пересборка по сигнатуре дерева черт.
- `TalentTreeDialog`: кнопка «Черты (N)» рядом с очками, только если `Enabled` и вехи есть. Экземпляр диалога держать в `canrpgclasses.ClientGui.cs`.
- `TalentBonusSummary.Build`: строки активных черт.
- Потом: секция в листе персонажа и вкладка в редакторе класса. Сейчас только JSON.

## Фаза 4. Контент и lang
- Общий пул на вехах 5, 10, …, 45, по 2-3 опции:
  - HP;
  - скорость;
  - сопротивление магии;
  - «Бронированный» (условие: металлическая броня);
  - «Мастер клинка» (weaponTools);
  - «Мастер лука».
- 1-2 классовых переопределения для примера.
- Статы брать только из `StatKeys`/`StatCatalog`.
- Lang en + ru: `trait-*`, `traitdesc-*`, `ui-traits-*`, `trait-reason-*`, `msg-trait-available`, `msg-talents-reset-budget`. Без em-dash, только " - ".

## Краевые случаи
- `maxLevel` ниже текущего уровня: всё ограничено `EffectiveLevel`, перерасход закрывает бюджетный сброс.
- Опция удалена из JSON: выбор хранится, но неактивен, UI даёт перевыбрать.
- `traitsEnabled=0`: статы сняты, UI скрыт, выборы сохранены.
- `ApplyLoadout` черты не трогает.

## Проверка (сборка: effectshud, затем canrpgclasses)
1. `maxLevel` 50, `levelsPerTalentPoint` 2: `/canrpglevel 50` даёт 24 очка, `/canrpglevel 3` даёт 1.
2. `levelScalingReferenceMax` 25: на 50-м уровне HP и атрибуты равны прежним на 25-м. При 0 примерно вдвое выше. Питомец охотника тоже (призвать заново).
3. Потратить 24 очка, `/canrpglevel 20`: бюджетный сброс и сообщение в чат.
4. XP через уровень 5: сообщение в чате и счётчик на кнопке в окне талантов.
5. Выбрать черту: стат появился, повторный выбор отклонён. Условная черта включается и выключается при смене брони.
6. `/canrpgrespec` сбрасывает черты; loadout их не трогает; при смене класса выборы сохраняются.
7. `traitsEnabled` 0/1 в админ-редакторе снимает и возвращает бонусы.
8. Мультиплеер: клиентская сводка совпадает с сервером.
