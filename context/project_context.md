# PROJECT CONTEXT: EasyKeep (Умный складской учёт)

## ПРОТОКОЛ ПЕРЕДАЧИ ПАМЯТИ И GIT-ДИСЦИПЛИНЫ (ОБЯЗАТЕЛЬНО ДЛЯ ОБОИХ АГЕНТОВ)
1. **Где хранить память:** Единственный источник правды о состоянии проекта — этот файл (`project_context.md`). Не создавай других логов или заметок.
2. **Изучение шаблона и видения (ПЕРВЫЙ ПРИОРИТЕТ):**
   - Обязательно прочитай `project.md` в корне репозитория — там описаны продуктовая логика и все уникальные фичи EasyKeep.
   - Проект развернут из учебного шаблона. Перед созданием или изменением любых файлов ты ОБЯЗАН проверить структуру всех папок, прочитать корневой `README.md`, все локальные `README.md` внутри подкаталогов и существующие файлы-шаблоны. Строго придерживайся заданной в них структуры, имен файлов и форматов оформления.
3. **Жесткий Git-протокол (Атомарные коммиты по микрошагам):**
   - ЗАПРЕЩЕНО копить изменения и делать один огромный коммит за всю сессию.
   - Выполняй работу строго маленькими логическими шагами (1 шаг = 1 законченная микрозадача: например, создана схема БД, написаны сущности EF Core, реализован репозиторий транзакций, сверстан один экран).
   - Сразу после завершения каждого микрошага (и проверки, что код собирается без ошибок) ты ОБЯЗАН:
     1) Обновить этот файл (`project_context.md`).
     2) Выполнить `git add .` и сделать понятный коммит в формате:
        `<тип>: <краткое и понятное описание на русском, что именно сделано>`
        Допустимые типы: `feat` (новый функционал), `db` (скрипты БД), `fix` (исправление бага), `refactor` (улучшение архитектуры), `docs` (документация и ER-схема).
     3) Записать хэш и название этого коммита в блок «Статус последней сессии».
4. **Как обновлять Часть 3 (Алгоритм перезаписи):**
   - Блок **«Статус последней сессии»** и **«ТОЧНЫЙ СЛЕДУЮЩИЙ ШАГ»** — **ПЕРЕЗАПИСЫВАЙ** целиком актуальными данными.
   - Блок **«Контракты и сигнатуры»** — **ДОПОЛНЯЙ** новыми готовыми классами и методами, не удаляя старые рабочие.
   - Указывай только сухие факты: путь к файлу, сигнатуры публичных методов и статус.
   - Если столкнулся с ошибкой, которую не успел исправить — запиши текст ошибки и файл в блок **«Известные баги»** (не коммить сломанный код!).

---

## ЧАСТЬ 1. КОНСТИТУЦИЯ ПРОЕКТА (НЕ ИЗМЕНЯТЬ, ТОЛЬКО ЧИТАТЬ)
- **Стек:** C# (.NET 10), WPF, MS SQL Server, **EF Core (`Microsoft.EntityFrameworkCore.SqlServer`) + ADO.NET (`Microsoft.Data.SqlClient`)**.
- **Принцип максимальной простоты и читаемости кода (KISS — важно для защиты):**
  1. Пиши прямой, линейный и максимально понятный код без оверинжиниринга (без сложных DI-контейнеров, рефлексии, лишних фабрик и многоуровневых абстракций).
  2. Используй понятные «говорящие» имена классов, методов и переменных.
  3. Разбивай сложные методы на короткие логические шаги (1 метод = 1 задача, не более 25–30 строк).
  4. Добавляй краткие, осмысленные комментарии на русском языке к нетривиальной логике (в транзакциях ADO.NET, расчёте прогноза дефицита и авто-балансировке).
- **Архитектура слоев:** `UI (WPF Views/Code-Behind)` -> `Services (Бизнес-логика)` -> `Data (EF Core Context + ADO.NET Repository)`. Прямые запросы к БД из UI запрещены.
- **Гибридная модель доступа к данным (строго по ТЗ на оценку «5»):**
  1. **EF Core (`EasyKeepContext`):** Используется для справочников (`Categories`, `Items`, `Warehouses`), быстрых выборок без отслеживания (`.AsNoTracking()`), фильтрации, проекций (`.Select()`), расчета «Светофора дефицита» и ленты «Рентген товара» (`.Include()`, `.GroupBy()`).
  2. **Чистый ADO.NET (`AdoNetMovementRepository`):** Обязателен по ТЗ! Используется для проведения документов движения (`IN`, `OUT`, `TRANSFER`) в единой транзакции `SqlTransaction` с блокировкой строки (`WITH (UPDLOCK, ROWLOCK)`), проверкой `quantity >= @qty` и `Rollback` при ошибке, а также для сложного аналитического отчета с `JOIN` и агрегацией.
