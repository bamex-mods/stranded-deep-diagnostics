# Stranded Deep Diagnostics — памятка пользователя

Эта памятка относится к **Stranded Deep Diagnostics 1.0.0-rc1**.

Diagnostics — инструмент для разработчиков модов и технической диагностики Stranded Deep. Он показывает состояние игровых объектов, физики, игроков, камер, ввода, UI, хранилищ, мира, строительства и подключённых BepInEx-плагинов, умеет делать снимки состояния и включать точечную трассировку.

Инструмент проектировался как **inspection-first**: обычный просмотр и отчёты не должны изменять игровой мир, сохранение, Transform, Rigidbody или collision state. Трассировка через Harmony включается только вручную там, где она поддерживается.

## 1. Быстрый старт

1. Запустите Stranded Deep и загрузите мир.
2. Нажмите `F8` — появится диагностический overlay.
3. В local split-screen используйте `Alt+F8`, чтобы выбрать P1 или P2.
4. Нажимайте `F9`, чтобы переключать диагностические модули.
5. Наведитесь центром камеры на нужный объект.
6. При необходимости нажмите `F10`, чтобы закрепить текущую цель.
7. Нажмите `F12`, чтобы сохранить отчёт текущего модуля.
8. Готовые отчёты по умолчанию находятся в:

```text
BepInEx/config/StrandedDeepDiagnostics/Reports/
```

Папка `Reports` создаётся при первой фактической записи отчёта.

## 2. Горячие клавиши

```text
F8          Diagnostics ON/OFF
Alt+F8      переключить активного локального игрока P1/P2
F9          следующий модуль
F10         закрепить / открепить текущую цель
F11         трассировка или recorder текущего модуля, если поддерживается
F12         отчёт / snapshot / incident marker в зависимости от модуля
Shift+F12   diff или специальный status report, если поддерживается
Ctrl+F12    deep safe-field dump в OBJECT / PHYSICS
```

При `F8 OFF` активные trace/recorder режимы AUDIO и RAFT выключаются. Они также сбрасываются при смене/выгрузке сцены или мира.

## 3. Как выбирается объект

Diagnostics использует камеру выбранного игрока, а не глобальный `Camera.main`.

В overlay можно увидеть несколько уровней цели:

```text
RAW#0
PICK
PRIMARY
```

`RAW#0` — первый физический Raycast hit.

`PICK` — цель после фильтрации служебных попаданий. Например, собственный Player или connector helper может быть пропущен, если за ним есть более полезный объект.

`PRIMARY` — объект, который Diagnostics считает основной семантической целью для инспекции.

`F10` закрепляет текущую цель. После этого можно отвернуть камеру и продолжить изучение того же объекта. Повторный `F10` открепляет её.

При переключении активного игрока через `Alt+F8` текущая и закреплённая цели сбрасываются.

## 4. CAPS: AVAILABLE / PARTIAL / UNAVAILABLE

В верхней части overlay показывается состояние capability активного модуля:

```text
AVAILABLE
PARTIAL
UNAVAILABLE
```

Это не оценка исправности игры.

- `AVAILABLE` — основные runtime-типы и точки инспекции найдены.
- `PARTIAL` — часть возможностей доступна, часть не была найдена или зависит от текущего runtime-состояния.
- `UNAVAILABLE` — нужные типы/методы в текущей среде не найдены.

Отсутствующий optional adapter не должен ломать остальные модули Diagnostics.

## 5. Модули

### OBJECT

Общая инспекция объекта под прицелом:

- hierarchy;
- components;
- runtime identity;
- ReferenceId и другие доступные saveable identifiers;
- безопасное чтение полей;
- RAW/PICK/PRIMARY target context.

`F12` — snapshot.

`Shift+F12` — snapshot + diff с предыдущим снимком. При первом нажатии создаётся baseline; повторите после изменения состояния.

`Ctrl+F12` — deep safe-field dump.

### PHYSICS

Показывает Rigidbody, Collider и физическую структуру выбранной цели, включая velocity/angularVelocity, kinematic/gravity/constraints, bounds и связи collider ↔ Rigidbody.

`F12` — physics snapshot.

`Shift+F12` — diff с предыдущим snapshot.

`Ctrl+F12` — более глубокий safe-field dump.

Diagnostics не вызывает `Physics.SyncTransforms()` и не меняет Rigidbody.

### CAMERA

Отчёт по камере выбранного P1/P2:

- viewport;
- camera hierarchy;
- enabled state;
- projection/FOV и другие доступные camera данные.

`F12` — camera report.

### INPUT

