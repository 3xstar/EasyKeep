# Исходный код приложения EasyKeep

В папке находятся решение `EasyKeep.sln` и WPF-проект `EasyKeep` (C#, `net10.0-windows`).

## Структура проекта

```text
src/EasyKeep/
├── EasyKeep.csproj                  # .NET 10, WPF, EF Core 10, Microsoft.Data.SqlClient 7.1.1
├── App.xaml, App.xaml.cs            # запуск приложения и подключение стилей
├── Styles.xaml                      # светлая дизайн-система, логотип, стили таблиц и кнопок
├── Data/
│   ├── AppConfig.cs                 # строка подключения и фоллбэк на LocalDB
│   ├── EasyKeepContext.cs           # EF Core: 6 DbSet, связи, уникальные индексы
│   ├── AdoNetMovementRepository.cs  # ADO.NET: SqlTransaction, UPDLOCK, JOIN-отчёты
│   ├── Entities/                    # Category, Item, Warehouse, Stock, Movement, MovementItem
│   └── Reports/                     # DTO журнала, остатков, топа и истории товара
├── Services/
│   ├── CatalogService.cs            # CRUD каталога и защита связанных записей
│   ├── MovementService.cs           # проверка остатков и проведение документов
│   ├── DeficitService.cs            # светофор дефицита и прогноз расхода
│   ├── AutoBalanceService.cs        # черновики авто-балансировки складов
│   ├── ReportService.cs             # агрегированные отчёты
│   └── WarehouseService.cs          # справочник складов
├── Views/
│   ├── MainWindow.xaml(.cs)         # главное окно и навигация
│   ├── StocksView.xaml(.cs)         # остатки и светофор
│   ├── MovementsView.xaml(.cs)      # приход, расход, перемещение, журнал
│   ├── CatalogView.xaml(.cs)        # каталог и «Рентген товара»
│   ├── ReportsView.xaml(.cs)        # отчёт
│   └── *Dialog.xaml(.cs)            # редактор товара, категории, авто-балансировка
├── appsettings.json                 # общая конфигурация без секретов
├── appsettings.Local.example.json   # пример локальной строки подключения
└── appsettings.Local.json           # реальная строка подключения, в .gitignore
```

## Архитектура

Зависимости направлены от интерфейса к данным: `UI (Views)` → `Services` → `Data`. SQL-запросы не размещены в обработчиках кнопок. EF Core используется для справочников, фильтрации, проекций и расчёта дефицита; чистый ADO.NET — для проведения движений в транзакции и аналитических JOIN-отчётов.

Ключевые решения:

- `AdoNetMovementRepository.CreateMovement` выполняет вставку шапки, строк и изменение остатков в одной транзакции `SqlTransaction` с `IsolationLevel.ReadCommitted`.
- Остаток читается запросом `SELECT ... FROM dbo.stocks WITH (UPDLOCK, ROWLOCK)`; при нехватке выбрасывается `InsufficientStockException` с доступным максимумом, затем вызывается `Rollback`.
- Все пользовательские значения передаются через `SqlParameter`; соединения и читатели освобождаются через `using`.
- `MovementService.PostMovementAsync` выполняет предварительную проверку остатков, а финальную проверку под блокировкой — репозиторий.
- `AppConfig.GetConnectionString()` читает `appsettings.Local.json`, затем `appsettings.json`, а при отсутствии настройки использует LocalDB.
- `MainWindow` подключает четыре экрана через `ContentControl`; каждый экран создаёт короткоживущий `EasyKeepContext`.

## Сборка и запуск

```powershell
dotnet restore src/EasyKeep.sln
dotnet build src/EasyKeep.sln
dotnet run --project src/EasyKeep/EasyKeep.csproj
```

Перед первым запуском разверните базу по [инструкции](../database/README.md) и при необходимости настройте строку подключения. Порядок действий для другого компьютера описан в [корневом README](../README.md).