- **База данных (6 таблиц):**
  1. `categories` (`id`, `name`)
  2. `items` (`id`, `sku`, `name`, `category_id`, `unit`, `price`, `min_stock`)
  3. `warehouses` (`id`, `name`, `address`)
  4. `stocks` (`id`, `warehouse_id`, `item_id`, `quantity` — уникальный индекс по `(warehouse_id, item_id)`, `CHECK (quantity >= 0)`)
  5. `movements` (`id`, `type` ['IN', 'OUT', 'TRANSFER'], `from_warehouse_id`, `to_warehouse_id`, `created_at`, `comment`)
  6. `movement_items` (`id`, `movement_id`, `item_id`, `quantity` — `CHECK (quantity > 0)`)
- **Уникальные фичи продукта (из `project.md`):**
  - **«Светофор дефицита»:** Расчет среднего расхода за 14–30 дней и прогноз дней до обнуления (🟢 >10 дн., 🟡 3–5 дн., 🔴 <=2 дн. или ниже `min_stock`, ⚪ 0 расхода за 30 дн.).
  - **«Авто-балансировка складов»:** Кнопка автосбора черновика перемещения дефицитных товаров с основного склада на торговую точку.
  - **«Командная строка склада»:** Поле ввода SKU/названия в постоянном фокусе (работа с клавиатуры/сканера по `Enter` добавляет позицию `+1`).
  - **«Рентген товара»:** Боковая панель истории всех движений выбранного товара при клике по строке.
  - **Умная валидация:** При попытке списать/переместить больше остатка — подсказка с предложением подставить доступный максимум.
- **Обязательные требования UI, безопасности и документации из ТЗ:**
  1. **UI и UX:** Подтверждение необратимых операций (удаление записей, проведение списания/перемещения); явные сообщения об отсутствии результатов при поиске/фильтрации; корректная валидация полей.
  2. **Безопасность:** В ADO.NET все SQL-запросы строго параметризованные (`SqlCommand.Parameters.AddWithValue` или `.Add`). Реальную строку подключения хранить в `appsettings.Local.json` или `.env` (в `.gitignore`). При ошибках подключения к БД выводить понятное сообщение пользователю без раскрытия паролей и деталей строки подключения.
  3. **Документация:** В `docs/testing.md` оформить 5–8 тест-кейсов (ID, начальные условия, действия, ожидаемый/фактический результат, статус), из них минимум 2 негативных (нехватка остатка с откатом транзакции, некорректный ввод/пустая накладная).

---

## ЧАСТЬ 2. ЧЕК-ЛИСТ ГОТОВНОСТИ ПО ТЗ
- [x] Изучены `project.md`, все шаблоны и `README.md` в папках репозитория
- [x] `database/schema.sql` и `database/seed.sql` (6 таблиц, `sku`, `min_stock`, тестовые + негативные данные + история за 30 дней для прогноза)
- [x] Слой `Data`: сущности + `EasyKeepContext` (EF Core) и `AdoNetMovementRepository` (чистый ADO.NET с `SqlTransaction`, блокировкой `UPDLOCK` и `JOIN`-отчетами)
- [x] Слой `Services`: логика «Светофора дефицита», «Авто-балансировки складов» и валидации остатков
- [x] Главное окно WPF + экраны: Каталог (с «Рентгеном товара»), Остатки (со «Светофором»), Движения (с «Командной строкой» и «Авто-балансировкой»), Отчёт (остатки по складам + топ активных товаров)
- [x] Поиск, фильтрация, умная валидация, подтверждение операций и сообщения об ошибках/пустых списках
- [ ] Заполнена вся документация по шаблонам (`README.md`, `docs/requirements.md`, `docs/testing.md`, ER-схема)

---

## ЧАСТЬ 3. ЭСТАФЕТА АГЕНТОВ (ОБНОВЛЯЕТСЯ ПЕРЕД СМЕНОЙ CLI)