Показывает привязку выбранного игрока к Rewired/controller/joystick и доступное состояние ввода.

`F12` — input report.

### UI

Census Unity Canvas/UI, полезный для split-screen и поиска конкретного Player UI.

`F12` — UI Canvas report.

### INTERACTION

Инспекция доступного interaction state выбранной цели.

`F11` — targeted interaction trace.

`F12` — текущий interaction report.

`Shift+F12` — текстовый diff между двумя interaction reports.

### SAVEABLE

Инспекция saveable/native identity выбранного объекта.

`F11` — targeted saveable trace.

`F12` — saveable report.

`Shift+F12` — text diff.

### STORAGE

Инспекция `SlotStorage` / `Interactive_STORAGE` и связанных storage-состояний, если они доступны у цели.

`F11` — targeted storage trace.

`F12` — storage report.

`Shift+F12` — text diff.

### INVENTORY

Инспекция player holder/inventory context и выбранной цели.

`F11` — targeted inventory trace.

`F12` — inventory report.

`Shift+F12` — text diff.

### WORLD

Состояние мира/зон и доступных world-level объектов.

`F11` — targeted world trace.

`F12` — world/zones report.

`Shift+F12` — text diff.

### CONSTRUCTION

Инспекция строительства, construction state и raft-related construction context.

`F11` — targeted construction trace.

`F12` — construction report.

`Shift+F12` — text diff.

### AUDIO

Аудиодиагностика Unity и optional adapter для BamEx Boombox.

Если найден `SplitStereoProcessor`, `F11` включает специальную DSP telemetry для `OnAudioFilterRead` без обычного логирования/reflection внутри audio callback.

`F12` ставит **incident marker**. Diagnostics сохраняет окно примерно 5 секунд до метки и 5 секунд после неё, после чего автоматически создаёт `audio-incident` report.

Практический сценарий:

```text
AUDIO
→ F11
→ воспроизвести проблему
→ в момент слышимого сбоя F12
→ подождать около 5 секунд
→ открыть свежий audio-incident report
```

Если optional Boombox adapter отсутствует, остальные модули Diagnostics продолжают работать.

### RAFT

Composite-модуль для диагностики плота. Использует общие OBJECT/PHYSICS/CONSTRUCTION/event primitives и ведёт bounded physics history для найденных raft structures.

`F11` — включить/выключить read-only raft recorder.

`F12` — incident marker с окном примерно 5 секунд до и после события.

`Shift+F12` — немедленный current RAFT status report без ожидания post-window.

Модуль может отмечать evidence вроде:

```text
RAFT_TILT
RAFT_ANGULAR_SPIKE
RAFT_LINEAR_SPIKE
RAFT_POSITION_JUMP
RAFT_RB_STATE_CHANGED
RAFT_CENTER_OF_MASS_CHANGED
RAFT_COLLIDER_SET_CHANGED
RAFT_CHILD_RB_CHANGED
```

Это диагностические признаки, а не автоматический вывод о причине бага.

Optional BamEx Raft Furniture adapter добавляет read-only сведения об attachment/follower state, если соответствующий мод установлен.

### TRACE

Общий отчёт по targeted Harmony trace events.

`F11` — включить/выключить TRACE profile.

`F12` — выгрузить текущие trace events.

`Shift+F12` — text diff trace report.

### PLUGINS

Показывает загруженные BepInEx plugins и состояние optional adapters.

`F12` — полный plugin/adapters report.

`Shift+F12` — text diff.

## 6. Где F11 реально поддерживается

Обычный targeted trace profile существует для:

```text
INTERACTION
SAVEABLE
STORAGE
INVENTORY
WORLD
CONSTRUCTION
TRACE
```

У `AUDIO` и `RAFT` собственная логика `F11`.

В модулях вроде `OBJECT`, `PHYSICS`, `CAMERA`, `INPUT`, `UI` и `PLUGINS` нажатие `F11` может показать:

```text
No targeted trace profile for <MODULE>
```

Это нормальное поведение.

## 7. Snapshot и diff

Для `OBJECT` / `PHYSICS`:

```text
Shift+F12
→ если baseline ещё нет: сохранить baseline
→ изменить/дождаться изменения состояния
→ Shift+F12 ещё раз
→ получить diff
```

Для большинства текстовых модулей `Shift+F12` работает аналогично, но сравнивает их текстовые reports.

Runtime `InstanceID` может изменяться после unload/reload и сам по себе не является persistent identity. Для persistence важнее проверять доступные `ReferenceId`/saveable identifiers и соответствующий контекст.

## 8. Отчёты

Default:

```text
BepInEx/config/StrandedDeepDiagnostics/Reports/
```

Внутри создаются session folders вида:

```text
<timestamp>-v1.0.0-rc1/
```

Каждая session содержит `session.txt` с capability manifest. Остальные файлы создаются при `F12`, incident capture и diff.

Путь можно изменить в:

```text
BepInEx/config/com.bamex.strandeddeep.diagnostics.cfg
```

параметром:

```ini
[Reports]
ReportRoot =
```

Пустое значение означает portable default.

## 9. Config

Основные настройки BepInEx:

```ini
[Input]
ToggleKey = F8

[Inspection]
MaxRayDistance = 100
MaxHierarchyDepth = 32
MaxFieldsPerComponent = 48

[Reports]
ReportRoot =
```

Не увеличивайте лимиты reflection без необходимости: они специально bounded, чтобы отчёты и runtime inspection оставались контролируемыми.

## 10. Полезные сценарии

### «Почему я не могу поставить объект?»

```text
F8
→ выбрать P1/P2
→ CONSTRUCTION
→ навести камеру на ghost / target area
→ F12
→ при необходимости F11 для targeted trace
```

### «Что физически происходит с этим объектом?»

```text
OBJECT / PHYSICS
→ F10 pin
→ Shift+F12 baseline
→ выполнить действие
→ Shift+F12 diff
```

### «Какой контроллер реально относится к P1/P2?»

```text
Alt+F8 выбрать игрока
→ INPUT
→ F12
```

### «Какой мод или adapter реально загружен?»

```text
PLUGINS
→ F12
```

### «Плот внезапно дёрнулся или накренился»

```text
RAFT
→ F11 заранее
→ играть как обычно
→ при подозрительном событии F12
→ подождать ~5 секунд
→ открыть raft-incident report
```

### «В аудио был короткий провал»

```text
AUDIO
→ F11, если DSP adapter AVAILABLE
→ дождаться сбоя
→ F12
→ подождать ~5 секунд
→ открыть audio-incident report
```

## 11. Что отправлять разработчику при баге

Минимальный набор:

```text
BepInEx/LogOutput.log
+
свежая session folder из StrandedDeepDiagnostics/Reports
```

Если проблема связана с AUDIO или RAFT, желательно приложить соответствующий `audio-incident` или `raft-incident` report.

Не нужно присылать игровой save, если его специально не запросили.

## 12. Troubleshooting

### Overlay не появляется

Проверьте, что BepInEx загрузил `StrandedDeepDiagnostics.dll`, затем посмотрите `BepInEx/LogOutput.log`.

### `PRIMARY <none>` / нет цели

Наведитесь центром gameplay camera на объект в пределах `MaxRayDistance`. В split-screen проверьте правильного игрока через `Alt+F8`.

### Capability = PARTIAL / UNAVAILABLE

Сделайте `PLUGINS → F12` и report проблемного модуля. Это может означать изменение игрового API, отсутствие optional adapter или просто отсутствие нужного runtime-объекта в текущей сцене.

### Папки Reports ещё нет

Она создаётся при первой фактической записи report. Нажмите `F12` в поддерживаемом модуле.

### F11 ничего не включает

Не каждый модуль имеет trace profile. Смотрите раздел «Где F11 реально поддерживается» выше.

## 13. Граница безопасности

Diagnostics предназначен для наблюдения и targeted instrumentation. Публичная линия не должна намеренно:

```text
вызывать SaveGame
создавать gameplay objects
перемещать Transform
записывать Rigidbody velocity/angularVelocity
менять isKinematic/useGravity/constraints
вызывать Physics.SyncTransforms()
менять IgnoreCollision
форсировать placement validity
чинить attachments
переписывать ReferenceId
изменять world sidecars
```

Если диагностическая возможность когда-либо потребует mutating experiment, она должна быть отдельной, явно обозначенной и выключенной по умолчанию; в `1.0.0-rc1` такого публичного режима нет.

## 14. Версия и совместимость

`1.0.0-rc1` — release candidate. Diagnostics использует runtime capability detection, поэтому отсутствие конкретного game type должно по возможности превращаться в `PARTIAL/UNAVAILABLE`, а не в crash.

Тем не менее Stranded Deep и его внутренние классы могут меняться между версиями. При несовпадении сначала приложите capability manifest (`session.txt`) и `LogOutput.log`.

## 15. Для разработчиков

Архитектура описана в `ARCHITECTURE.md`.

Optional adapters — в `ADAPTERS.md`.

Сборка из исходников — в `README.md`.

Проект распространяется под MIT License.