### Статус последней сессии
- **Активный агент:** OpenCode (DeepSeek V4.1 Flash Free)
- **Последний коммит Git:** `c46a533` — `docs: зафиксирован хэш коммита экрана отчётов`
- **Этап 3, шаг 1 (документация):**
  - `docs/requirements.md` заполнен: FR-01…FR-08, 6 пользовательских сценариев, BR-01…BR-06, полный TX-01 с `SqlTransaction`/`UPDLOCK, ROWLOCK`, 6 сущностей, UI/отчёт/валидация, границы.
  - `docs/database-schema.md` заполнен: таблицы, типы, PK/FK/UQ/CK, индексы, правила удаления, поддержка TX-01; добавлена ER-диаграмма `docs/database-schema.svg` (проверена как XML и отрисовкой в Edge).
  - `database/README.md` описывает точное развёртывание `schema.sql` → `seed.sql`, проверочные запросы и контрольные значения: 7 категорий, 19 товаров, 4 склада, 28 движений, 129 строк, 43 остатка, 11 909 единиц, 642 123,50 ₽.
  - Сборка не менялась; код не затрагивался.

### Контракты и сигнатуры (накопительная база — не стирать рабочее)

#### 1. Фактическая структура репозитория (шаблон преподавателя)
```text
EasyKeep/
├── .gitignore                   # bin/obj, .vs, секреты, *.mdf/*.ldf/*.bak
├── AGENTS.md, QWEN.md           # требование читать project.md и project_context.md
├── README.md                    # паспорт проекта: автор, тема, стек, запуск, BR/TX, скриншоты
├── START_HERE.md                # ТЗ, календарь, чек-лист сдачи
├── context/
│   ├── project.md               # продуктовое видение EasyKeep
│   ├── project_context.md       # конституция, чек-лист, эстафета памяти
│   └── requirements.docx        # исходное ТЗ (бинарный файл)
├── database/
│   ├── README.md                # параметры БД и порядок развёртывания
│   ├── schema.sql               # заготовка: создать 5–7 связанных таблиц
│   └── seed.sql                 # заготовка: учебные и негативные данные
├── docs/
│   ├── requirements.md          # 5–8 функций, BR, TX-01, данные, границы
│   ├── database-schema.md       # ER-диаграмма + описание таблиц/ограничений
│   └── testing.md               # 5–8 тест-кейсов, минимум 2 отрицательных
├── screenshots/README.md        # 2–4 скриншота ключевых экранов
└── src/README.md                # решение и WPF-проект создаются здесь
```

#### 2. Требования шаблона, которые нельзя нарушать
- **README.md:** заменить все поля `[...]`; указать ФИО, группу, тему, назначение, реализованные возможности, версии Windows/.NET/WPF/SQL Server/`Microsoft.Data.SqlClient`, структуру, точный порядок развёртывания и запуска, BR-01/BR-02, TX-01, ссылки на `docs/` и 2–4 скриншота.
- **START_HERE.md:** минимум 3 функциональных экрана; CRUD либо архивирование; поиск и фильтрация; JOIN; минимум 2 бизнес-правила; 1 транзакция; 1 агрегированный отчёт; параметризованный SQL; освобождение соединений; SQL не в обработчиках кнопок; подтверждение опасных операций; понятные сообщения об ошибках и пустых результатах.
- **docs/requirements.md:** 5–8 функций в проверяемой форме (FR-01…), пользовательские сценарии, минимум BR-01/BR-02, полный сценарий TX-01 (транзакция, проверки, изменения, откат, блокировки), 5–7 таблиц, разделы UI/отчёта/валидации и границы проекта.
- **docs/database-schema.md:** собственная ER-диаграмма (`database-schema.png|svg|pdf`), таблица полей с типами SQL Server, PK/FK, ограничения целостности, правила удаления и поддержка TX-01. Имена таблиц/ключей должны совпадать с `database/schema.sql`.
- **docs/testing.md:** 5–8 содержательных тестов (TC-01…TC-08), минимум 2 отрицательных, обязательны проверка бизнес-правила и отката транзакции; для каждого — условия, действия, ожидаемый/фактический результат, статус `Не выполнен/Пройден/Не пройден`.
- **database/README.md:** название БД, версия/редакция SQL Server, аутентификация, инструмент, порядок выполнения `schema.sql` → `seed.sql`, проверка результата, поведение при повторном запуске, описание тестовых данных и настройки подключения.
- **database/schema.sql:** PK, FK, NOT NULL/UNIQUE/CHECK, обоснованные индексы, понятное поведение при повторном запуске; запрещены безусловное удаление БД и абсолютные локальные пути.
- **database/seed.sql:** данные для CRUD, поиска/JOIN, двух бизнес-правил, успешной транзакции и отката, отчёта с заранее известным итогом; порядок вставки по FK; вымышленные данные.
- **src/README.md:** решение и WPF-проект в `src/`, осмысленные имена, `Microsoft.Data.SqlClient`, разделение UI/логики/данных; SQL не в обработчиках кнопок; транзакция с Commit/Rollback.
- **Безопасность:** строка подключения только в `appsettings.Local.json` или `.env` (уже в `.gitignore`); в Git — пример без секретов; сообщения об ошибках без паролей и деталей строки подключения.
- **Git:** последовательная история, атомарные коммиты по микрошагам, без секретов.

#### 3. Структура кода (создана в микрошаге 1.4)
```text
src/EasyKeep.sln
src/EasyKeep/
├── EasyKeep.csproj                 # net10.0-windows, EF Core 10.0.15, SqlClient 7.1.1
├── App.xaml, App.xaml.cs
├── Styles.xaml                      # единая светлая дизайн-система и логотип
├── AssemblyInfo.cs
├── Data/
│   ├── AppConfig.cs                # строка подключения из appsettings*.json
│   ├── Entities/                   # Category, Item, Warehouse, Stock, Movement, MovementItem
│   ├── EasyKeepContext.cs          # EF Core: DbSet, связи, индексы, AsNoTracking в запросах
│   └── AdoNetMovementRepository.cs # ADO.NET: SqlTransaction, UPDLOCK, JOIN-отчёты
├── Services/
│   ├── DeficitService.cs           # светофор дефицита и прогноз
│   ├── AutoBalanceService.cs       # черновики перемещений
│   ├── MovementService.cs          # проверка остатков и проведение
│   └── DTO: DeficitRow, AutoBalanceDraft, StockCheckResult, StockShortage
├── Views/
│   └── MainWindow.xaml(.cs)        # стартовое окно-каркас
├── appsettings.json                # пустая строка подключения, без секретов
├── appsettings.Local.example.json  # пример настройки для другого ПК
└── appsettings.Local.json          # реальное подключение, в .gitignore
```
- `AppConfig.GetConnectionString()`: сначала читает `appsettings.Local.json`, затем `appsettings.json`; JSON допускает комментарии и запятые в конце.
- `MainWindow` вызывает `AppConfig` и показывает только факт настройки, без вывода строки подключения.
- Сборка: `dotnet build src/EasyKeep.sln` → 0 ошибок, 0 предупреждений (SDK 10.0.404).

#### 4. Схема БД `database/schema.sql` (реализована в микрошаге 1.2)
- База: `EasyKeep` (создаётся при отсутствии, `USE EasyKeep`), схема `dbo`, повторный запуск безопасен.
- `dbo.categories`: `id INT IDENTITY PK`, `name NVARCHAR(100) NOT NULL`; `UQ_categories_name(name)`.
- `dbo.items`: `id INT IDENTITY PK`, `sku NVARCHAR(50) NOT NULL` (`UQ_items_sku`), `name NVARCHAR(200) NOT NULL`, `category_id INT NOT NULL` (`FK_items_categories`), `unit NVARCHAR(20) NOT NULL`, `price DECIMAL(18,2) NOT NULL DEFAULT 0` (`CK_items_price >= 0`), `min_stock INT NOT NULL DEFAULT 0` (`CK_items_min_stock >= 0`); индекс `IX_items_name(name)`.
- `dbo.warehouses`: `id INT IDENTITY PK`, `name NVARCHAR(100) NOT NULL` (`UQ_warehouses_name`), `address NVARCHAR(300) NULL`.
- `dbo.stocks`: `id INT IDENTITY PK`, `warehouse_id INT NOT NULL` (`FK_stocks_warehouses`), `item_id INT NOT NULL` (`FK_stocks_items`), `quantity INT NOT NULL DEFAULT 0` (`CK_stocks_quantity >= 0`); `UQ_stocks_warehouse_item(warehouse_id, item_id)`, индекс `IX_stocks_item_id(item_id)`.
- `dbo.movements`: `id INT IDENTITY PK`, `type NVARCHAR(10) NOT NULL` (`CK_movements_type IN ('IN','OUT','TRANSFER')`), `from_warehouse_id INT NULL` (`FK_movements_from_warehouse`), `to_warehouse_id INT NULL` (`FK_movements_to_warehouse`), `created_at DATETIME2(0) NOT NULL DEFAULT SYSDATETIME()`, `comment NVARCHAR(500) NULL`; `CK_movements_warehouses` требует: IN — только `to`, OUT — только `from`, TRANSFER — оба склада и `from <> to`; индексы `IX_movements_created_at(created_at DESC)`, `IX_movements_type(type)`.
- `dbo.movement_items`: `id INT IDENTITY PK`, `movement_id INT NOT NULL` (`FK_movement_items_movements ON DELETE CASCADE`), `item_id INT NOT NULL` (`FK_movement_items_items`), `quantity INT NOT NULL` (`CK_movement_items_quantity > 0`); `UQ_movement_items_movement_item(movement_id, item_id)`, индекс `IX_movement_items_item_id(item_id)`.
- Файл сохранён в UTF-8 с BOM (корректная кириллица в SSMS); в SQL-литералах нет зависимости от кодировки, кроме комментариев/`PRINT`.

#### 5. Тестовые данные `database/seed.sql` (реализованы в микрошаге 1.3)
- Защита от повтора: `IF EXISTS (SELECT 1 FROM dbo.items WHERE sku = N'COF-001') RETURN;` в одном batch с загрузкой (без промежуточного `GO`).
- 7 категорий: Кофе, Чай, Молочные продукты, Сиропы, Упаковка, Выпечка, Расходники.
- 19 товаров (`COF-001`…`SUP-004`), единицы `кг/л/шт./упак.`, цены и `min_stock` заполнены.
- 4 склада: `Основной склад`, `Торговый зал`, `Кофейня на Арбате`, `Склад брака`.
- 28 документов и 129 строк за 30 дней: 2 `IN`, 10 `TRANSFER`, 16 `OUT`; даты считаются от `CAST(GETDATE() AS DATE)`.
- 43 строки `stocks` рассчитываются из движений через CTE `stock_deltas`; расхождений с историей нет, отрицательных остатков нет.
- Демо-сценарии: `Торговый зал/MILK-001 = 36` при `min_stock = 40` (красный), `Торговый зал/COF-001 = 25` (жёлтый по прогнозу), `Основной склад/SUP-004 = 0`, `TEA-001/TEA-002` без расхода (неликвид), `Склад брака` для перемещения и утилизации.

#### 6. Слой Data на EF Core (реализован в микрошаге 1.5)
- `Data/Entities/Category.cs`: `Id`, `Name`, `List<Item> Items`.
- `Data/Entities/Item.cs`: `Id`, `Sku`, `Name`, `CategoryId`, `Unit`, `Price`, `MinStock`, `Category`, `List<Stock> Stocks`, `List<MovementItem> MovementItems`.
- `Data/Entities/Warehouse.cs`: `Id`, `Name`, `Address?`, `List<Stock> Stocks`.
- `Data/Entities/Stock.cs`: `Id`, `WarehouseId`, `ItemId`, `Quantity`, `Warehouse`, `Item`.
- `Data/Entities/Movement.cs`: `Id`, `Type`, `FromWarehouseId?`, `ToWarehouseId?`, `CreatedAt`, `Comment?`, `FromWarehouse`, `ToWarehouse`, `List<MovementItem> Items`; константы `MovementTypes.In/Out/Transfer`.
- `Data/Entities/MovementItem.cs`: `Id`, `MovementId`, `ItemId`, `Quantity`, `Movement`, `Item`.
- `Data/EasyKeepContext.cs`: `DbSet` для 6 таблиц; конструкторы `()` и `(DbContextOptions<EasyKeepContext>)`; `OnConfiguring` использует `AppConfig.GetConnectionString()`, если опции не переданы; `OnModelCreating` настраивает уникальные индексы и связи (`DeleteBehavior.Restrict` для справочников, `Cascade` для строк движения).
- Проверка на LocalDB: `AsNoTracking`, `Include/ThenInclude`, `GroupBy` и проекции работают; дефицит находится корректно.

#### 7. ADO.NET слой `Data/AdoNetMovementRepository.cs` (реализован в микрошаге 1.6)
- `int CreateMovement(Movement movement)` — валидирует документ, открывает `SqlConnection`, создаёт `SqlTransaction(IsolationLevel.ReadCommitted)`, вставляет шапку через `OUTPUT INSERTED.id`, применяет строки и фиксирует/откатывает; возвращает id и записывает его в `movement.Id`.
- `List<MovementJournalRow> GetJournal(DateTime from, DateTime to, string? type = null, int? warehouseId = null, string? search = null)` — период `[from, to)`, фильтры по типу/складу, поиск по SKU/названию через `EXISTS` (итоги документа не искажаются).
- `List<WarehouseStockRow> GetStockReport()` — `LEFT JOIN` складов, остатков и товаров, `COUNT`/`SUM` количества и стоимости.
- `List<TopItemRow> GetTopItems(DateTime from, DateTime to, int limit = 10)` — топ по расходу `OUT` и перемещениям, `COUNT(DISTINCT m.id)`.
- `List<ItemMovementRow> GetItemHistory(int itemId, int limit = 50)` — история товара для «Рентгена».
- Приватные шаги: `InsertMovementHeader`, `ApplyMovementLine`, `EnsureStockRow`, `DecreaseStock`, `IncreaseStock`, `InsertMovementItem`, `ValidateMovement`, `ValidateWarehouses`, `ValidateLines`.
- `EnsureStockRow` создаёт нулевую строку остатка; `DecreaseStock` читает её с `WITH (UPDLOCK, ROWLOCK)` и при нехватке бросает `InsufficientStockException(itemName, available, requested)`; `IncreaseStock` делает `quantity = quantity + @quantity`.
- Все значения передаются через `SqlParameter` (`AddWithValue`); строка подключения берётся из `AppConfig`; технические `SqlException` оборачиваются в понятное `InvalidOperationException` без секретов.
- DTO отчётов: `Data/Reports/MovementJournalRow`, `WarehouseStockRow`, `TopItemRow`, `ItemMovementRow`.
- Проверено: `IN` +10, `TRANSFER` -4/+4, `OUT` -3, создание нового остатка, откат при нехватке, частичный откат второй строки, пустая накладная, совпадение складов.

#### 8. Слой бизнес-логики `Services/` (реализован в микрошаге 1.7)
- `DeficitService(EasyKeepContext context)`:
  - `Task<List<DeficitRow>> GetDeficitRowsAsync(int? warehouseId = null)` — строки светофора по всем складам или одному; сортировка: критичные → внимание → норма → неликвид.
  - Анализ за 30 дней, расход = `OUT` + исходящие `TRANSFER`; `AverageDailyConsumption`, `DaysLeft`, `Status`, `StatusText`, `DaysLeftText`.
  - Статусы: `Critical` — остаток < `min_stock` или ≤2 дней; `Warning` — ≤10 дней; `Normal` — >10 дней; `NonLiquid` — расход 0.
- `AutoBalanceService(EasyKeepContext context)`:
  - `Task<List<AutoBalanceDraft>> BuildDraftsAsync()` — по одному черновику `TRANSFER` на торговую точку; основной склад ищется по имени «Основной склад», склад брака исключается.
  - Пополнение до `min_stock`; учитывается излишек основного склада (`quantity - min_stock`); один товар не распределяется дважды.
  - `AutoBalanceDraft`: `ToWarehouseId`, `ToWarehouseName`, `Movement`, `LinesCount`, `TotalQuantity`.
- `MovementService(EasyKeepContext context)`:
  - `Task<StockCheckResult> CheckStockAsync(Movement movement)` — проверка остатка на складе-отправителе; для `IN` всегда достаточно; возвращает `StockShortage` с `Available`/`Requested` и подсказкой.
  - `Task<int> PostMovementAsync(Movement movement)` — проверяет остаток, при нехватке бросает `InsufficientStockException`, иначе вызывает `AdoNetMovementRepository.CreateMovement`.
  - `Task<int> GetAvailableQuantityAsync(int warehouseId, int itemId)` — доступный максимум для интерфейса.
- `StockCheckResult(bool IsEnough, IReadOnlyList<StockShortage> Shortages)`, `StockShortage.Message` содержит доступный максимум.

#### 9. UI-каркас и дизайн-система (реализованы в шаге 2.1)
- `src/EasyKeep/Styles.xaml`:
  - Кисти: `AppBackgroundBrush` (`#F8FAFC`), `CardBackgroundBrush` (`#FFFFFF`), `CardBorderBrush` (`#E2E8F0`), `AccentBrush` (`#2563EB`), `AccentHoverBrush`, `AccentSoftBrush` (`#EFF6FF`), `TextPrimaryBrush` (`#0F172A`), `TextSecondaryBrush` (`#64748B`), `TextMutedBrush`, `SuccessBrush`, `WarningBrush`, `DangerBrush`, `RowHoverBrush`, `DividerBrush`.
  - `EasyKeepLogo` (`DrawingImage`) — векторный логотип: синяя плитка с белым контуром коробки.
  - Стили текста: `PageTitleText`, `SectionTitleText`, `BodyText`, `CaptionText`, `MetricValueText`, `EmptyStateText`.
  - Контейнеры: `CardBorder` (белая карточка, рамка, радиус 12), `PillBorder`, `AppSeparator`.
  - Кнопки: `PrimaryButton`, `SecondaryButton`, `DangerButton`, `NavButton` (RadioButton для меню).
  - Ввод/таблицы: `FieldTextBox`, `FieldComboBox`, `ModernDataGrid` (стили заголовка, строки и ячейки).
- `App.xaml`: `Styles.xaml` подключён через `ResourceDictionary.MergedDictionaries`.
- `Views/MainWindow.xaml`:
  - Колонки 248 px (меню) + рабочая область; логотип, пункты `StocksNav`, `MovementsNav`, `CatalogNav`, `ReportsNav` с `Tag` = `stocks|movements|catalog|reports`.
  - `PageTitleText`, `PageSubtitleText`; `ContentArea` (`ContentControl`) и `PlaceholderPanel` для ещё не подключённых экранов.
- `Views/MainWindow.xaml.cs`: `NavButton_Checked` → `ShowSection(string)`; по умолчанию выбран «Остатки». На шагах 2.2–2.5 в `ShowSection` добавляются реальные `UserControl`.
- `Data/AppConfig.cs`: при отсутствии/пустой строке в `appsettings.Local.json` и `appsettings.json` возвращает `FallbackConnectionString` к `(localdb)\MSSQLLocalDB`, БД `EasyKeep`, Windows-аутентификация. Исключение больше не бросается.

#### 10. Экран «Остатки» и справочник складов (шаг 2.2)
- `Services/WarehouseService.cs`: `WarehouseService(EasyKeepContext context)`, `Task<List<Warehouse>> GetWarehousesAsync()` — `AsNoTracking`, сортировка по названию.
- `Views/StocksView.xaml(.cs)`:
  - `StocksView_Loaded` загружает склады (с синтетическим пунктом `Id = 0`, «Все склады») и строки светофора.
  - `LoadRowsAsync()` создаёт короткоживущий `EasyKeepContext`, вызывает `DeficitService.GetDeficitRowsAsync(warehouseId)`; фильтр склада `0` → «все».
  - `ApplyFilter()` фильтрует уже загруженный список по `Sku`/`ItemName` без запросов к БД; обновляет `SummaryText` («Позиций: N · критично: K») и `StateText`.
  - Колонки: SKU, товар, склад, остаток, минимум, расход/день, прогноз, бейдж светофора (`DataTrigger` по `DeficitStatus`).
  - `RefreshButton_Click` перезагружает строки; ошибки БД показываются понятным текстом без деталей подключения.
- `MainWindow.ShowSection`: для `stocks` возвращает `new StocksView()`, иначе показывает `PlaceholderPanel`.

#### 11. Экран «Движения» (шаг 2.3)
- `Services/MovementService.cs` (дополнено):
  - `Task<Item?> FindItemAsync(string query)` — точное совпадение по `Sku`, затем частичное по `Name`.
  - `Task<List<Item>> GetItemsAsync(IEnumerable<int> itemIds)` — товары для подписей предпросмотра.
  - `List<MovementJournalRow> GetJournal(DateTime from, DateTime to, string? type = null, int? warehouseId = null, string? search = null)`.
- `Data/Reports/MovementJournalRow.cs`: `TypeText` (`IN` → «Приход», `OUT` → «Расход», `TRANSFER` → «Перемещение») и `RouteText` (склад-источник → склад-получатель).
- `Views/MovementLineModel.cs`: `ItemId`, `Sku`, `Name`, `Unit`, `Quantity` с `INotifyPropertyChanged`.
- `Views/MovementsView.xaml(.cs)`:
  - формы IN/OUT/TRANSFER, склады показываются по типу операции; для TRANSFER склады автоматически разводятся;
  - `CommandLineBox_KeyDown` + `AddFromCommandLineAsync`: Enter → `FindItemAsync` → `+1`, повторный ввод увеличивает `Quantity`;
  - редактируемая колонка «Кол-во» в `LinesGrid`, удаление строки по ✕, итоги «Позиций / единиц»;
  - `PostButton_Click`: валидация, `MessageBox` подтверждения, `MovementService.PostMovementAsync`, обработка `InsufficientStockException` с подстановкой доступного максимума;
  - `AutoBalanceButton_Click`: `BuildDraftsAsync` → `AutoBalancePreviewRow` → `AutoBalanceDialog` → последовательное проведение черновиков;
  - журнал: период (`DatePicker`), тип, склад, поиск по товару, кнопка «Показать», пустые состояния и сообщение об ошибке подключения.
- `Views/AutoBalanceDialog.xaml(.cs)` и запись `AutoBalancePreviewRow(WarehouseName, Sku, ItemName, Quantity)`; `DialogResult=true` только по кнопке «Провести все».
- `MainWindow.ShowSection`: `movements` → `new MovementsView()`.

#### 12. Экран «Каталог» (шаг 2.4)
- `Services/CatalogService.cs`:
  - `Task<List<Item>> GetItemsAsync()` — `AsNoTracking().Include(Item.Category)`, сортировка по названию.
  - `Task<List<Category>> GetCategoriesAsync()`.
  - `Task<Item> CreateItemAsync(string sku, string name, int categoryId, string unit, decimal price, int minStock)`.
  - `Task<Item> UpdateItemAsync(int id, string sku, string name, int categoryId, string unit, decimal price, int minStock)`.
  - `Task DeleteItemAsync(int id)` — запрещает удаление товара, используемого в остатках/движениях.
  - `Task<Category> CreateCategoryAsync(string name)`, `UpdateCategoryAsync(int id, string name)`, `DeleteCategoryAsync(int id)` — запрет удаления категории с товарами.
  - `List<ItemMovementRow> GetItemHistory(int itemId, int limit = 50)` — обёртка над ADO.NET.
  - Валидация: пустые/длинные поля, отрицательные цена и `min_stock`, уникальность SKU/названия категории; `DbUpdateException` → понятное `InvalidOperationException`.
- `Data/Reports/ItemMovementRow.cs`: `DateText`, `TypeText`, `RouteText`, `QuantityText`.
- `Views/CatalogView.xaml(.cs)`:
  - таблица товаров, локальные поиск и фильтр по категории, счётчик «Товаров: N»;
  - кнопки «Добавить товар», «Изменить», «Удалить» (подтверждение), «Категории»;
  - `ItemsGrid_SelectionChanged` загружает историю выбранного товара в `HistoryList` (дата, тип, маршрут, количество, комментарий), есть состояния «не выбран»/«нет движений».
- `Views/ItemEditorDialog.xaml(.cs)`: свойства `Sku`, `ItemName`, `CategoryId`, `Unit`, `Price`, `MinStock`; валидация ввода.
- `Views/CategoryManagerDialog.xaml(.cs)`: список категорий, добавление, переименование, удаление с подтверждением.
- `MainWindow.ShowSection`: `catalog` → `new CatalogView()`.

#### 13. Экран «Отчёт» (шаг 2.5)
- `Services/ReportService.cs`: `List<WarehouseStockRow> GetStockReport()`, `List<TopItemRow> GetTopItems(DateTime from, DateTime to, int limit = 10)` — обёртки над `AdoNetMovementRepository`.
- `Views/ReportsView.xaml(.cs)`:
  - левая карточка «Остатки по складам»: метрики `WarehouseCountText`, `TotalQuantityText`, `TotalValueText`, таблица `StockGrid`, кнопка «Обновить»;
  - правая карточка «Топ активных товаров»: `FromDatePicker`/`ToDatePicker` (по умолчанию 30 дней), кнопка «Построить», таблица `TopItemsGrid`, счётчик `TopSummaryText`;
  - пустые состояния `StockStateText`/`TopStateText` и сообщения об ошибках подключения.
- `MainWindow.ShowSection`: `reports` → `new ReportsView()`. Все четыре раздела подключены, `PlaceholderPanel` остаётся только как резерв.

### Известные баги / незавершенные места
- Нет. Все четыре экрана работают и проверены; выполняется Этап 3 (тестирование, README, чистка репозитория).

### ТОЧНЫЙ СЛЕДУЮЩИЙ ШАГ (ЗАДАЧА №1 ДЛЯ СЛЕДУЮЩЕГО АГЕНТА)
1. **Тестирование:** прогнать TC-01…TC-08 на LocalDB (8 кейсов, минимум 4 негативных), заполнить `docs/testing.md` фактическими результатами и статусами, восстановить чистую базу из `seed.sql`.
2. **README:** полностью заполнить корневой `README.md`, удалить `AGENTS.md`, `QWEN.md`, `context/`, проверить `dotnet build src/EasyKeep.sln`.
